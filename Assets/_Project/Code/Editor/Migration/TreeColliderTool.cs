using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Technie.PhysicsCreator;
using Technie.PhysicsCreator.Rigid;

namespace MrMoonlight.EditorTools.Migration
{
	// Turns Carlos's painted Technie hulls (one hull per log/branch, painted in Prefab Mode on
	// the "Visual" child) into tight, separated convex-hull colliders.
	//
	//   TreeColliderTool.Plan("AP_Tree_X")    dry run: nothing changes, writes report + image
	//   TreeColliderTool.Apply("AP_Tree_X")   splits the paint, generates colliders, verifies
	//   TreeColliderTool.Restore("AP_Tree_X") puts every "<name>_sNN" back into "<name>"
	//
	// Reports and images go to <project>/Temp/TreeColliders/. Full process:
	// Docs/technie-vegetation-collider-process.md
	public static class TreeColliderTool
	{
		public static LimbSegmenter.Settings Settings = new LimbSegmenter.Settings();

		// Cover (2026-09-18): how far a hull's surface may stand outside the WHOLE tree mesh
		// before it counts as a collider in visible air. Measured every CoverSpacing metres over
		// the hull surface; points at or below CoverGroundY (local height, metres) are under the
		// terrain and ignored.
		public static float CoverTolerance = 0.05f;
		public static float CoverSpacing = 0.04f;
		public static float CoverGroundY = 0.03f;
		// Only stand-off over OPEN air counts: a gap a ball of this radius (metres) fits into.
		// Bark grooves and knots narrower than that are roughness, not see-through air - with
		// every point counted, Deadtree06 came out as 1,278 colliders (2026-09-18).
		public static float OpenAirRadius = 0.06f;
		// The cover passes (plane splits, Face patches) stop after this many seconds per Plan/Apply
		// and leave the remaining pieces as they are, with a NOTE: a plan once froze the Editor
		// for 13 minutes on Deadtree06.
		public static float CoverBudgetSeconds = 90f;
		// Cover refinement: a piece whose hull stands off the mesh by more than CoverTolerance is
		// split by the best of several planes (or into its connected parts) until it doesn't, or
		// until a split no longer helps. At most this many extra pieces per segmenter piece.
		public static bool RefineCover = true;
		public static int MaxCoverSplitsPerPiece = 24;
		// Pieces still over tolerance after the plane splits become small inward Face hulls.
		public static bool FacePatchFallback = true;

		private const int PreviewLayer = 31;
		private static readonly Regex SplitName = new Regex(@"_s\d+$");

		private static string ReportDir
		{
			get { return System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "TreeColliders")); }
		}

		private class Context
		{
			public PrefabStage Stage;
			public GameObject Root;
			public Transform Visual;
			public RigidColliderCreator Creator;
			public PaintingData Paint;
			public Mesh Mesh;
			public Vector3[] Verts;
			public int[] Tris;
			public Vector3 Scale;
			public MeshCoverField Field;
			public System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
			public bool BudgetNoted;
		}

		private class PieceStats
		{
			public LimbSegmenter.Piece Piece;
			public Hull Target;
			public Mesh HullMesh;
			public float Volume;
			public bool Flagged;
			public MeshCoverField.Stats Cover;
		}

		private class HullPlan
		{
			public Hull Source;
			public string BaseName;
			public List<int> OriginalFaces;
			public int GivenAway;
			public LimbSegmenter.Result Seg;
			public List<PieceStats> Pieces = new List<PieceStats>();
			public float OriginalVolume;
		}

		private class PlanSet
		{
			public List<HullPlan> Plans = new List<HullPlan>();
			public List<Hull> Duplicates = new List<Hull>();
			public List<string> PaintNotes = new List<string>();
		}

		// Zoomed dry run: plans every painted hull and renders four sides framed on the listed
		// pieces of one hull (report numbering, 1 = s01). Everything else in frame is drawn too,
		// so the branches meeting there are visible.
		public static string PlanFocus(string prefabName, string hullName, int[] pieceNumbers)
		{
			Context ctx = OpenContext(prefabName);
			PlanSet set = BuildPlans(ctx, null);
			int offset = 0;
			HullPlan target = null;
			foreach (HullPlan plan in set.Plans)
			{
				if (plan.Source.name == hullName) { target = plan; break; }
				offset += plan.Pieces.Count;
			}
			if (target == null) return "No painted hull named " + hullName;
			List<int> focus = new List<int>();
			foreach (int n in pieceNumbers) focus.Add(offset + n - 1);
			string image = RenderPreview(ctx, set.Plans, prefabName + "_focus.png", focus);
			DestroyMeshes(set.Plans);
			return image;
		}

		public static string Plan(string prefabName, string[] onlyHullNames = null)
		{
			Context ctx = OpenContext(prefabName);
			PlanSet set = BuildPlans(ctx, onlyHullNames);
			string image = RenderPreview(ctx, set.Plans, prefabName + "_plan.png");
			string report = WriteReport(ctx, set, "PLAN (nothing changed)", image, null, prefabName + "_plan.txt");
			DestroyMeshes(set.Plans);
			return report;
		}

		public static string Apply(string prefabName, string[] onlyHullNames = null)
		{
			Context ctx = OpenContext(prefabName);
			PlanSet set = BuildPlans(ctx, onlyHullNames);
			List<HullPlan> plans = set.Plans;

			foreach (HullPlan plan in plans)
			{
				int total = 0;
				foreach (PieceStats p in plan.Pieces) total += p.Piece.Faces.Count;
				if (total != plan.OriginalFaces.Count)
					throw new System.InvalidOperationException(plan.Source.name + ": pieces hold " + total + " tris but the paint had " + plan.OriginalFaces.Count + " - refusing to apply, nothing changed.");
			}

			Undo.RegisterCompleteObjectUndo(ctx.Paint, "Split tree hulls");
			// Exact duplicates only: every one of their triangles lives on in the hull they copy.
			foreach (Hull dup in set.Duplicates)
				ctx.Paint.RemoveHull(ctx.Paint.hulls.IndexOf(dup));
			int colourIndex = 0;
			foreach (HullPlan plan in plans)
			{
				int insertAt = ctx.Paint.hulls.IndexOf(plan.Source) + 1;
				string baseName = plan.Source.name;
				for (int i = 0; i < plan.Pieces.Count; i++)
				{
					Hull target = plan.Source;
					if (i > 0)
					{
						target = ctx.Paint.AddHull(HullType.ConvexHull, plan.Source.material, plan.Source.isChildCollider, plan.Source.isTrigger);
						ctx.Paint.hulls.RemoveAt(ctx.Paint.hulls.Count - 1);
						ctx.Paint.hulls.Insert(insertAt++, target);
					}
					target.name = baseName + "_s" + (i + 1).ToString("00");
					target.type = plan.Pieces[i].Piece.AsFace ? HullType.Face : HullType.ConvexHull;
					if (plan.Pieces[i].Piece.AsFace) target.faceThickness = plan.Pieces[i].Piece.FaceThickness;
					target.colour = PieceColour(colourIndex++);
					target.SetSelectedFaces(plan.Pieces[i].Piece.Faces, ctx.Mesh);
					plan.Pieces[i].Target = target;
				}
			}
			ctx.Paint.activeHull = -1;
			EditorUtility.SetDirty(ctx.Paint);

			GenerateAll(ctx);
			string verify = VerifyColliders(ctx);

			// Render what was actually generated, not the plan: swap each piece's preview mesh
			// for a copy of the collision mesh its MeshCollider now uses.
			foreach (HullPlan plan in plans)
				foreach (PieceStats p in plan.Pieces)
				{
					if (p.HullMesh != null) Object.DestroyImmediate(p.HullMesh);
					p.HullMesh = p.Target != null ? GeneratedHullMesh(p.Target) : null;
				}
			string image = RenderPreview(ctx, plans, prefabName + "_applied.png");
			string report = WriteReport(ctx, set, "APPLIED", image, verify, prefabName + "_applied.txt");
			DestroyMeshes(plans);
			return report;
		}

		// Checks the tree as it is right now: every painted hull next to the collider Technie
		// actually generated for it (not a plan), collider bookkeeping, and the largest gaps.
		// Use after Apply, and after any hand edit + Regenerate.
		public static string Inspect(string prefabName)
		{
			return InspectCore(prefabName, false, Vector3.zero, 0f);
		}

		// Close-up of the CURRENT colliders (PlanFocus only works before Apply): four sides and a
		// top view framed on every piece whose paint comes within `radius` metres of `centre`
		// (metric Visual-local coordinates, as the COVER lines print them). Everything starting
		// above the framed pieces is hidden in the top view.
		public static string InspectFocus(string prefabName, Vector3 centre, float radius)
		{
			return InspectCore(prefabName, true, centre, radius);
		}

		private static string InspectCore(string prefabName, bool focus, Vector3 centre, float radius)
		{
			Context ctx = OpenContext(prefabName);
			List<HullPlan> hulls = new List<HullPlan>();
			List<PieceStats> all = new List<PieceStats>();
			foreach (Hull h in ctx.Paint.hulls)
			{
				int[] faces = h.GetSelectedFaces();
				if (faces.Length == 0) continue;
				LimbSegmenter.Piece piece = new LimbSegmenter.Piece { Faces = new List<int>(faces) };
				PieceStats st = new PieceStats { Piece = piece, Target = h };
				st.HullMesh = GeneratedHullMesh(h);
				st.Cover = CoverOf(st.HullMesh, ctx, CoverSpacing);
				st.Flagged = st.Cover.Max > CoverTolerance;
				HullPlan plan = new HullPlan { Source = h, BaseName = h.name, OriginalFaces = piece.Faces };
				plan.Pieces.Add(st);
				hulls.Add(plan);
				all.Add(st);
			}

			StringBuilder sb = new StringBuilder();
			sb.AppendLine(ctx.Root.name + " - INSPECT (current colliders)");
			sb.Append(VerifyColliders(ctx));
			int bad = 0, faceHulls = 0;
			foreach (PieceStats p in all) { if (p.Flagged) bad++; if (p.Target.type == HullType.Face) faceHulls++; }
			sb.AppendLine(bad + " of " + all.Count + " colliders stand > " + CoverTolerance + " m outside the tree mesh (" + faceHulls + " are Face hulls)");
			sb.Append(WorstCover(hulls));
			List<int> focusList = null;
			if (focus)
			{
				focusList = new List<int>();
				for (int i = 0; i < all.Count; i++)
					foreach (int f in all[i].Piece.Faces)
					{
						bool near = false;
						for (int k = 0; k < 3 && !near; k++) near = (Vector3.Scale(ctx.Verts[ctx.Tris[f * 3 + k]], ctx.Scale) - centre).magnitude <= radius;
						if (near) { focusList.Add(i); break; }
					}
				sb.AppendLine("Focus: " + focusList.Count + " pieces within " + radius + " m of " + centre);
			}
			string image = RenderPreview(ctx, hulls, prefabName + (focus ? "_inspectfocus.png" : "_inspect.png"), focusList);
			sb.AppendLine("Image: " + image);
			DestroyMeshes(hulls);

			System.IO.Directory.CreateDirectory(ReportDir);
			string path = System.IO.Path.Combine(ReportDir, prefabName + (focus ? "_inspectfocus.txt" : "_inspect.txt"));
			System.IO.File.WriteAllText(path, sb.ToString());
			return sb.ToString() + "Report: " + path;
		}

		// Regenerates colliders for the hulls as they are now and checks them - for use after
		// Carlos edits a split piece by hand.
		public static string Regenerate(string prefabName)
		{
			Context ctx = OpenContext(prefabName);
			GenerateAll(ctx);
			return VerifyColliders(ctx);
		}

		public static string Restore(string prefabName)
		{
			Context ctx = OpenContext(prefabName);
			Undo.RegisterCompleteObjectUndo(ctx.Paint, "Restore tree hulls");
			StringBuilder sb = new StringBuilder();

			Dictionary<string, List<Hull>> groups = new Dictionary<string, List<Hull>>();
			foreach (Hull h in ctx.Paint.hulls)
			{
				if (!SplitName.IsMatch(h.name)) continue;
				string baseName = SplitName.Replace(h.name, "");
				if (!groups.ContainsKey(baseName)) groups[baseName] = new List<Hull>();
				groups[baseName].Add(h);
			}

			foreach (KeyValuePair<string, List<Hull>> g in groups)
			{
				List<int> union = new List<int>();
				foreach (Hull h in g.Value) union.AddRange(h.GetSelectedFaces());
				Hull keep = g.Value[0];
				keep.name = g.Key;
				keep.type = HullType.ConvexHull;
				keep.SetSelectedFaces(union, ctx.Mesh);
				for (int i = 1; i < g.Value.Count; i++)
					ctx.Paint.RemoveHull(ctx.Paint.hulls.IndexOf(g.Value[i]));
				sb.AppendLine(g.Key + ": merged " + g.Value.Count + " pieces back (" + union.Count + " tris)");
			}
			EditorUtility.SetDirty(ctx.Paint);

			ctx.Creator.RemoveAllGenerated();
			sb.AppendLine("Generated colliders removed. Colliders left on Visual: " + ctx.Visual.GetComponents<Collider>().Length);
			return sb.ToString();
		}

		// Debug for the cover metric: stand-off (metres outside the tree mesh) at n points on the
		// line from -> to (metric Visual-local coordinates, i.e. local position x the Visual's scale).
		public static string Probe(string prefabName, Vector3 from, Vector3 to, int n)
		{
			Context ctx = OpenContext(prefabName);
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("scale " + ctx.Scale + " bounds " + ctx.Mesh.bounds);
			for (int i = 0; i < n; i++)
			{
				Vector3 p = Vector3.Lerp(from, to, n > 1 ? (float)i / (n - 1) : 0f);
				sb.AppendLine(p.ToString("F2") + "  " + ctx.Field.Outside(p).ToString("F3"));
			}
			return sb.ToString();
		}

		// Read-only, from disk: colliders on Visual, painted hull count, and the first hull names,
		// for each test-copy prefab named.
		public static string Status(string[] prefabNames)
		{
			StringBuilder sb = new StringBuilder();
			foreach (string n in prefabNames)
			{
				string path = "Assets/_Project/Art/VegetationPrefabs/AST116_ColliderTest/" + n + ".prefab";
				GameObject root = PrefabUtility.LoadPrefabContents(path);
				try
				{
					Transform vis = root.transform.Find("Visual");
					RigidColliderCreator cr = vis != null ? vis.GetComponent<RigidColliderCreator>() : null;
					sb.Append(n + ": colliders " + (vis != null ? vis.GetComponents<Collider>().Length : -1));
					if (cr != null && cr.paintingData != null)
					{
						sb.Append(", hulls " + cr.paintingData.hulls.Count + " [");
						int k = 0;
						foreach (Hull h in cr.paintingData.hulls)
							if (k++ < 8) sb.Append(h.name + ":" + h.GetSelectedFaces().Length + " ");
						sb.Append("]");
					}
					else sb.Append(", no paint");
					sb.AppendLine();
				}
				finally { PrefabUtility.UnloadPrefabContents(root); }
			}
			return sb.ToString();
		}

		// A copy of the collider shape Technie actually built for a hull. Face hulls store the raw
		// face points (PhysX cooks them convex), so their shape is the hull of those points.
		private static Mesh GeneratedHullMesh(Hull h)
		{
			if (h.type == HullType.Face)
			{
				if (h.faceCollisionMesh == null) return null;
				try { return QHullUtil.FindConvexHull(h.name, h.faceCollisionMesh, false); }
				catch { return null; }
			}
			return h.collisionMesh != null ? Object.Instantiate(h.collisionMesh) : null;
		}

		private static Mesh ColliderMeshOf(Hull h)
		{
			return h.type == HullType.Face ? h.faceCollisionMesh : h.collisionMesh;
		}

		private static Context OpenContext(string prefabName)
		{
			Context ctx = new Context();
			ctx.Stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (ctx.Stage == null) throw new System.InvalidOperationException("Open the tree prefab in Prefab Mode first.");
			ctx.Root = ctx.Stage.prefabContentsRoot;
			if (ctx.Root.name != prefabName) throw new System.InvalidOperationException("Prefab Mode has '" + ctx.Root.name + "' open, not '" + prefabName + "'.");
			ctx.Visual = ctx.Root.transform.Find("Visual");
			if (ctx.Visual == null) throw new System.InvalidOperationException("No 'Visual' child under " + prefabName);
			ctx.Creator = ctx.Visual.GetComponent<RigidColliderCreator>();
			if (ctx.Creator == null || ctx.Creator.paintingData == null) throw new System.InvalidOperationException("No painted hulls on " + prefabName + "/Visual.");
			ctx.Paint = ctx.Creator.paintingData;
			ctx.Mesh = ctx.Visual.GetComponent<MeshFilter>().sharedMesh;
			ctx.Verts = ctx.Mesh.vertices;
			ctx.Tris = ctx.Mesh.triangles;
			ctx.Scale = ctx.Visual.lossyScale;
			Vector3[] metric = new Vector3[ctx.Verts.Length];
			for (int i = 0; i < metric.Length; i++) metric[i] = Vector3.Scale(ctx.Verts[i], ctx.Scale);
			ctx.Field = new MeshCoverField(metric, ctx.Tris, ctx.Scale.x * ctx.Scale.y * ctx.Scale.z < 0f);
			ctx.Field.OpenAirRadius = OpenAirRadius;
			return ctx;
		}

		// Paint ownership, applied across every hull (even when planning just one):
		//  - A hull with exactly the same triangles as an earlier hull is a duplicate and is
		//    dropped (2026-09-17: branch_17 was a copy of branch_12).
		//  - A triangle painted into several hulls belongs to the one with the fewest triangles.
		//    Technie doesn't remove a triangle from a hull when it's painted into another, so a
		//    trunk hull painted first still holds every branch painted separately afterwards
		//    (Juniper02's log_1 held all of 11 branch hulls). Carlos's rule: the branch wins.
		private static PlanSet BuildPlans(Context ctx, string[] onlyHullNames)
		{
			PlanSet set = new PlanSet();
			HashSet<string> only = onlyHullNames != null ? new HashSet<string>(onlyHullNames) : null;
			// The Visual's local origin is the tree's base (feet-origin convention).
			Vector3 baseHint = Vector3.zero;

			List<Hull> painted = new List<Hull>();
			Dictionary<string, Hull> bySignature = new Dictionary<string, Hull>();
			foreach (Hull h in ctx.Paint.hulls)
			{
				int[] f = h.GetSelectedFaces();
				if (f.Length == 0) continue;
				int[] sorted = (int[])f.Clone();
				System.Array.Sort(sorted);
				string signature = string.Join(",", sorted);
				Hull first;
				if (bySignature.TryGetValue(signature, out first))
				{
					set.Duplicates.Add(h);
					set.PaintNotes.Add(h.name + " is an exact copy of " + first.name + " (" + f.Length + " tris) - dropped, " + first.name + " kept");
					continue;
				}
				bySignature[signature] = h;
				painted.Add(h);
			}

			Dictionary<int, Hull> owner = new Dictionary<int, Hull>();
			foreach (Hull h in painted)
				foreach (int f in h.GetSelectedFaces())
				{
					Hull current;
					if (!owner.TryGetValue(f, out current) || h.GetSelectedFaces().Length < current.GetSelectedFaces().Length)
						owner[f] = h;
				}

			foreach (Hull h in painted)
			{
				if (only != null ? !only.Contains(h.name) : SplitName.IsMatch(h.name)) continue;
				int[] all = h.GetSelectedFaces();
				List<int> faces = new List<int>();
				foreach (int f in all) if (owner[f] == h) faces.Add(f);
				if (faces.Count < all.Length)
					set.PaintNotes.Add(h.name + ": " + (all.Length - faces.Count) + " of its " + all.Length + " tris are also painted in smaller hulls and go to those instead");
				if (faces.Count == 0) continue;

				HullPlan plan = new HullPlan { Source = h, BaseName = h.name, OriginalFaces = faces, GivenAway = all.Length - faces.Count };
				plan.Seg = LimbSegmenter.Segment(ctx.Verts, ctx.Tris, faces.ToArray(), ctx.Scale, baseHint, Settings);
				plan.OriginalVolume = HullVolume(h.name, plan.OriginalFaces, ctx, out Mesh unused);
				if (unused != null) Object.DestroyImmediate(unused);

				foreach (LimbSegmenter.Piece piece in plan.Seg.Pieces)
				{
					if (!RefineCover) { plan.Pieces.Add(MakeStats(h.name, piece, ctx)); continue; }
					foreach (LimbSegmenter.Piece rp in RefineByCover(h.name, piece, ctx, plan.Seg.Notes))
					{
						PieceStats st = MakeStats(h.name, rp, ctx);
						if (!st.Flagged || !FacePatchFallback || OverBudget(ctx, plan.Seg.Notes)) { plan.Pieces.Add(st); continue; }
						if (st.HullMesh != null) Object.DestroyImmediate(st.HullMesh);
						foreach (LimbSegmenter.Piece fp in FacePatches(h.name, rp, ctx, plan.Seg.Notes))
							plan.Pieces.Add(MakeStats(h.name, fp, ctx));
					}
				}
				set.Plans.Add(plan);
			}
			return set;
		}

		private static PieceStats MakeStats(string name, LimbSegmenter.Piece piece, Context ctx)
		{
			PieceStats st = new PieceStats { Piece = piece };
			st.Volume = PieceHull(name, piece, ctx, out st.HullMesh);
			st.Cover = CoverOf(st.HullMesh, ctx, CoverSpacing);
			st.Flagged = st.Cover.Max > CoverTolerance;
			return st;
		}

		private static MeshCoverField.Stats CoverOf(Mesh hull, Context ctx, float spacing, float bail = float.MaxValue)
		{
			if (hull == null) return new MeshCoverField.Stats();
			Vector3[] hv = hull.vertices;
			for (int i = 0; i < hv.Length; i++) hv[i] = Vector3.Scale(hv[i], ctx.Scale);
			return ctx.Field.Measure(hv, hull.triangles, spacing, CoverGroundY, bail);
		}

		private static bool OverBudget(Context ctx, List<string> notes)
		{
			if (ctx.Clock.Elapsed.TotalSeconds <= CoverBudgetSeconds) return false;
			if (!ctx.BudgetNoted)
			{
				notes.Add("COVER BUDGET: " + CoverBudgetSeconds + " s used up - the pieces after this point were not refined for cover. Check the COVER list.");
				ctx.BudgetNoted = true;
			}
			return true;
		}

		private static MeshCoverField.Stats CoverOfFace(string name, List<int> faces, float thickness, Context ctx, float spacing, float bail = float.MaxValue)
		{
			Mesh m;
			FaceHullVolume(name, faces, thickness, ctx, out m);
			if (m == null) return new MeshCoverField.Stats { Max = float.MaxValue };
			MeshCoverField.Stats c = CoverOf(m, ctx, spacing, bail);
			Object.DestroyImmediate(m);
			return c;
		}

		// Last resort for a piece the plane splits couldn't bring within CoverTolerance (usually
		// because the mesh's triangles are as big as the dent: DeadTree01's edges are ~0.5 m, so
		// a trunk flare or a crotch is one or two triangles wide). Break it into small patches of
		// adjacent triangles, each built as a Technie Face hull pushed inward: a patch's collider
		// lies on the bark it came from, so its only stand-off is the patch's own outward
		// concavity. A patch grows from its largest triangle while it stays within tolerance.
		// Thickness: the thickest of FaceThicknesses (metres) that keeps a single triangle within
		// tolerance - too thick and a thin limb's back side pokes through.
		public static float[] FaceThicknesses = { 0.3f, 0.15f, 0.08f, 0.04f, 0.02f };
		public static int MaxTrisPerFacePatch = 24;

		private static List<LimbSegmenter.Piece> FacePatches(string name, LimbSegmenter.Piece piece, Context ctx, List<string> notes)
		{
			float coarse = CoverSpacing * 2f;
			float unitScale = (Mathf.Abs(ctx.Scale.x) + Mathf.Abs(ctx.Scale.y) + Mathf.Abs(ctx.Scale.z)) / 3f;

			// Edge adjacency over welded positions.
			Dictionary<long, List<int>> byEdge = new Dictionary<long, List<int>>();
			Dictionary<Vector3Int, int> weld = new Dictionary<Vector3Int, int>();
			int W(Vector3 p)
			{
				Vector3Int q = new Vector3Int(Mathf.RoundToInt(p.x / Settings.WeldEpsilon), Mathf.RoundToInt(p.y / Settings.WeldEpsilon), Mathf.RoundToInt(p.z / Settings.WeldEpsilon));
				int id;
				if (!weld.TryGetValue(q, out id)) { id = weld.Count; weld[q] = id; }
				return id;
			}
			foreach (int f in piece.Faces)
			{
				int[] w = { W(ctx.Verts[ctx.Tris[f * 3]]), W(ctx.Verts[ctx.Tris[f * 3 + 1]]), W(ctx.Verts[ctx.Tris[f * 3 + 2]]) };
				for (int e = 0; e < 3; e++)
				{
					int a = Mathf.Min(w[e], w[(e + 1) % 3]), b = Mathf.Max(w[e], w[(e + 1) % 3]);
					long key = ((long)a << 32) | (uint)b;
					List<int> l;
					if (!byEdge.TryGetValue(key, out l)) { l = new List<int>(); byEdge[key] = l; }
					l.Add(f);
				}
			}
			Dictionary<int, List<int>> nbrs = new Dictionary<int, List<int>>();
			foreach (int f in piece.Faces) nbrs[f] = new List<int>();
			foreach (List<int> l in byEdge.Values)
				for (int i = 0; i < l.Count; i++)
					for (int j = 0; j < l.Count; j++)
						if (i != j && !nbrs[l[i]].Contains(l[j])) nbrs[l[i]].Add(l[j]);

			// Largest triangles first: they dominate the stand-off and anchor the patches.
			List<int> order = new List<int>(piece.Faces);
			order.Sort((x, y) => TriArea(y, ctx).CompareTo(TriArea(x, ctx)));
			HashSet<int> left = new HashSet<int>(piece.Faces);
			List<LimbSegmenter.Piece> result = new List<LimbSegmenter.Piece>();
			float worst = 0f;
			foreach (int seed in order)
			{
				if (!left.Contains(seed)) continue;
				List<int> patch = new List<int> { seed };
				left.Remove(seed);

				float thickness = FaceThicknesses[FaceThicknesses.Length - 1];
				MeshCoverField.Stats cur = new MeshCoverField.Stats { Max = float.MaxValue };
				foreach (float t in FaceThicknesses)
				{
					MeshCoverField.Stats c = CoverOfFace(name, patch, t / unitScale, ctx, coarse);
					if (c.Max <= CoverTolerance || c.Max < cur.Max) { thickness = t; cur = c; }
					if (c.Max <= CoverTolerance) break;
				}

				bool grew = true;
				while (grew && patch.Count < MaxTrisPerFacePatch)
				{
					grew = false;
					foreach (int f in patch.ToArray())
						foreach (int nb in nbrs[f])
						{
							if (!left.Contains(nb) || patch.Count >= MaxTrisPerFacePatch) continue;
							patch.Add(nb);
							MeshCoverField.Stats c = CoverOfFace(name, patch, thickness / unitScale, ctx, coarse, Mathf.Max(CoverTolerance, cur.Max));
							if (c.Max <= Mathf.Max(CoverTolerance, cur.Max)) { left.Remove(nb); cur = c; grew = true; }
							else patch.RemoveAt(patch.Count - 1);
						}
				}
				worst = Mathf.Max(worst, cur.Max);
				result.Add(new LimbSegmenter.Piece { Component = piece.Component, Arc = piece.Arc, IndexInArc = piece.IndexInArc, Start = piece.Start, End = piece.End, Faces = patch, AsFace = true, FaceThickness = thickness / unitScale });
			}
			notes.Add("face patches: piece at " + piece.Start.ToString("F2") + "-" + piece.End.ToString("F2") + " m on arm " + piece.Arc + " (" + piece.Faces.Count + " tris) -> " + result.Count + " Face hulls, worst stand-off " + worst.ToString("F2") + " m");
			return result;
		}

		private static float TriArea(int f, Context ctx)
		{
			Vector3 a = Vector3.Scale(ctx.Verts[ctx.Tris[f * 3]], ctx.Scale), b = Vector3.Scale(ctx.Verts[ctx.Tris[f * 3 + 1]], ctx.Scale), c = Vector3.Scale(ctx.Verts[ctx.Tris[f * 3 + 2]], ctx.Scale);
			return 0.5f * Vector3.Cross(b - a, c - a).magnitude;
		}

		private static MeshCoverField.Stats CoverOfFaces(string name, List<int> faces, Context ctx, float spacing, float bail = float.MaxValue)
		{
			Mesh m;
			HullVolume(name, faces, ctx, out m);
			if (m == null) return new MeshCoverField.Stats { Max = float.MaxValue };
			MeshCoverField.Stats c = CoverOf(m, ctx, spacing, bail);
			Object.DestroyImmediate(m);
			return c;
		}

		// Cover-driven splitting (2026-09-18). Slicing along the limb can't fix a concavity ACROSS
		// the limb - roots fused into the trunk at the ground, two arms fused under a fork - and
		// four rounds of tuning the ground footprint went in circles. So measure instead of guess:
		// while a piece's hull stands off the whole tree mesh by more than CoverTolerance, try
		// splitting it into its connected parts and by a set of planes (its principal axes, six
		// vertical planes, a level plane, and the plane through its axis and the worst stand-off
		// point) and keep the split whose worst part stands off least. Stops when a split no
		// longer buys a real improvement (bark grooves can't be fixed by splitting).
		private static List<LimbSegmenter.Piece> RefineByCover(string name, LimbSegmenter.Piece piece, Context ctx, List<string> notes)
		{
			List<LimbSegmenter.Piece> done = new List<LimbSegmenter.Piece>();
			float coarse = CoverSpacing * 2f;
			Queue<KeyValuePair<LimbSegmenter.Piece, MeshCoverField.Stats>> work = new Queue<KeyValuePair<LimbSegmenter.Piece, MeshCoverField.Stats>>();
			MeshCoverField.Stats first = CoverOfFaces(name, piece.Faces, ctx, coarse);
			work.Enqueue(new KeyValuePair<LimbSegmenter.Piece, MeshCoverField.Stats>(piece, first));
			int splits = 0;
			float worstAfter = 0f;
			while (work.Count > 0)
			{
				KeyValuePair<LimbSegmenter.Piece, MeshCoverField.Stats> item = work.Dequeue();
				LimbSegmenter.Piece p = item.Key;
				float cover = item.Value.Max;
				if (cover <= CoverTolerance || splits >= MaxCoverSplitsPerPiece || p.Faces.Count < 2 * Settings.MinTrisPerPiece || OverBudget(ctx, notes))
				{
					done.Add(p);
					worstAfter = Mathf.Max(worstAfter, cover);
					continue;
				}

				List<List<int>> best = null;
				float bestScore = cover;
				MeshCoverField.Stats[] bestParts = null;
				foreach (List<List<int>> cand in CoverSplitCandidates(p.Faces, item.Value.WorstPoint, ctx))
				{
					bool ok = true;
					foreach (List<int> part in cand) if (part.Count < 2 || PieceFlat(part, ctx)) { ok = false; break; }
					if (!ok) continue;
					float score = 0f;
					MeshCoverField.Stats[] parts = new MeshCoverField.Stats[cand.Count];
					for (int i = 0; i < cand.Count && score < bestScore; i++) { parts[i] = CoverOfFaces(name, cand[i], ctx, coarse, bestScore); score = Mathf.Max(score, parts[i].Max); }
					if (score < bestScore) { bestScore = score; best = cand; bestParts = parts; }
				}
				// A split must buy a real improvement, or bark grooves would shatter every ring.
				if (best == null || bestScore > cover - Mathf.Max(0.02f, 0.2f * (cover - CoverTolerance)))
				{
					done.Add(p);
					worstAfter = Mathf.Max(worstAfter, cover);
					continue;
				}
				splits += best.Count - 1;
				for (int i = 0; i < best.Count; i++)
				{
					LimbSegmenter.Piece child = new LimbSegmenter.Piece { Component = p.Component, Arc = p.Arc, IndexInArc = p.IndexInArc, Start = p.Start, End = p.End, Faces = best[i] };
					work.Enqueue(new KeyValuePair<LimbSegmenter.Piece, MeshCoverField.Stats>(child, bestParts[i]));
				}
			}
			if (done.Count > 1)
				notes.Add("cover: piece at " + piece.Start.ToString("F2") + "-" + piece.End.ToString("F2") + " m on arm " + piece.Arc + " split into " + done.Count + " (stand-off " + first.Max.ToString("F2") + " -> " + worstAfter.ToString("F2") + " m)");
			return done;
		}

		private static bool PieceFlat(List<int> faces, Context ctx)
		{
			HashSet<Vector3> ps = new HashSet<Vector3>();
			foreach (int f in faces) for (int k = 0; k < 3; k++) ps.Add(Vector3.Scale(ctx.Verts[ctx.Tris[f * 3 + k]], ctx.Scale));
			if (ps.Count < 4) return true;
			Vector3 mean = Vector3.zero;
			foreach (Vector3 p in ps) mean += p;
			mean /= ps.Count;
			float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
			foreach (Vector3 q in ps)
			{
				Vector3 d = q - mean;
				xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z; yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
			}
			float det = xx * (yy * zz - yz * yz) - xy * (xy * zz - yz * xz) + xz * (xy * yz - yy * xz);
			float trace = xx + yy + zz;
			return trace < 1e-12f || det / (trace * trace * trace) < 1e-5f;
		}

		private static IEnumerable<List<List<int>>> CoverSplitCandidates(List<int> faces, Vector3 worst, Context ctx)
		{
			int count = faces.Count;
			Vector3[] fc = new Vector3[count];
			Vector3 mean = Vector3.zero;
			for (int i = 0; i < count; i++)
			{
				int f = faces[i];
				fc[i] = Vector3.Scale((ctx.Verts[ctx.Tris[f * 3]] + ctx.Verts[ctx.Tris[f * 3 + 1]] + ctx.Verts[ctx.Tris[f * 3 + 2]]) / 3f, ctx.Scale);
				mean += fc[i];
			}
			mean /= count;

			// Connected parts (sharing a welded vertex): separate roots, or ring slivers.
			List<List<int>> parts = ConnectedParts(faces, ctx);
			if (parts.Count > 1) yield return parts;

			float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
			foreach (Vector3 p in fc)
			{
				Vector3 d = p - mean;
				xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z; yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
			}
			Matrix4x4 cov = Matrix4x4.zero;
			cov[0, 0] = xx; cov[0, 1] = xy; cov[0, 2] = xz; cov[1, 0] = xy; cov[1, 1] = yy; cov[1, 2] = yz; cov[2, 0] = xz; cov[2, 1] = yz; cov[2, 2] = zz;
			Vector3 e1 = PowerIterate(cov, new Vector3(0.3f, 1f, 0.2f));
			float l1 = Vector3.Dot(cov.MultiplyVector(e1), e1);
			Matrix4x4 cov2 = cov;
			for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) cov2[r, c] -= l1 * e1[r] * e1[c];
			Vector3 e2 = PowerIterate(cov2, Vector3.Cross(e1, Mathf.Abs(e1.y) < 0.9f ? Vector3.up : Vector3.right));
			e2 = (e2 - e1 * Vector3.Dot(e2, e1)).normalized;

			List<Vector3> normals = new List<Vector3> { e1, e2, Vector3.Cross(e1, e2).normalized };
			// The plane holding the piece's long axis and its worst stand-off point: on a star
			// cross-section that point sits in the air between two lobes.
			Vector3 toWorst = worst - mean;
			Vector3 wn = Vector3.Cross(e1, toWorst);
			if (wn.sqrMagnitude > 1e-8f) normals.Add(wn.normalized);
			for (int k = 0; k < 6; k++)
			{
				float ang = k * Mathf.PI / 6f;
				normals.Add(new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)));
			}
			normals.Add(Vector3.up);

			float[] qs = { 0.5f, 0.3f, 0.7f };
			foreach (Vector3 nrm in normals)
			{
				float[] proj = new float[count];
				for (int i = 0; i < count; i++) proj[i] = Vector3.Dot(fc[i], nrm);
				float[] sorted = (float[])proj.Clone();
				System.Array.Sort(sorted);
				foreach (float q in qs)
				{
					float cut = sorted[Mathf.Clamp((int)(q * count), 0, count - 1)];
					List<int> lo = new List<int>(), hi = new List<int>();
					for (int i = 0; i < count; i++) { if (proj[i] < cut) lo.Add(faces[i]); else hi.Add(faces[i]); }
					if (lo.Count == 0 || hi.Count == 0) continue;
					yield return new List<List<int>> { lo, hi };
				}
			}
		}

		private static Vector3 PowerIterate(Matrix4x4 m, Vector3 v)
		{
			if (v.sqrMagnitude < 1e-12f) v = Vector3.up;
			v.Normalize();
			for (int i = 0; i < 50; i++)
			{
				Vector3 w = m.MultiplyVector(v);
				if (w.sqrMagnitude < 1e-20f) break;
				v = w.normalized;
			}
			return v;
		}

		private static List<List<int>> ConnectedParts(List<int> faces, Context ctx)
		{
			Dictionary<Vector3Int, int> firstFace = new Dictionary<Vector3Int, int>();
			int[] parent = new int[faces.Count];
			for (int i = 0; i < parent.Length; i++) parent[i] = i;
			int Find(int x)
			{
				while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
				return x;
			}
			for (int i = 0; i < faces.Count; i++)
				for (int k = 0; k < 3; k++)
				{
					Vector3 p = ctx.Verts[ctx.Tris[faces[i] * 3 + k]];
					Vector3Int q = new Vector3Int(Mathf.RoundToInt(p.x / Settings.WeldEpsilon), Mathf.RoundToInt(p.y / Settings.WeldEpsilon), Mathf.RoundToInt(p.z / Settings.WeldEpsilon));
					int other;
					if (firstFace.TryGetValue(q, out other)) { int ra = Find(i), rb = Find(other); if (ra != rb) parent[ra] = rb; }
					else firstFace[q] = i;
				}
			Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();
			for (int i = 0; i < faces.Count; i++)
			{
				int r = Find(i);
				if (!groups.ContainsKey(r)) groups[r] = new List<int>();
				groups[r].Add(faces[i]);
			}
			return new List<List<int>>(groups.Values);
		}

		private static float PieceHull(string name, LimbSegmenter.Piece piece, Context ctx, out Mesh mesh)
		{
			return piece.AsFace ? FaceHullVolume(name, piece.Faces, piece.FaceThickness, ctx, out mesh) : HullVolume(name, piece.Faces, ctx, out mesh);
		}

		// Technie's "Face" hull (Hull.GenerateFace): every painted triangle plus a copy pushed
		// `thickness` mesh units along Cross(p2-p0, p1-p0) - inward on this project's meshes -
		// cooked by PhysX as one convex MeshCollider. So its collider is the convex hull of
		// those points; this builds that hull the same way for measuring and preview.
		private static float FaceHullVolume(string name, List<int> faces, float thickness, Context ctx, out Mesh mesh)
		{
			mesh = null;
			Mesh pts = FacePointsMesh(faces, thickness, ctx.Verts, ctx.Tris);
			Mesh hull = null;
			try { hull = QHullUtil.FindConvexHull(name, pts, false); }
			catch { hull = null; }
			Object.DestroyImmediate(pts);
			if (hull == null || hull.triangles.Length < 12) { if (hull != null) Object.DestroyImmediate(hull); return 0f; }
			mesh = hull;
			Vector3[] hv = hull.vertices;
			int[] hi = hull.triangles;
			float v = 0f;
			for (int i = 0; i < hi.Length; i += 3)
				v += Vector3.Dot(hv[hi[i]], Vector3.Cross(hv[hi[i + 1]], hv[hi[i + 2]]));
			return Mathf.Abs(v / 6f) * Mathf.Abs(ctx.Scale.x * ctx.Scale.y * ctx.Scale.z);
		}

		private static Mesh FacePointsMesh(List<int> faces, float thickness, Vector3[] verts, int[] tris)
		{
			Vector3[] p = new Vector3[faces.Count * 6];
			for (int i = 0; i < faces.Count; i++)
			{
				int f = faces[i];
				Vector3 p0 = verts[tris[f * 3]], p1 = verts[tris[f * 3 + 1]], p2 = verts[tris[f * 3 + 2]];
				Vector3 normal = Vector3.Cross((p2 - p0).normalized, (p1 - p0).normalized);
				p[i * 6] = p0; p[i * 6 + 1] = p1; p[i * 6 + 2] = p2;
				p[i * 6 + 3] = p0 + normal * thickness; p[i * 6 + 4] = p1 + normal * thickness; p[i * 6 + 5] = p2 + normal * thickness;
			}
			int[] idx = new int[p.Length];
			for (int i = 0; i < idx.Length; i++) idx[i] = i;
			Mesh m = new Mesh();
			m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			m.vertices = p;
			m.triangles = idx;
			return m;
		}

		// Volume of the exact convex hull Technie will build (same QHull call), in metres^3.
		private static float HullVolume(string name, List<int> faces, Context ctx, out Mesh mesh)
		{
			mesh = null;
			Vector3[] hv; int[] hi;
			try { QHullUtil.FindConvexHull(name, faces.ToArray(), ctx.Verts, ctx.Tris, out hv, out hi, false); }
			catch { return 0f; }
			if (hv == null || hi == null || hi.Length < 12) return 0f;

			mesh = new Mesh();
			mesh.vertices = hv;
			mesh.triangles = hi;
			float v = 0f;
			for (int i = 0; i < hi.Length; i += 3)
				v += Vector3.Dot(hv[hi[i]], Vector3.Cross(hv[hi[i + 1]], hv[hi[i + 2]]));
			return Mathf.Abs(v / 6f) * Mathf.Abs(ctx.Scale.x * ctx.Scale.y * ctx.Scale.z);
		}

		// Same steps as RigidColliderCreatorWindow.GenerateCollidersRoutine (convex hulls only),
		// but synchronous. The window's own GenerateColliders() silently does nothing when its
		// target hasn't caught up with Selection yet, which is how an earlier run ended with
		// zero colliders while logging "complete".
		private static void GenerateAll(Context ctx)
		{
			Undo.RegisterCompleteObjectUndo(ctx.Creator.gameObject, "Generate tree colliders");
			ctx.Creator.RemoveAllGenerated();

			foreach (Hull hull in ctx.Paint.hulls)
				hull.GenerateCollisionMesh(ctx.Verts, ctx.Tris, null);

			string hullAssetPath = AssetDatabase.GetAssetPath(ctx.Creator.hullData);
			List<Mesh> existing = new List<Mesh>();
			foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(hullAssetPath))
				if (o is Mesh) existing.Add((Mesh)o);
			foreach (Mesh m in existing)
				if (!ctx.Paint.ContainsMesh(m)) Object.DestroyImmediate(m, true);
			foreach (Hull hull in ctx.Paint.hulls)
			{
				Mesh m = ColliderMeshOf(hull);
				if (m != null && !existing.Contains(m) && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m)))
					AssetDatabase.AddObjectToAsset(m, hullAssetPath);
			}

			EditorUtility.SetDirty(ctx.Creator.hullData);
			AssetDatabase.SaveAssets();

			ctx.Creator.CreateColliderComponents(null);
			EditorUtility.SetDirty(ctx.Creator);
			EditorSceneManager.MarkSceneDirty(ctx.Stage.scene);
		}

		private static string VerifyColliders(Context ctx)
		{
			StringBuilder sb = new StringBuilder();
			MeshCollider[] cols = ctx.Visual.GetComponents<MeshCollider>();
			Collider[] all = ctx.Root.GetComponentsInChildren<Collider>(true);
			int ok = 0;
			List<string> problems = new List<string>();
			foreach (Hull h in ctx.Paint.hulls)
			{
				int matches = 0;
				Mesh hm = ColliderMeshOf(h);
				foreach (MeshCollider c in cols) if (c.sharedMesh != null && c.sharedMesh == hm) matches++;
				if (h.hasColliderError || hm == null || hm.vertexCount < 4) problems.Add(h.name + ": hull generation failed");
				else if (matches != 1) problems.Add(h.name + ": " + matches + " colliders use its mesh (expected 1)");
				else ok++;
			}
			foreach (MeshCollider c in cols)
			{
				if (!c.convex) problems.Add("collider using '" + (c.sharedMesh != null ? c.sharedMesh.name : "null") + "' is not convex");
				if (!ctx.Paint.ContainsMesh(c.sharedMesh)) problems.Add("stale collider with mesh '" + (c.sharedMesh != null ? c.sharedMesh.name : "null") + "' belongs to no hull");
			}
			sb.AppendLine("Hulls: " + ctx.Paint.hulls.Count + " | colliders on Visual: " + cols.Length + " | all colliders under root: " + all.Length + " | hulls with exactly one matching collider: " + ok);
			if (problems.Count == 0) sb.AppendLine("VERIFY OK");
			else foreach (string p in problems) sb.AppendLine("VERIFY PROBLEM: " + p);
			return sb.ToString();
		}

		private static string WriteReport(Context ctx, PlanSet set, string title, string image, string verify, string fileName)
		{
			List<HullPlan> plans = set.Plans;
			StringBuilder sb = new StringBuilder();
			sb.AppendLine(ctx.Root.name + " - " + title);
			foreach (string note in set.PaintNotes) sb.AppendLine("PAINT: " + note);
			sb.AppendLine("Settings: straightness " + Settings.StraightnessTolerance + " radius, max length " + Settings.MaxLengthPerDiameter + " diameters, min length " + Settings.MinLengthPerDiameter + " diameters, arm persistence " + Settings.PersistenceEdges + " edges");
			int totalPieces = 0, flagged = 0;
			float before = 0f, after = 0f;

			// Paint check: a triangle painted into two hulls gives two overlapping colliders.
			Dictionary<int, string> owner = new Dictionary<int, string>();
			Dictionary<string, int> overlaps = new Dictionary<string, int>();
			foreach (HullPlan plan in plans)
				foreach (int f in plan.OriginalFaces)
				{
					string other;
					if (owner.TryGetValue(f, out other))
					{
						string key = other + " + " + plan.Source.name;
						overlaps[key] = overlaps.ContainsKey(key) ? overlaps[key] + 1 : 1;
					}
					else owner[f] = plan.Source.name;
				}
			foreach (KeyValuePair<string, int> o in overlaps)
				sb.AppendLine("PAINT OVERLAP: " + o.Key + " share " + o.Value + " triangles");

			foreach (HullPlan plan in plans)
			{
				float sum = 0f;
				foreach (PieceStats p in plan.Pieces) sum += p.Volume;
				before += plan.OriginalVolume;
				after += sum;
				totalPieces += plan.Pieces.Count;

				int forks = 0;
				foreach (LimbSegmenter.ArcInfo a in plan.Seg.Arcs) if (a.Children.Count > 0) forks++;
				sb.AppendLine();
				sb.AppendLine(plan.BaseName + ": " + plan.OriginalFaces.Count + " tris, " + plan.Seg.ComponentCount + " connected part(s), " + plan.Seg.Arcs.Count + " arm(s), " + forks + " fork(s), median edge " + plan.Seg.MedianEdge.ToString("F3") + " m -> " + plan.Pieces.Count + " pieces | single-hull volume " + plan.OriginalVolume.ToString("F3") + " m3 -> pieces " + sum.ToString("F3") + " m3");
				if (plan.Seg.ComponentCount > 1)
					sb.AppendLine("  NOTE: this paint is " + plan.Seg.ComponentCount + " separate islands of mesh - each island is split on its own.");
				foreach (string note in plan.Seg.Notes) sb.AppendLine("  NOTE: " + note);

				for (int i = 0; i < plan.Pieces.Count; i++)
				{
					PieceStats p = plan.Pieces[i];
					if (p.Flagged) flagged++;
					sb.AppendLine("  s" + (i + 1).ToString("00") + "  arm " + p.Piece.Arc + "  " + p.Piece.Start.ToString("F2") + "-" + p.Piece.End.ToString("F2") + " m  " + p.Piece.Faces.Count + " tris" + (p.Piece.AsFace ? " FACE t" + (p.Piece.FaceThickness * (Mathf.Abs(ctx.Scale.x) + Mathf.Abs(ctx.Scale.y) + Mathf.Abs(ctx.Scale.z)) / 3f).ToString("F2") + "m" : "") + "  vol " + p.Volume.ToString("F4") + "  cover " + p.Cover.Max.ToString("F3") + "/" + p.Cover.P95.ToString("F3") + " m" + (p.Flagged ? " @" + p.Cover.WorstPoint.y.ToString("F2") + "m   <-- CHECK" : ""));
				}
			}

			sb.AppendLine();
			sb.AppendLine("TOTAL: " + plans.Count + " painted hulls -> " + totalPieces + " pieces, " + flagged + " flagged (hull stands > " + CoverTolerance + " m outside the tree mesh)");
			sb.Append(WorstCover(plans));
			sb.AppendLine("Collider volume if each paint were one hull: " + before.ToString("F3") + " m3; after split: " + after.ToString("F3") + " m3");
			if (verify != null) { sb.AppendLine(); sb.Append(verify); }
			sb.AppendLine("Image: " + image);

			System.IO.Directory.CreateDirectory(ReportDir);
			string path = System.IO.Path.Combine(ReportDir, fileName);
			System.IO.File.WriteAllText(path, sb.ToString());
			return sb.ToString() + "Report: " + path;
		}

		// The pieces whose hulls stand farthest outside the visible tree - where a shot through
		// visible air would hit. Report numbering (s01 = 1) so PlanFocus can take them directly.
		private static string WorstCover(List<HullPlan> plans)
		{
			List<KeyValuePair<string, PieceStats>> all = new List<KeyValuePair<string, PieceStats>>();
			foreach (HullPlan plan in plans)
				for (int i = 0; i < plan.Pieces.Count; i++)
					all.Add(new KeyValuePair<string, PieceStats>(plan.BaseName + (plan.Pieces.Count > 1 ? " s" + (i + 1).ToString("00") : ""), plan.Pieces[i]));
			all.Sort((x, y) => y.Value.Cover.Max.CompareTo(x.Value.Cover.Max));
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("COVER (hull surface outside the whole tree mesh: max / p95, worst point x,y,z):");
			for (int i = 0; i < Mathf.Min(12, all.Count); i++)
			{
				MeshCoverField.Stats c = all[i].Value.Cover;
				sb.AppendLine("  " + all[i].Key + "  " + c.Max.ToString("F3") + " / " + c.P95.ToString("F3") + " m  at (" + c.WorstPoint.x.ToString("F2") + ", " + c.WorstPoint.y.ToString("F2") + ", " + c.WorstPoint.z.ToString("F2") + ")");
			}
			return sb.ToString();
		}

		private static Color PieceColour(int i)
		{
			float hue = (i * 0.618034f) % 1f;
			return Color.HSVToRGB(hue, 0.75f, 1f);
		}

		// Renders, from three angles, the painted surface (left) next to the planned or generated
		// hulls (right), with the same camera. A hull that is wider than the painted surface next
		// to it, or fills a gap between two arms, is wrapping empty space.
		//
		// Uses its own camera with Camera.scene set to the Prefab Stage's scene: a camera without
		// that renders the main scene instead of the isolated prefab contents (that's why the
		// 2026-09-17 captures kept showing the unedited tree). Only layer 31 is drawn, so leaves,
		// the textured tree and in-context neighbours never get in the way.
		private static string RenderPreview(Context ctx, List<HullPlan> plans, string fileName, List<int> focusPieces = null)
		{
			int tile = plans.Count == 1 ? 640 : 512;
			bool focused = focusPieces != null && focusPieces.Count > 0;
			List<Object> temp = new List<Object>();
			GameObject holder = new GameObject("~TreeColliderPreview");
			temp.Add(holder);
			string path = null;

			try
			{
				holder.transform.SetParent(ctx.Visual, false);
				holder.hideFlags = HideFlags.DontSave;

				Shader lit = Shader.Find("Universal Render Pipeline/Lit");
				if (lit == null) lit = Shader.Find("Standard");

				// Left column: each piece's own painted triangles in its colour. Right column: the
				// hull built from them, same colour. A colour showing up in two separate places on
				// the left means that piece mixes unrelated parts of the tree - which a convex hull
				// on the right would hide, since it always renders as one solid blob.
				List<Renderer> paintRenderers = new List<Renderer>();
				List<Renderer> hullRenderers = new List<Renderer>();
				int colour = 0;
				foreach (HullPlan plan in plans)
					foreach (PieceStats p in plan.Pieces)
					{
						Color c = PieceColour(colour++);
						Mesh paintMesh = FlatMesh(ctx.Verts, ctx.Tris, p.Piece.Faces);
						temp.Add(paintMesh);
						paintRenderers.Add(AddPreviewObject(holder, "paint", paintMesh, c, lit, temp));
						if (p.HullMesh == null) continue;
						Mesh flat = FlatMesh(p.HullMesh.vertices, p.HullMesh.triangles, null);
						temp.Add(flat);
						hullRenderers.Add(AddPreviewObject(holder, "hull", flat, c, lit, temp));
					}
				if (paintRenderers.Count == 0) return null;

				// Third column: the real tree in grey and, in red, only the collider surface that
				// stands more than CoverTolerance outside it - where a shot through visible air
				// would hit. Hull surface in air is outside the grey mesh by definition, so it shows.
				List<Vector3> red = new List<Vector3>();
				foreach (HullPlan plan in plans)
					foreach (PieceStats p in plan.Pieces)
					{
						if (p.HullMesh == null) continue;
						Vector3[] hv = p.HullMesh.vertices;
						for (int i = 0; i < hv.Length; i++) hv[i] = Vector3.Scale(hv[i], ctx.Scale);
						ctx.Field.CollectOutside(hv, p.HullMesh.triangles, 0.03f, CoverGroundY, CoverTolerance, red);
					}
				Vector3 inv = new Vector3(1f / ctx.Scale.x, 1f / ctx.Scale.y, 1f / ctx.Scale.z);
				for (int i = 0; i < red.Count; i++) red[i] = Vector3.Scale(red[i], inv);
				int[] redIdx = new int[red.Count];
				for (int i = 0; i < redIdx.Length; i++) redIdx[i] = i;
				Mesh redMesh = FlatMesh(red.ToArray(), redIdx, null);
				temp.Add(redMesh);
				Renderer redRenderer = AddPreviewObject(holder, "standoff", redMesh, new Color(1f, 0.05f, 0.05f), lit, temp);
				// Only the painted wood is drawn grey (the leaf cards would bury everything); the red
				// is still measured against the whole mesh.
				List<int> wood = new List<int>();
				foreach (HullPlan plan in plans)
					foreach (PieceStats p in plan.Pieces) wood.AddRange(p.Piece.Faces);
				Mesh treeMesh = FlatMesh(ctx.Verts, ctx.Tris, wood);
				temp.Add(treeMesh);
				Renderer treeRenderer = AddPreviewObject(holder, "tree", treeMesh, new Color(0.62f, 0.6f, 0.56f), lit, temp);

				// Camera and light stay unparented: the Visual has a non-uniform scale, and a camera
				// under it would render a skewed image.
				GameObject lightGo = new GameObject("~PreviewLight");
				lightGo.hideFlags = HideFlags.DontSave;
				temp.Add(lightGo);
				UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo, ctx.Stage.scene);
				Light light = lightGo.AddComponent<Light>();
				light.type = LightType.Directional;
				light.intensity = 1.2f;
				light.cullingMask = 1 << PreviewLayer;

				GameObject camGo = new GameObject("~PreviewCamera");
				camGo.hideFlags = HideFlags.DontSave;
				temp.Add(camGo);
				UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, ctx.Stage.scene);
				Camera cam = camGo.AddComponent<Camera>();
				cam.enabled = false;
				cam.scene = ctx.Stage.scene;
				cam.cullingMask = 1 << PreviewLayer;
				cam.clearFlags = CameraClearFlags.SolidColor;
				cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
				cam.orthographic = true;

				Bounds b = paintRenderers[0].bounds;
				foreach (Renderer r in paintRenderers) b.Encapsulate(r.bounds);
				float radius = Mathf.Max(b.extents.magnitude, 0.05f);
				// Last view is straight down: roots and fused bases only show their bridging from
				// above (Carlos found Deadtree06's root plates that way; the side views hid them).
				Vector3[] viewDirs = { new Vector3(0f, -0.15f, 1f), new Vector3(-1f, -0.15f, 0f), new Vector3(0.7f, -0.9f, -0.7f), Vector3.down };
				float frame = radius * 1.02f;
				float topCut = float.MaxValue;
				Bounds fb = new Bounds();
				if (focused)
				{
					bool first = true;
					foreach (int i in focusPieces)
					{
						if (i < 0 || i >= paintRenderers.Count) continue;
						if (first) { fb = paintRenderers[i].bounds; first = false; }
						else fb.Encapsulate(paintRenderers[i].bounds);
					}
					if (!first)
					{
						b.center = fb.center;
						frame = Mathf.Max(fb.extents.magnitude * 1.4f, 0.1f);
						// Top view of a close-up: hide whatever starts above the focused pieces,
						// or the crown covers the base being checked.
						topCut = fb.max.y + 0.1f;
					}
					viewDirs = new[] { new Vector3(0f, -0.2f, 1f), new Vector3(-1f, -0.2f, 0f), new Vector3(0f, -0.2f, -1f), new Vector3(1f, -0.2f, 0f), Vector3.down };
				}

				RenderTexture rt = new RenderTexture(tile, tile, 24, RenderTextureFormat.ARGB32);
				rt.antiAliasing = 4;
				temp.Add(rt);
				cam.targetTexture = rt;
				// In a close-up's top view the grey tree and red patches above the focus are hidden
				// too (single meshes, so they're filtered per triangle instead of per renderer).
				Renderer treeTop = treeRenderer, redTop = redRenderer;
				if (topCut < float.MaxValue)
				{
					Mesh tc = CutMesh(treeMesh.vertices, treeMesh.triangles, ctx.Visual, topCut);
					Mesh rc = CutMesh(red.ToArray(), redIdx, ctx.Visual, topCut);
					temp.Add(tc); temp.Add(rc);
					treeTop = AddPreviewObject(holder, "treeTop", tc, new Color(0.62f, 0.6f, 0.56f), lit, temp);
					redTop = AddPreviewObject(holder, "standoffTop", rc, new Color(1f, 0.05f, 0.05f), lit, temp);
				}
				Texture2D sheet = new Texture2D(tile * 3, tile * viewDirs.Length, TextureFormat.RGB24, false);
				temp.Add(sheet);
				RenderTexture prevActive = RenderTexture.active;

				for (int v = 0; v < viewDirs.Length; v++)
				{
					Vector3 fwd = viewDirs[v].normalized;
					camGo.transform.position = b.center - fwd * radius * 3f;
					bool top = Mathf.Abs(fwd.y) > 0.95f;
					camGo.transform.rotation = Quaternion.LookRotation(fwd, top ? Vector3.forward : Vector3.up);
					cam.orthographicSize = frame;
					cam.nearClipPlane = 0.01f;
					cam.farClipPlane = radius * 6f;
					lightGo.transform.rotation = Quaternion.LookRotation(fwd + new Vector3(0.3f, -0.6f, 0.2f), Vector3.up);

					for (int col = 0; col < 3; col++)
					{
						float cut = top ? topCut : float.MaxValue;
						foreach (Renderer r in paintRenderers) r.enabled = col == 0 && r.bounds.min.y < cut;
						foreach (Renderer r in hullRenderers) r.enabled = col == 1 && r.bounds.min.y < cut;
						bool cutTop = top && topCut < float.MaxValue;
						treeRenderer.enabled = col == 2 && !cutTop;
						redRenderer.enabled = col == 2 && !cutTop;
						treeTop.enabled = col == 2 && (cutTop || treeTop == treeRenderer);
						redTop.enabled = col == 2 && (cutTop || redTop == redRenderer);
						cam.Render();
						RenderTexture.active = rt;
						sheet.ReadPixels(new Rect(0, 0, tile, tile), col * tile, (viewDirs.Length - 1 - v) * tile);
					}
				}
				sheet.Apply();
				RenderTexture.active = prevActive;
				cam.targetTexture = null;

				System.IO.Directory.CreateDirectory(ReportDir);
				path = System.IO.Path.Combine(ReportDir, fileName);
				System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
			}
			finally
			{
				for (int i = temp.Count - 1; i >= 0; i--)
					if (temp[i] != null) Object.DestroyImmediate(temp[i]);
			}
			return path;
		}

		private static Renderer AddPreviewObject(GameObject holder, string name, Mesh mesh, Color colour, Shader shader, List<Object> temp)
		{
			GameObject go = new GameObject("~" + name);
			go.layer = PreviewLayer;
			go.transform.SetParent(holder.transform, false);
			go.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer r = go.AddComponent<MeshRenderer>();
			Material m = new Material(shader);
			if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
			if (m.HasProperty("_Color")) m.SetColor("_Color", colour);
			if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
			temp.Add(m);
			r.sharedMaterial = m;
			r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			return r;
		}

		// Triangles of a Visual-local mesh whose lowest corner is below world height `cut`.
		private static Mesh CutMesh(Vector3[] verts, int[] tris, Transform visual, float cut)
		{
			List<int> keep = new List<int>();
			for (int t = 0; t < tris.Length / 3; t++)
			{
				float lo = float.MaxValue;
				for (int k = 0; k < 3; k++) lo = Mathf.Min(lo, visual.TransformPoint(verts[tris[t * 3 + k]]).y);
				if (lo < cut) keep.Add(t);
			}
			return FlatMesh(verts, tris, keep);
		}

		// Unshared vertices per triangle so lighting shows every facet.
		private static Mesh FlatMesh(Vector3[] verts, int[] tris, List<int> faces)
		{
			int count = faces != null ? faces.Count : tris.Length / 3;
			Vector3[] v = new Vector3[count * 3];
			int[] t = new int[count * 3];
			for (int i = 0; i < count; i++)
			{
				int f = faces != null ? faces[i] : i;
				for (int k = 0; k < 3; k++) { v[i * 3 + k] = verts[tris[f * 3 + k]]; t[i * 3 + k] = i * 3 + k; }
			}
			Mesh m = new Mesh();
			m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			m.vertices = v;
			m.triangles = t;
			m.RecalculateNormals();
			m.RecalculateBounds();
			return m;
		}

		private static void DestroyMeshes(List<HullPlan> plans)
		{
			foreach (HullPlan plan in plans)
				foreach (PieceStats p in plan.Pieces)
					if (p.HullMesh != null) Object.DestroyImmediate(p.HullMesh);
		}
	}
}
