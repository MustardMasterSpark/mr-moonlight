using System.Collections.Generic;
using UnityEngine;

namespace MrMoonlight.EditorTools.Migration
{
	// Cover metric for tree colliders (MRM-84, 2026-09-18). Carlos's bar: an enemy hides behind a
	// tree, the player shoots where the bark visibly isn't, and the shot must not hit an invisible
	// collider. So what matters is how far a collider's surface stands OUTSIDE the visible mesh -
	// measured against the whole tree, not just the piece's own paint (the old "gap" column
	// couldn't see a plate spanning root to root, because each root is some other piece's paint).
	//
	// Signed distance to the nearest triangle of the whole mesh, sign from the nearest faces'
	// normals (bark normals point out). Points inside the wood (a hull's end cap inside the trunk)
	// are fine and read 0. A uniform grid over the triangles keeps queries cheap.
	public class MeshCoverField
	{
		private readonly Vector3[] a, b, c, n;
		private readonly List<int>[] cells;
		private readonly Vector3 origin;
		private readonly float cell;
		private readonly int nx, ny, nz;
		private readonly int[] stamp;
		private int stampId;

		// Positions in metres (the Visual's scale already applied).
		public MeshCoverField(Vector3[] metricVerts, int[] tris, bool flipNormals)
		{
			int t = tris.Length / 3;
			a = new Vector3[t]; b = new Vector3[t]; c = new Vector3[t]; n = new Vector3[t];
			Bounds bounds = new Bounds(metricVerts[tris[0]], Vector3.zero);
			for (int i = 0; i < t; i++)
			{
				a[i] = metricVerts[tris[i * 3]]; b[i] = metricVerts[tris[i * 3 + 1]]; c[i] = metricVerts[tris[i * 3 + 2]];
				Vector3 nn = Vector3.Cross(b[i] - a[i], c[i] - a[i]);
				n[i] = (flipNormals ? -nn : nn).normalized;
				bounds.Encapsulate(a[i]); bounds.Encapsulate(b[i]); bounds.Encapsulate(c[i]);
			}
			bounds.Expand(1f);
			float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
			cell = Mathf.Clamp(longest / 64f, 0.04f, 0.5f);
			origin = bounds.min;
			nx = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / cell));
			ny = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / cell));
			nz = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / cell));
			cells = new List<int>[nx * ny * nz];
			stamp = new int[t];
			for (int i = 0; i < t; i++)
			{
				Vector3 lo = Vector3.Min(a[i], Vector3.Min(b[i], c[i])), hi = Vector3.Max(a[i], Vector3.Max(b[i], c[i]));
				Vector3Int l = CellOf(lo), h = CellOf(hi);
				for (int x = l.x; x <= h.x; x++)
					for (int y = l.y; y <= h.y; y++)
						for (int z = l.z; z <= h.z; z++)
						{
							int k = (x * ny + y) * nz + z;
							if (cells[k] == null) cells[k] = new List<int>();
							cells[k].Add(i);
						}
			}
		}

		private Vector3Int CellOf(Vector3 p)
		{
			Vector3 q = (p - origin) / cell;
			return new Vector3Int(Mathf.Clamp((int)q.x, 0, nx - 1), Mathf.Clamp((int)q.y, 0, ny - 1), Mathf.Clamp((int)q.z, 0, nz - 1));
		}

		// How far p stands outside the visible surface, in metres (0 if on or inside it).
		public float Outside(Vector3 p)
		{
			Vector3Int pc = CellOf(p);
			stampId++;
			float best = float.MaxValue;
			List<int> near = new List<int>();
			List<Vector3> nearPt = new List<Vector3>();
			int maxK = Mathf.Max(nx, Mathf.Max(ny, nz));
			for (int k = 0; k <= maxK; k++)
			{
				for (int x = pc.x - k; x <= pc.x + k; x++)
				{
					if (x < 0 || x >= nx) continue;
					for (int y = pc.y - k; y <= pc.y + k; y++)
					{
						if (y < 0 || y >= ny) continue;
						for (int z = pc.z - k; z <= pc.z + k; z++)
						{
							if (z < 0 || z >= nz) continue;
							if (Mathf.Max(Mathf.Abs(x - pc.x), Mathf.Max(Mathf.Abs(y - pc.y), Mathf.Abs(z - pc.z))) != k) continue;
							List<int> list = cells[(x * ny + y) * nz + z];
							if (list == null) continue;
							foreach (int i in list)
							{
								if (stamp[i] == stampId) continue;
								stamp[i] = stampId;
								Vector3 q = ClosestPointOnTriangle(p, a[i], b[i], c[i]);
								float d = (q - p).magnitude;
								if (d < best - 1e-4f) { best = d; near.Clear(); nearPt.Clear(); }
								if (d <= best + 1e-4f) { near.Add(i); nearPt.Add(q); }
							}
						}
					}
				}
				// Every triangle not visited yet lies at least k cells away.
				if (best <= k * cell) break;
			}
			// Under 2 cm reads as 0: well inside tolerance either way, and the inside test below
			// is the expensive part.
			if (near.Count == 0 || best < 0.02f) return 0f;
			Vector3Int key = new Vector3Int(Mathf.RoundToInt(p.x * 100f), Mathf.RoundToInt(p.y * 100f), Mathf.RoundToInt(p.z * 100f));
			bool cachedInside;
			if (insideCache.TryGetValue(key, out cachedInside)) return cachedInside ? 0f : best;
			// Inside or outside: the nearest face's normal is NOT reliable here - these trees are
			// several overlapping shells (branches pushed into the trunk), so a point deep in the
			// trunk can be nearest to a buried branch's outer surface (DeadTree01, 2026-09-18:
			// 0.5 m "outside" readings in the middle of the trunk). The winding number counts
			// every shell and tolerates holes: |w| >= 0.5 means inside some piece of wood.
			bool inside = Mathf.Abs(Winding(p)) >= 0.5f;
			insideCache[key] = inside;
			return inside ? 0f : best;
		}

		private readonly Dictionary<Vector3Int, bool> insideCache = new Dictionary<Vector3Int, bool>();

		// Open air vs bark roughness (2026-09-18). A hull bridging a bark groove or a knot stands
		// off the surface too, but nobody sees a groove as "air" - requiring 5 cm everywhere cut
		// Deadtree06 into 1,278 colliders. What Carlos means by a shot "where the bark visibly
		// isn't" is a gap a ball of OpenAirRadius fits into: between roots, inside a crotch.
		// A point counts only if some ball of that radius holds it without touching wood
		// (morphological closing of the tree by that ball). 0 = count every point.
		public float OpenAirRadius = 0f;

		private static Vector3[] ballDirs;

		private bool InOpenAir(Vector3 p, float here)
		{
			float r = OpenAirRadius;
			if (r <= 0f || here >= r) return true;
			if (ballDirs == null)
			{
				List<Vector3> dirs = new List<Vector3>();
				for (int x = -1; x <= 1; x++)
					for (int y = -1; y <= 1; y++)
						for (int z = -1; z <= 1; z++)
							if (x != 0 || y != 0 || z != 0) dirs.Add(new Vector3(x, y, z).normalized);
				ballDirs = dirs.ToArray();
			}
			// A ball of radius r holding p has its centre within r of p, and needs clearance r.
			foreach (Vector3 d in ballDirs)
			{
				Vector3 c = p + d * (r - here);
				if (RawOutside(c) >= r - 0.005f) return true;
			}
			return false;
		}

		// Stand-off that counts for cover: Outside(p), but 0 where no open-air ball fits.
		public float Cover(Vector3 p)
		{
			float d = Outside(p);
			if (d <= 0f || OpenAirRadius <= 0f) return d;
			return InOpenAir(p, d) ? d : 0f;
		}

		private float RawOutside(Vector3 p)
		{
			return Outside(p);
		}

		// The parts of a hull surface that stand outside the mesh by more than `tolerance`, as
		// small triangles (metric coordinates) - drawn red in the preview's third column.
		public void CollectOutside(Vector3[] metricHullVerts, int[] hullTris, float spacing, float groundY, float tolerance, List<Vector3> outTris)
		{
			for (int t = 0; t < hullTris.Length; t += 3)
			{
				Vector3 p0 = metricHullVerts[hullTris[t]], p1 = metricHullVerts[hullTris[t + 1]], p2 = metricHullVerts[hullTris[t + 2]];
				float longest = Mathf.Max((p1 - p0).magnitude, Mathf.Max((p2 - p1).magnitude, (p0 - p2).magnitude));
				int L = Mathf.Clamp(Mathf.CeilToInt(longest / spacing), 1, 40);
				Vector3 e1 = (p1 - p0) / L, e2 = (p2 - p0) / L;
				for (int i = 0; i < L; i++)
					for (int j = 0; j < L - i; j++)
					{
						Vector3 q0 = p0 + e1 * i + e2 * j;
						// Up-pointing sub-triangle, then the down-pointing one next to it.
						Emit(q0, q0 + e1, q0 + e2, groundY, tolerance, outTris);
						if (j < L - i - 1) Emit(q0 + e1, q0 + e1 + e2, q0 + e2, groundY, tolerance, outTris);
					}
			}
		}

		private void Emit(Vector3 a0, Vector3 a1, Vector3 a2, float groundY, float tolerance, List<Vector3> outTris)
		{
			Vector3 c0 = (a0 + a1 + a2) / 3f;
			if (c0.y <= groundY || Cover(c0) <= tolerance) return;
			outTris.Add(a0); outTris.Add(a1); outTris.Add(a2);
		}

		// Fast winding number (Barill et al. 2018, first order) over an octree of triangles: a node
		// far from p (distance > WindingBeta x its radius) counts as one dipole - its area-weighted
		// normal at its area centroid; near leaves are summed exactly. Only the sign decision
		// |w| >= 0.5 is used, which is robust to the dipole approximation. (A flat 12^3 cluster grid
		// was used first: on Deadtree06's 5k triangles every query summed thousands of triangles
		// exactly and a plan froze the Editor for over ten minutes, 2026-09-18.)
		private const float WindingBeta = 2f;
		private const int LeafTris = 12;
		private readonly List<Vector3> nodeCentre = new List<Vector3>(), nodeNormal = new List<Vector3>();
		private readonly List<float> nodeRadius = new List<float>();
		private readonly List<int[]> nodeChildren = new List<int[]>();
		private readonly List<int[]> nodeTris = new List<int[]>();
		private int rootNode = -1;

		private int BuildNode(List<int> tris, int depth)
		{
			Vector3 cen = Vector3.zero, nrm = Vector3.zero;
			float area = 0f;
			foreach (int i in tris)
			{
				Vector3 cr = Vector3.Cross(b[i] - a[i], c[i] - a[i]);
				float ar = 0.5f * cr.magnitude;
				cen += (a[i] + b[i] + c[i]) / 3f * ar;
				area += ar;
				nrm += 0.5f * cr;
			}
			cen = area > 1e-12f ? cen / area : (a[tris[0]] + b[tris[0]] + c[tris[0]]) / 3f;
			float rad = 0f;
			foreach (int i in tris) rad = Mathf.Max(rad, Mathf.Max((a[i] - cen).magnitude, Mathf.Max((b[i] - cen).magnitude, (c[i] - cen).magnitude)));

			int id = nodeCentre.Count;
			nodeCentre.Add(cen); nodeNormal.Add(nrm); nodeRadius.Add(rad); nodeChildren.Add(null); nodeTris.Add(null);
			if (tris.Count <= LeafTris || depth >= 16)
			{
				nodeTris[id] = tris.ToArray();
				return id;
			}
			// Split into octants around the triangles' centroid mean.
			Vector3 mid = Vector3.zero;
			foreach (int i in tris) mid += (a[i] + b[i] + c[i]) / 3f;
			mid /= tris.Count;
			List<int>[] oct = new List<int>[8];
			foreach (int i in tris)
			{
				Vector3 tc = (a[i] + b[i] + c[i]) / 3f;
				int o = (tc.x > mid.x ? 1 : 0) | (tc.y > mid.y ? 2 : 0) | (tc.z > mid.z ? 4 : 0);
				if (oct[o] == null) oct[o] = new List<int>();
				oct[o].Add(i);
			}
			int filled = 0;
			foreach (List<int> l in oct) if (l != null) filled++;
			if (filled <= 1)
			{
				nodeTris[id] = tris.ToArray();
				return id;
			}
			List<int> kids = new List<int>();
			foreach (List<int> l in oct) if (l != null) kids.Add(BuildNode(l, depth + 1));
			nodeChildren[id] = kids.ToArray();
			return id;
		}

		public float Winding(Vector3 p)
		{
			if (rootNode < 0)
			{
				List<int> all = new List<int>(a.Length);
				for (int i = 0; i < a.Length; i++) all.Add(i);
				rootNode = BuildNode(all, 0);
			}
			double w = 0.0;
			Stack<int> stack = new Stack<int>();
			stack.Push(rootNode);
			while (stack.Count > 0)
			{
				int k = stack.Pop();
				Vector3 d = nodeCentre[k] - p;
				float dist = d.magnitude;
				if (dist > WindingBeta * nodeRadius[k])
				{
					w += Vector3.Dot(nodeNormal[k], d) / (4.0 * System.Math.PI * dist * dist * dist);
					continue;
				}
				if (nodeTris[k] != null)
				{
					foreach (int i in nodeTris[k]) w += SolidAngle(a[i] - p, b[i] - p, c[i] - p) / (4.0 * System.Math.PI);
					continue;
				}
				foreach (int ch in nodeChildren[k]) stack.Push(ch);
			}
			return (float)w;
		}

		private static double SolidAngle(Vector3 va, Vector3 vb, Vector3 vc)
		{
			double la = va.magnitude, lb = vb.magnitude, lc = vc.magnitude;
			double num = Vector3.Dot(va, Vector3.Cross(vb, vc));
			double den = la * lb * lc + Vector3.Dot(va, vb) * lc + Vector3.Dot(va, vc) * lb + Vector3.Dot(vb, vc) * la;
			return 2.0 * System.Math.Atan2(num, den);
		}

		public static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
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

		public struct Stats
		{
			public float Max;
			public float P95;
			public Vector3 WorstPoint;
			public int Samples;
		}

		// Samples a hull's surface every `spacing` metres and measures each point against the
		// field. Points at or below `groundY` are skipped: the terrain hides them.
		// `bail`: stop as soon as any point stands off more than this (the split search only needs
		// to know a candidate lost). A bailed result has Max > bail and a meaningless P95.
		public Stats Measure(Vector3[] metricHullVerts, int[] hullTris, float spacing, float groundY, float bail = float.MaxValue)
		{
			Stats st = new Stats();
			List<float> ds = new List<float>();
			// Face centres first: a bridging face stands off most in its middle, so a losing
			// candidate usually bails within a handful of queries.
			if (bail < float.MaxValue)
				for (int t = 0; t < hullTris.Length; t += 3)
				{
					Vector3 cc = (metricHullVerts[hullTris[t]] + metricHullVerts[hullTris[t + 1]] + metricHullVerts[hullTris[t + 2]]) / 3f;
					if (cc.y <= groundY) continue;
					float d = Cover(cc);
					if (d > st.Max) { st.Max = d; st.WorstPoint = cc; }
					if (d > bail) { st.Samples = 1; return st; }
				}
			for (int t = 0; t < hullTris.Length; t += 3)
			{
				Vector3 p0 = metricHullVerts[hullTris[t]], p1 = metricHullVerts[hullTris[t + 1]], p2 = metricHullVerts[hullTris[t + 2]];
				float longest = Mathf.Max((p1 - p0).magnitude, Mathf.Max((p2 - p1).magnitude, (p0 - p2).magnitude));
				int L = Mathf.Clamp(Mathf.CeilToInt(longest / spacing), 1, 40);
				for (int i = 0; i <= L; i++)
					for (int j = 0; j <= L - i; j++)
					{
						float u = (float)i / L, v = (float)j / L;
						Vector3 p = p0 + (p1 - p0) * u + (p2 - p0) * v;
						if (p.y <= groundY) continue;
						float d = Cover(p);
						ds.Add(d);
						if (d > st.Max) { st.Max = d; st.WorstPoint = p; }
						if (d > bail) { st.Samples = ds.Count; return st; }
					}
			}
			st.Samples = ds.Count;
			if (ds.Count > 0)
			{
				ds.Sort();
				st.P95 = ds[Mathf.Min(ds.Count - 1, (int)(ds.Count * 0.95f))];
			}
			return st;
		}
	}
}
