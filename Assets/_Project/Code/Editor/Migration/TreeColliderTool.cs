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

		// "Gap" = how far a hull's faces stand off the painted surface they came from. A hull
		// hugging a limb has small gaps (bark grooves); a hull bridging a crotch, the space
		// between roots or the inside of a bend has a gap comparable to the limb's radius.
		// Flagged when the gap exceeds this fraction of the piece's radius (and GapFloor).
		public static float GapWarningRadii = 0.5f;
		public static float GapFloorMetres = 0.03f;

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
		}

		private class PieceStats
		{
			public LimbSegmenter.Piece Piece;
			public Hull Target;
			public Mesh HullMesh;
			public float Volume;
			public float Gap;
			public float Radius;
			public bool Flagged;
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
					target.type = HullType.ConvexHull;
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
					p.HullMesh = p.Target != null && p.Target.collisionMesh != null ? Object.Instantiate(p.Target.collisionMesh) : null;
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
			Context ctx = OpenContext(prefabName);
			List<HullPlan> hulls = new List<HullPlan>();
			List<PieceStats> all = new List<PieceStats>();
			foreach (Hull h in ctx.Paint.hulls)
			{
				int[] faces = h.GetSelectedFaces();
				if (faces.Length == 0) continue;
				LimbSegmenter.Piece piece = new LimbSegmenter.Piece { Faces = new List<int>(faces) };
				PieceStats st = new PieceStats { Piece = piece, Target = h };
				st.HullMesh = h.collisionMesh != null ? Object.Instantiate(h.collisionMesh) : null;
				st.Gap = MaxGap(st.HullMesh, piece.Faces, ctx);
				HullPlan plan = new HullPlan { Source = h, BaseName = h.name, OriginalFaces = piece.Faces };
				plan.Pieces.Add(st);
				hulls.Add(plan);
				all.Add(st);
			}

			StringBuilder sb = new StringBuilder();
			sb.AppendLine(ctx.Root.name + " - INSPECT (current colliders)");
			sb.Append(VerifyColliders(ctx));
			all.Sort((a, b) => b.Gap.CompareTo(a.Gap));
			sb.AppendLine("Largest gaps (hull face standing off its own painted surface):");
			for (int i = 0; i < Mathf.Min(10, all.Count); i++)
				sb.AppendLine("  " + all[i].Target.name + "  " + all[i].Piece.Faces.Count + " tris  gap " + all[i].Gap.ToString("F3") + " m");
			string image = RenderPreview(ctx, hulls, prefabName + "_inspect.png");
			sb.AppendLine("Image: " + image);
			DestroyMeshes(hulls);

			System.IO.Directory.CreateDirectory(ReportDir);
			string path = System.IO.Path.Combine(ReportDir, prefabName + "_inspect.txt");
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
					PieceStats st = new PieceStats { Piece = piece };
					st.Volume = HullVolume(h.name, piece.Faces, ctx, out st.HullMesh);
					float length = Mathf.Max(piece.End - piece.Start, 0.01f);
					st.Radius = Mathf.Sqrt(st.Volume / (Mathf.PI * length));
					st.Gap = MaxGap(st.HullMesh, piece.Faces, ctx);
					st.Flagged = st.Gap > GapFloorMetres && st.Gap > GapWarningRadii * st.Radius;
					plan.Pieces.Add(st);
				}
				set.Plans.Add(plan);
			}
			return set;
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

		// Largest distance (metres) from the centre of any hull face to the piece's painted
		// surface. The piece's open ends (where it meets the next piece) are capped first with a
		// fan: a hull's end caps sit inside the limb, not in empty space, and would otherwise
		// always measure as a full radius of "gap".
		private static float MaxGap(Mesh hull, List<int> faces, Context ctx)
		{
			if (hull == null) return 0f;
			List<Vector3> a = new List<Vector3>(), b = new List<Vector3>(), c = new List<Vector3>();
			Dictionary<long, int> edgeUse = new Dictionary<long, int>();
			Dictionary<Vector3Int, int> keyIndex = new Dictionary<Vector3Int, int>();
			List<Vector3> keyPos = new List<Vector3>();
			int KeyOf(Vector3 local)
			{
				Vector3Int q = new Vector3Int(Mathf.RoundToInt(local.x / Settings.WeldEpsilon), Mathf.RoundToInt(local.y / Settings.WeldEpsilon), Mathf.RoundToInt(local.z / Settings.WeldEpsilon));
				int k;
				if (!keyIndex.TryGetValue(q, out k)) { k = keyPos.Count; keyIndex[q] = k; keyPos.Add(Vector3.Scale(local, ctx.Scale)); }
				return k;
			}

			foreach (int f in faces)
			{
				int k0 = KeyOf(ctx.Verts[ctx.Tris[f * 3]]), k1 = KeyOf(ctx.Verts[ctx.Tris[f * 3 + 1]]), k2 = KeyOf(ctx.Verts[ctx.Tris[f * 3 + 2]]);
				a.Add(keyPos[k0]); b.Add(keyPos[k1]); c.Add(keyPos[k2]);
				int[] ks = { k0, k1, k2 };
				for (int e = 0; e < 3; e++)
				{
					int p = ks[e], q = ks[(e + 1) % 3];
					if (p == q) continue;
					long key = ((long)Mathf.Min(p, q) << 32) | (uint)Mathf.Max(p, q);
					edgeUse[key] = edgeUse.ContainsKey(key) ? edgeUse[key] + 1 : 1;
				}
			}

			Dictionary<int, List<int>> bAdj = new Dictionary<int, List<int>>();
			foreach (KeyValuePair<long, int> e in edgeUse)
			{
				if (e.Value != 1) continue;
				int p = (int)(e.Key >> 32), q = (int)(e.Key & 0xffffffff);
				if (!bAdj.ContainsKey(p)) bAdj[p] = new List<int>();
				if (!bAdj.ContainsKey(q)) bAdj[q] = new List<int>();
				bAdj[p].Add(q); bAdj[q].Add(p);
			}
			HashSet<int> seen = new HashSet<int>();
			foreach (int start in bAdj.Keys)
			{
				if (!seen.Add(start)) continue;
				List<int> loop = new List<int>();
				Stack<int> st = new Stack<int>();
				st.Push(start);
				while (st.Count > 0)
				{
					int u = st.Pop();
					loop.Add(u);
					foreach (int v in bAdj[u]) if (seen.Add(v)) st.Push(v);
				}
				Vector3 centre = Vector3.zero;
				foreach (int u in loop) centre += keyPos[u];
				centre /= loop.Count;
				foreach (int u in loop)
					foreach (int v in bAdj[u])
						if (u < v) { a.Add(centre); b.Add(keyPos[u]); c.Add(keyPos[v]); }
			}

			Vector3[] hv = hull.vertices;
			int[] hi = hull.triangles;
			float worst = 0f;
			for (int t = 0; t < hi.Length; t += 3)
			{
				Vector3 p = Vector3.Scale((hv[hi[t]] + hv[hi[t + 1]] + hv[hi[t + 2]]) / 3f, ctx.Scale);
				float best = float.MaxValue;
				for (int i = 0; i < a.Count; i++)
				{
					float d = (ClosestPointOnTriangle(p, a[i], b[i], c[i]) - p).sqrMagnitude;
					if (d < best) best = d;
				}
				worst = Mathf.Max(worst, Mathf.Sqrt(best));
			}
			return worst;
		}

		private static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
		{
			Vector3 ab = b - a, ac = c - a, ap = p - a;
			float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
			if (d1 <= 0f && d2 <= 0f) return a;
			Vector3 bp = p - b;
			float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
			if (d3 >= 0f && d4 <= d3) return b;
			float vc = d1 * d4 - d3 * d2;
			if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
			Vector3 cp = p - c;
			float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
			if (d6 >= 0f && d5 <= d6) return c;
			float vb = d5 * d2 - d1 * d6;
			if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
			float va = d3 * d6 - d5 * d4;
			if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
			float denom = 1f / (va + vb + vc);
			return a + ab * (vb * denom) + ac * (vc * denom);
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
				if (hull.collisionMesh != null && !existing.Contains(hull.collisionMesh))
					AssetDatabase.AddObjectToAsset(hull.collisionMesh, hullAssetPath);

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
				foreach (MeshCollider c in cols) if (c.sharedMesh != null && c.sharedMesh == h.collisionMesh) matches++;
				if (h.hasColliderError || h.collisionMesh == null || h.collisionMesh.vertexCount < 4) problems.Add(h.name + ": hull generation failed");
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
					sb.AppendLine("  s" + (i + 1).ToString("00") + "  arm " + p.Piece.Arc + "  " + p.Piece.Start.ToString("F2") + "-" + p.Piece.End.ToString("F2") + " m  " + p.Piece.Faces.Count + " tris  vol " + p.Volume.ToString("F4") + "  radius " + p.Radius.ToString("F3") + "  gap " + p.Gap.ToString("F3") + " m" + (p.Flagged ? "   <-- CHECK" : ""));
				}
			}

			sb.AppendLine();
			sb.AppendLine("TOTAL: " + plans.Count + " painted hulls -> " + totalPieces + " pieces, " + flagged + " flagged (gap > " + GapWarningRadii + " x radius and > " + GapFloorMetres + " m)");
			sb.AppendLine("Collider volume if each paint were one hull: " + before.ToString("F3") + " m3; after split: " + after.ToString("F3") + " m3");
			if (verify != null) { sb.AppendLine(); sb.Append(verify); }
			sb.AppendLine("Image: " + image);

			System.IO.Directory.CreateDirectory(ReportDir);
			string path = System.IO.Path.Combine(ReportDir, fileName);
			System.IO.File.WriteAllText(path, sb.ToString());
			return sb.ToString() + "Report: " + path;
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
				Vector3[] viewDirs = { new Vector3(0f, -0.15f, 1f), new Vector3(-1f, -0.15f, 0f), new Vector3(0.7f, -0.9f, -0.7f) };
				float frame = radius * 1.02f;
				if (focused)
				{
					Bounds fb = new Bounds();
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
					}
					viewDirs = new[] { new Vector3(0f, -0.2f, 1f), new Vector3(-1f, -0.2f, 0f), new Vector3(0f, -0.2f, -1f), new Vector3(1f, -0.2f, 0f) };
				}

				RenderTexture rt = new RenderTexture(tile, tile, 24, RenderTextureFormat.ARGB32);
				rt.antiAliasing = 4;
				temp.Add(rt);
				cam.targetTexture = rt;
				Texture2D sheet = new Texture2D(tile * 2, tile * viewDirs.Length, TextureFormat.RGB24, false);
				temp.Add(sheet);
				RenderTexture prevActive = RenderTexture.active;

				for (int v = 0; v < viewDirs.Length; v++)
				{
					Vector3 fwd = viewDirs[v].normalized;
					camGo.transform.position = b.center - fwd * radius * 3f;
					camGo.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
					cam.orthographicSize = frame;
					cam.nearClipPlane = 0.01f;
					cam.farClipPlane = radius * 6f;
					lightGo.transform.rotation = Quaternion.LookRotation(fwd + new Vector3(0.3f, -0.6f, 0.2f), Vector3.up);

					for (int col = 0; col < 2; col++)
					{
						foreach (Renderer r in paintRenderers) r.enabled = col == 0;
						foreach (Renderer r in hullRenderers) r.enabled = col == 1;
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
