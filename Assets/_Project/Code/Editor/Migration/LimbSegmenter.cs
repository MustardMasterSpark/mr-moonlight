using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MrMoonlight.EditorTools.Migration
{
	// Splits one painted limb selection (trunk, log or branch - a tube that may fork) into
	// pieces whose convex hulls hug the surface. Pure geometry: no Technie or editor state.
	//
	// Pipeline (see Docs/technie-vegetation-collider-process.md for the why of each step):
	//   1. Weld vertices by position (UV/normal seams duplicate vertices).
	//   2. Split the selection into connected components; each is processed on its own.
	//   3. Geodesic distance from the selection's widest open end (the limb's base ring).
	//   4. Join tree of that distance field: tips are maxima, forks are where superlevel sets
	//      merge. Every fork arm becomes its own arc, so no piece ever spans a fork crotch.
	//   5. Each arc is cut greedily into pieces that stay nearly straight (centerline sag
	//      under a fraction of the local radius) and no longer than a few diameters.
	//   6. Too-small or flat pieces merge into a piece they actually touch.
	public static class LimbSegmenter
	{
		public class Settings
		{
			public float WeldEpsilon = 1e-4f;
			public float StraightnessTolerance = 0.2f;
			public float MaxLengthPerDiameter = 2.0f;
			public float MinLengthPerDiameter = 0.5f;
			// A protrusion becomes its own arm once it reaches this many typical mesh edges past
			// the point where it joins. Measured in edges, not trunk radii: roots, stubs and fork
			// arms are thinner than the trunk they leave, so a radius-based threshold folded
			// every root back into the trunk and one hull wrapped all of them.
			// 1 edge: stubs and broken-off nubs get their own small piece instead of making the
			// trunk ring they sit on bulge (Carlos's call, 2026-09-17).
			public float PersistenceEdges = 1f;
			// Pieces touching a fork are capped at this many diameters: the limb is fused and
			// peanut-shaped there, and a longer hull would bridge the crotch.
			public float JunctionPieceDiameters = 0.75f;
			// How far below a fork (in parent diameters) vertices may be handed to the child arm.
			public float ForkRegionDiameters = 2f;
			// Near a fork, vertices within the carrying-on limb's radius times (1 + this) stay with it.
			public float TubeRadiusTolerance = 0.15f;
			public int MinTrisPerPiece = 6;
		}

		public class Piece
		{
			public int Component;
			public int Arc;
			public int IndexInArc;
			public float Start;
			public float End;
			public List<int> Faces = new List<int>();
		}

		public class ArcInfo
		{
			public int Component;
			public int Parent = -1;
			public List<int> Children = new List<int>();
			public float Base;
			public float Peak;
			public int NodeCount;
			public int PieceCount;
		}

		public class Result
		{
			public List<Piece> Pieces = new List<Piece>();
			public List<ArcInfo> Arcs = new List<ArcInfo>();
			public List<string> Notes = new List<string>();
			public Dictionary<int, float> FaceDistance = new Dictionary<int, float>();
			public int ComponentCount;
			public float MedianEdge;
		}

		public static Result Segment(Vector3[] localVerts, int[] tris, IList<int> faces, Vector3 metricScale, Vector3 localBaseHint, Settings s)
		{
			Result result = new Result();

			// 1. Weld.
			Dictionary<Vector3Int, int> posToNode = new Dictionary<Vector3Int, int>();
			Dictionary<int, int> vertToNode = new Dictionary<int, int>();
			List<Vector3> nodePos = new List<Vector3>();
			int NodeOf(int vi)
			{
				int n;
				if (vertToNode.TryGetValue(vi, out n)) return n;
				Vector3 p = localVerts[vi];
				Vector3Int q = new Vector3Int(Mathf.RoundToInt(p.x / s.WeldEpsilon), Mathf.RoundToInt(p.y / s.WeldEpsilon), Mathf.RoundToInt(p.z / s.WeldEpsilon));
				if (!posToNode.TryGetValue(q, out n))
				{
					n = nodePos.Count;
					posToNode[q] = n;
					nodePos.Add(Vector3.Scale(p, metricScale));
				}
				vertToNode[vi] = n;
				return n;
			}

			int faceCount = faces.Count;
			int[] fn = new int[faceCount * 3];
			for (int i = 0; i < faceCount; i++)
			{
				int t = faces[i];
				fn[i * 3] = NodeOf(tris[t * 3]);
				fn[i * 3 + 1] = NodeOf(tris[t * 3 + 1]);
				fn[i * 3 + 2] = NodeOf(tris[t * 3 + 2]);
			}
			int nodeCount = nodePos.Count;

			Dictionary<long, int> edgeUse = new Dictionary<long, int>();
			List<List<KeyValuePair<int, float>>> adj = new List<List<KeyValuePair<int, float>>>(nodeCount);
			for (int n = 0; n < nodeCount; n++) adj.Add(new List<KeyValuePair<int, float>>());
			List<float> edgeLengths = new List<float>();
			for (int i = 0; i < faceCount; i++)
			{
				for (int e = 0; e < 3; e++)
				{
					int a = fn[i * 3 + e], b = fn[i * 3 + (e + 1) % 3];
					if (a == b) continue;
					long key = EdgeKey(a, b);
					int uses;
					if (edgeUse.TryGetValue(key, out uses)) { edgeUse[key] = uses + 1; continue; }
					edgeUse[key] = 1;
					float w = Vector3.Distance(nodePos[a], nodePos[b]);
					adj[a].Add(new KeyValuePair<int, float>(b, w));
					adj[b].Add(new KeyValuePair<int, float>(a, w));
					edgeLengths.Add(w);
				}
			}
			edgeLengths.Sort();
			float medianEdge = edgeLengths.Count > 0 ? edgeLengths[edgeLengths.Count / 2] : 0.01f;
			result.MedianEdge = medianEdge;

			// 2. Components.
			int[] comp = new int[nodeCount];
			for (int n = 0; n < nodeCount; n++) comp[n] = -1;
			int compCount = 0;
			for (int n = 0; n < nodeCount; n++)
			{
				if (comp[n] != -1) continue;
				Queue<int> q = new Queue<int>();
				q.Enqueue(n);
				comp[n] = compCount;
				while (q.Count > 0)
				{
					int u = q.Dequeue();
					foreach (KeyValuePair<int, float> e in adj[u])
						if (comp[e.Key] == -1) { comp[e.Key] = compCount; q.Enqueue(e.Key); }
				}
				compCount++;
			}
			result.ComponentCount = compCount;

			float[] dist = new float[nodeCount];
			int[] nodeArc = new int[nodeCount];
			for (int n = 0; n < nodeCount; n++) { dist[n] = float.MaxValue; nodeArc[n] = -1; }

			for (int c = 0; c < compCount; c++)
			{
				List<int> nodes = new List<int>();
				for (int n = 0; n < nodeCount; n++) if (comp[n] == c) nodes.Add(n);

				// 3. Root = widest open boundary loop (the limb's base ring).
				List<int> sources;
				FindRoot(c, comp, fn, faceCount, edgeUse, nodePos, localBaseHint, metricScale, nodes, out sources, result.Notes);

				Dijkstra(sources, adj, dist);
				foreach (int n in nodes)
					if (dist[n] == float.MaxValue) dist[n] = 0f;

				// 4. Join tree with persistence.
				float persistence = s.PersistenceEdges * medianEdge;
				BuildArcs(c, nodes, adj, dist, persistence, nodeArc, result.Arcs);
			}

			// 4a. The join tree only ever merges two arms at a time, so several roots leaving the
			// trunk at once show up as a chain of 2-way forks joined by near-zero-length arms.
			// Fold those into their parent to get one many-way fork.
			CollapseShortJunctionArcs(result.Arcs, nodeCount, nodeArc, s.PersistenceEdges * medianEdge);

			// 4b. Give each fork's fused region to the tube it actually belongs to.
			ReassignForkShoulders(result.Arcs, nodeCount, nodeArc, dist, nodePos, medianEdge, s, result.Notes);

			// 5. Cut each arc into near-straight pieces.
			List<List<float>> arcCuts = new List<List<float>>();
			for (int a = 0; a < result.Arcs.Count; a++)
				arcCuts.Add(CutArc(a, result.Arcs[a], nodeCount, nodeArc, dist, nodePos, medianEdge, s));

			// Assign faces to (arc, piece).
			Dictionary<long, Piece> pieceMap = new Dictionary<long, Piece>();
			for (int i = 0; i < faceCount; i++)
			{
				int n0 = fn[i * 3], n1 = fn[i * 3 + 1], n2 = fn[i * 3 + 2];
				int arc = MajorityArc(nodeArc[n0], nodeArc[n1], nodeArc[n2], dist, n0, n1, n2);
				float d = (dist[n0] + dist[n1] + dist[n2]) / 3f;
				result.FaceDistance[faces[i]] = d;
				List<float> cuts = arcCuts[arc];
				int idx = 0;
				while (idx < cuts.Count - 2 && d >= cuts[idx + 1]) idx++;
				long key = ((long)arc << 20) | (uint)idx;
				Piece p;
				if (!pieceMap.TryGetValue(key, out p))
				{
					p = new Piece { Component = result.Arcs[arc].Component, Arc = arc, IndexInArc = idx, Start = cuts[idx], End = cuts[idx + 1] };
					pieceMap[key] = p;
				}
				p.Faces.Add(i);
			}

			// No connected-parts split here: every piece is one band of one arm, so if its
			// triangles don't touch each other it's a ring cut into slivers by long triangles
			// (this mesh's edges are ~18 cm), and the slivers belong in the same hull. Splitting
			// them made each sliver its own lumpy hull.
			List<Piece> connected = new List<Piece>(pieceMap.Values);

			// 6. Merge tiny/flat pieces into a piece they share a node with.
			MergeSmallPieces(connected, fn, nodePos, s, result.Notes);

			connected.Sort((x, y) =>
			{
				int c0 = x.Component.CompareTo(y.Component);
				if (c0 != 0) return c0;
				int a0 = ArcOrder(result.Arcs, x.Arc).CompareTo(ArcOrder(result.Arcs, y.Arc));
				if (a0 != 0) return a0;
				return x.Start.CompareTo(y.Start);
			});

			// Faces are indices into `faces`; convert back to mesh triangle indices.
			foreach (Piece p in connected)
			{
				for (int i = 0; i < p.Faces.Count; i++) p.Faces[i] = faces[p.Faces[i]];
				result.Arcs[p.Arc].PieceCount++;
			}
			result.Pieces = connected;
			return result;
		}

		private static long EdgeKey(int a, int b)
		{
			int lo = a < b ? a : b, hi = a < b ? b : a;
			return ((long)lo << 32) | (uint)hi;
		}

		private static void FindRoot(int c, int[] comp, int[] fn, int faceCount, Dictionary<long, int> edgeUse, List<Vector3> nodePos, Vector3 localBaseHint, Vector3 metricScale, List<int> nodes, out List<int> sources, List<string> notes)
		{
			// Boundary edges = used by exactly one face of this component.
			Dictionary<int, List<int>> bAdj = new Dictionary<int, List<int>>();
			for (int i = 0; i < faceCount; i++)
			{
				if (comp[fn[i * 3]] != c) continue;
				for (int e = 0; e < 3; e++)
				{
					int a = fn[i * 3 + e], b = fn[i * 3 + (e + 1) % 3];
					if (a == b || edgeUse[EdgeKey(a, b)] != 1) continue;
					if (!bAdj.ContainsKey(a)) bAdj[a] = new List<int>();
					if (!bAdj.ContainsKey(b)) bAdj[b] = new List<int>();
					bAdj[a].Add(b);
					bAdj[b].Add(a);
				}
			}

			// The limb starts where it is nearest the tree's base: the trunk at the ground, a
			// branch where it leaves its parent. If an open ring (the edge of the paint) is there,
			// start from the whole ring so distance bands come out as rings; if the nearest point
			// is not on any open ring (e.g. a trunk with a closed bottom but open sockets where
			// separately painted branches were taken out), start from that point. "Widest ring"
			// was used before and picked a branch socket halfway up the trunk as the base.
			Vector3 hint = Vector3.Scale(localBaseHint, metricScale);
			int nearestNode = nodes[0];
			foreach (int n in nodes)
				if ((nodePos[n] - hint).sqrMagnitude < (nodePos[nearestNode] - hint).sqrMagnitude) nearestNode = n;
			float nearestDist = Vector3.Distance(nodePos[nearestNode], hint);

			List<int> bestLoop = null;
			float bestLoopDist = float.MaxValue;
			HashSet<int> seen = new HashSet<int>();
			foreach (int start in bAdj.Keys)
			{
				if (seen.Contains(start)) continue;
				List<int> loop = new List<int>();
				Stack<int> st = new Stack<int>();
				st.Push(start);
				seen.Add(start);
				float loopDist = float.MaxValue;
				while (st.Count > 0)
				{
					int u = st.Pop();
					loop.Add(u);
					loopDist = Mathf.Min(loopDist, Vector3.Distance(nodePos[u], hint));
					foreach (int v in bAdj[u])
						if (seen.Add(v)) st.Push(v);
				}
				if (loopDist < bestLoopDist) { bestLoopDist = loopDist; bestLoop = loop; }
			}

			if (bestLoop != null && bestLoop.Count >= 3 && bestLoopDist <= nearestDist + 1e-4f)
			{
				sources = bestLoop;
				return;
			}

			sources = new List<int> { nearestNode };
			notes.Add("part " + c + ": no open edge at its base, started from the vertex nearest the tree base");
		}

		private static void Dijkstra(List<int> sources, List<List<KeyValuePair<int, float>>> adj, float[] dist)
		{
			MinHeap heap = new MinHeap();
			foreach (int s in sources) { dist[s] = 0f; heap.Push(s, 0f); }
			while (heap.Count > 0)
			{
				float d;
				int u = heap.Pop(out d);
				if (d > dist[u]) continue;
				foreach (KeyValuePair<int, float> e in adj[u])
				{
					float nd = d + e.Value;
					if (nd < dist[e.Key]) { dist[e.Key] = nd; heap.Push(e.Key, nd); }
				}
			}
		}

		// Sweeps nodes from farthest to nearest. A new component appears at every tip; when two
		// components meet at a node, that node is a fork - but only if both arms reach at least
		// `persistence` beyond it. Shorter arms are bumps, stubs or ragged paint edges and are
		// folded into the arm they join, so they never produce their own piece.
		private static void BuildArcs(int component, List<int> nodes, List<List<KeyValuePair<int, float>>> adj, float[] dist, float persistence, int[] nodeArc, List<ArcInfo> arcs)
		{
			nodes.Sort((x, y) => dist[y].CompareTo(dist[x]));
			Dictionary<int, int> uf = new Dictionary<int, int>();
			Dictionary<int, int> rootArc = new Dictionary<int, int>();
			Dictionary<int, float> rootPeak = new Dictionary<int, float>();
			List<int> arcRedirect = new List<int>();
			int firstArc = arcs.Count;

			int Find(int x)
			{
				while (uf[x] != x) { uf[x] = uf[uf[x]]; x = uf[x]; }
				return x;
			}

			foreach (int u in nodes)
			{
				HashSet<int> roots = new HashSet<int>();
				foreach (KeyValuePair<int, float> e in adj[u])
					if (uf.ContainsKey(e.Key)) roots.Add(Find(e.Key));

				uf[u] = u;
				if (roots.Count == 0)
				{
					arcs.Add(new ArcInfo { Component = component, Peak = dist[u] });
					arcRedirect.Add(arcs.Count - 1);
					rootArc[u] = arcs.Count - 1;
					rootPeak[u] = dist[u];
					nodeArc[u] = arcs.Count - 1;
					continue;
				}

				List<int> significant = new List<int>();
				int strongest = -1;
				foreach (int r in roots)
				{
					if (strongest == -1 || rootPeak[r] > rootPeak[strongest]) strongest = r;
					if (rootPeak[r] - dist[u] >= persistence) significant.Add(r);
				}

				int arcForU;
				if (significant.Count >= 2)
				{
					arcs.Add(new ArcInfo { Component = component, Peak = dist[u] });
					arcRedirect.Add(arcs.Count - 1);
					arcForU = arcs.Count - 1;
					foreach (int r in roots)
					{
						int child = Resolve(arcRedirect, rootArc[r], firstArc);
						if (significant.Contains(r))
						{
							arcs[child].Base = dist[u];
							arcs[child].Parent = arcForU;
							arcs[arcForU].Children.Add(child);
						}
						else
						{
							arcRedirect[child - firstArc] = arcForU;
						}
					}
				}
				else
				{
					arcForU = Resolve(arcRedirect, rootArc[strongest], firstArc);
					foreach (int r in roots)
					{
						if (r == strongest) continue;
						int other = Resolve(arcRedirect, rootArc[r], firstArc);
						if (other != arcForU) arcRedirect[other - firstArc] = arcForU;
					}
				}

				float peak = dist[u];
				foreach (int r in roots) { uf[r] = u; if (rootPeak[r] > peak) peak = rootPeak[r]; }
				rootArc[u] = arcForU;
				rootPeak[u] = peak;
				nodeArc[u] = arcForU;
			}

			foreach (int u in nodes) nodeArc[u] = Resolve(arcRedirect, nodeArc[u], firstArc);

			// Redirected arcs are dead: re-point their children and compact the list.
			Dictionary<int, int> remap = new Dictionary<int, int>();
			List<ArcInfo> live = new List<ArcInfo>();
			for (int a = firstArc; a < arcs.Count; a++)
			{
				if (Resolve(arcRedirect, a, firstArc) != a) continue;
				remap[a] = firstArc + live.Count;
				live.Add(arcs[a]);
			}
			foreach (ArcInfo arc in live)
			{
				if (arc.Parent >= 0) arc.Parent = remap[Resolve(arcRedirect, arc.Parent, firstArc)];
				List<int> kids = new List<int>();
				foreach (int k in arc.Children)
				{
					int rk = Resolve(arcRedirect, k, firstArc);
					if (remap.ContainsKey(rk) && !kids.Contains(remap[rk])) kids.Add(remap[rk]);
				}
				arc.Children = kids;
			}
			arcs.RemoveRange(firstArc, arcs.Count - firstArc);
			arcs.AddRange(live);
			foreach (int u in nodes) nodeArc[u] = remap[nodeArc[u]];
			foreach (int u in nodes) arcs[nodeArc[u]].NodeCount++;

			// The arc holding the source ring starts at distance 0.
			foreach (ArcInfo arc in live) if (arc.Parent < 0) arc.Base = 0f;
		}

		private static void CollapseShortJunctionArcs(List<ArcInfo> arcs, int nodeCount, int[] nodeArc, float minLength)
		{
			bool[] dead = new bool[arcs.Count];
			bool changed = true;
			while (changed)
			{
				changed = false;
				for (int a = 0; a < arcs.Count; a++)
				{
					ArcInfo arc = arcs[a];
					if (dead[a] || arc.Parent < 0 || arc.Children.Count == 0 || arc.Peak - arc.Base >= minLength) continue;
					ArcInfo parent = arcs[arc.Parent];
					parent.Children.Remove(a);
					foreach (int c in arc.Children)
					{
						arcs[c].Parent = arc.Parent;
						parent.Children.Add(c);
					}
					parent.Peak = Mathf.Max(parent.Peak, arc.Peak);
					for (int n = 0; n < nodeCount; n++) if (nodeArc[n] == a) nodeArc[n] = arc.Parent;
					dead[a] = true;
					changed = true;
				}
			}

			int[] remap = new int[arcs.Count];
			List<ArcInfo> live = new List<ArcInfo>();
			for (int a = 0; a < arcs.Count; a++)
			{
				remap[a] = dead[a] ? -1 : live.Count;
				if (!dead[a]) live.Add(arcs[a]);
			}
			foreach (ArcInfo arc in live)
			{
				if (arc.Parent >= 0) arc.Parent = remap[arc.Parent];
				for (int i = 0; i < arc.Children.Count; i++) arc.Children[i] = remap[arc.Children[i]];
				arc.NodeCount = 0;
			}
			for (int n = 0; n < nodeCount; n++)
				if (nodeArc[n] >= 0) { nodeArc[n] = remap[nodeArc[n]]; live[nodeArc[n]].NodeCount++; }
			arcs.Clear();
			arcs.AddRange(live);
		}

		private static int Resolve(List<int> redirect, int arc, int offset)
		{
			while (redirect[arc - offset] != arc) arc = redirect[arc - offset];
			return arc;
		}

		// Greedy walk along one arc: extend the current piece band by band and cut when its
		// centerline stops being straight (a convex hull over a bend fills the inside of the
		// bend with empty space) or when it gets longer than MaxLengthPerDiameter.
		private static List<float> CutArc(int arcIndex, ArcInfo arc, int nodeCount, int[] nodeArc, float[] dist, List<Vector3> nodePos, float medianEdge, Settings s)
		{
			float length = arc.Peak - arc.Base;
			List<float> cuts = new List<float> { arc.Base, arc.Peak + 1e-4f };
			if (length <= 0f) return cuts;

			float bandWidth = Mathf.Max(1.5f * medianEdge, length / 200f);
			int bandCount = Mathf.Max(1, Mathf.CeilToInt(length / bandWidth));
			bandWidth = length / bandCount;

			Vector3[] sum = new Vector3[bandCount];
			int[] count = new int[bandCount];
			for (int n = 0; n < nodeCount; n++)
			{
				if (nodeArc[n] != arcIndex) continue;
				int b = Mathf.Clamp((int)((dist[n] - arc.Base) / bandWidth), 0, bandCount - 1);
				sum[b] += nodePos[n];
				count[b]++;
			}
			Vector3[] centre = new Vector3[bandCount];
			for (int b = 0; b < bandCount; b++) centre[b] = count[b] > 0 ? sum[b] / count[b] : Vector3.zero;
			float[] radius = new float[bandCount];
			for (int n = 0; n < nodeCount; n++)
			{
				if (nodeArc[n] != arcIndex) continue;
				int b = Mathf.Clamp((int)((dist[n] - arc.Base) / bandWidth), 0, bandCount - 1);
				radius[b] += Vector3.Distance(nodePos[n], centre[b]);
			}
			List<int> valid = new List<int>();
			for (int b = 0; b < bandCount; b++)
			{
				if (count[b] == 0) continue;
				radius[b] /= count[b];
				valid.Add(b);
			}
			if (valid.Count < 3) return cuts;

			// Smooth band centres so single noisy bands don't trigger cuts.
			Vector3[] smooth = new Vector3[valid.Count];
			for (int i = 0; i < valid.Count; i++)
			{
				Vector3 acc = Vector3.zero; int k = 0;
				for (int j = Mathf.Max(0, i - 1); j <= Mathf.Min(valid.Count - 1, i + 1); j++) { acc += centre[valid[j]]; k++; }
				smooth[i] = acc / k;
			}

			cuts = new List<float> { arc.Base };
			int startIdx = 0;
			for (int end = 1; end < valid.Count; end++)
			{
				float segStart = arc.Base + valid[startIdx] * bandWidth;
				float segEnd = arc.Base + (valid[end] + 1) * bandWidth;
				float segLength = segEnd - segStart;

				float rSum = 0f;
				for (int i = startIdx; i <= end; i++) rSum += radius[valid[i]];
				float meanR = Mathf.Max(rSum / (end - startIdx + 1), 1e-5f);

				if (segLength < s.MinLengthPerDiameter * 2f * meanR) continue;

				float sag = 0f;
				Vector3 a = smooth[startIdx], b = smooth[end];
				for (int i = startIdx + 1; i < end; i++) sag = Mathf.Max(sag, DistanceToSegment(smooth[i], a, b));

				bool tooBent = sag > s.StraightnessTolerance * meanR;
				bool tooLong = segLength > s.MaxLengthPerDiameter * 2f * meanR;
				if (tooBent || tooLong)
				{
					// [startIdx .. end-1] still passed, so the cut goes where band `end` begins.
					cuts.Add(arc.Base + valid[end] * bandWidth);
					startIdx = end;
				}
			}
			cuts.Add(arc.Peak + 1e-4f);

			// Just below a fork the limb's cross-section is two arms fused together, and any hull
			// over it fills the crotch between them. Keep the piece that ends at the fork short so
			// that wedge stays small. (Above the fork each arm is its own arc, so no cap needed.)
			if (arc.Children.Count > 0)
			{
				List<float> radii = new List<float>();
				foreach (int b in valid) radii.Add(radius[b]);
				radii.Sort();
				float junction = s.JunctionPieceDiameters * 2f * radii[radii.Count / 2];
				float forkCut = arc.Peak - junction;
				if (forkCut - arc.Base > 0.5f * junction)
				{
					cuts.RemoveAll(c => c > forkCut - 0.25f * junction && c < arc.Peak);
					cuts.Insert(cuts.Count - 1, forkCut);
					cuts.Sort();
				}
			}
			return cuts;
		}

		private class Axis
		{
			public List<Vector3> Points = new List<Vector3>();
			public List<float> Dist = new List<float>();
			public List<float> Radius = new List<float>();
			public float MedianRadius;
		}

		// Band centroids along one arc, base to tip.
		private static Axis BuildAxis(int arcIndex, ArcInfo arc, int nodeCount, int[] nodeArc, float[] dist, List<Vector3> nodePos, float medianEdge)
		{
			Axis axis = new Axis();
			float length = Mathf.Max(arc.Peak - arc.Base, 1e-4f);
			float bandWidth = Mathf.Max(1.5f * medianEdge, length / 60f);
			int bandCount = Mathf.Max(1, Mathf.CeilToInt(length / bandWidth));
			bandWidth = length / bandCount;
			Vector3[] sum = new Vector3[bandCount];
			int[] count = new int[bandCount];
			List<int>[] members = new List<int>[bandCount];
			for (int n = 0; n < nodeCount; n++)
			{
				if (nodeArc[n] != arcIndex) continue;
				int b = Mathf.Clamp((int)((dist[n] - arc.Base) / bandWidth), 0, bandCount - 1);
				sum[b] += nodePos[n];
				count[b]++;
				if (members[b] == null) members[b] = new List<int>();
				members[b].Add(n);
			}
			List<float> radii = new List<float>();
			for (int b = 0; b < bandCount; b++)
			{
				if (count[b] == 0) continue;
				Vector3 c = sum[b] / count[b];
				float r = 0f;
				foreach (int n in members[b]) r += Vector3.Distance(nodePos[n], c);
				r /= count[b];
				axis.Points.Add(c);
				axis.Dist.Add(arc.Base + (b + 0.5f) * bandWidth);
				axis.Radius.Add(r);
				radii.Add(r);
			}
			radii.Sort();
			axis.MedianRadius = radii.Count > 0 ? radii[radii.Count / 2] : medianEdge;
			return axis;
		}

		// Below a fork the parent and a side branch are one fused cross-section. Slicing by
		// distance from the base hands the side branch's shoulder to the parent's last piece, and
		// that piece's hull then fills the crotch above the branch - the wedge Carlos kept
		// circling. Fix: model the limb that carries on through the fork as a tube (axis through
		// the fork + its radius measured away from the fork). Any parent vertex near the fork that
		// sticks out of that tube, and is closer to a side branch's axis than to the tube's axis,
		// belongs to that side branch. The parent's last piece is then a plain tube, and the side
		// branch's first piece reaches down to the parent's surface.
		//
		// (An earlier version compared "distance to axis / radius" per arm. Shoulder vertices sit
		// on the parent's surface, so against a thin branch's radius they always looked far, and
		// thin branches never got their shoulders back - its log showed "took 0" at every fork.)
		private static void ReassignForkShoulders(List<ArcInfo> arcs, int nodeCount, int[] nodeArc, float[] dist, List<Vector3> nodePos, float medianEdge, Settings s, List<string> notes)
		{
			List<Axis> axes = new List<Axis>();
			for (int a = 0; a < arcs.Count; a++) axes.Add(BuildAxis(a, arcs[a], nodeCount, nodeArc, dist, nodePos, medianEdge));
			float endBand = 1.5f * medianEdge;

			for (int p = 0; p < arcs.Count; p++)
			{
				ArcInfo parent = arcs[p];
				if (parent.Children.Count == 0) continue;

				// Every child gets an axis from its start to its far end, however short it is.
				// The carrying-on limb is the child that is thickest where it leaves the fork (not
				// on average - a long arm with a thin tip averages thin).
				int count = parent.Children.Count;
				Vector3[] cStart = new Vector3[count], cEnd = new Vector3[count];
				bool[] valid = new bool[count];
				float[] entryRadius = new float[count];
				int cont = -1;
				for (int k = 0; k < count; k++)
				{
					ArcInfo child = arcs[parent.Children[k]];
					float span = Mathf.Max(endBand, 0.25f * (child.Peak - child.Base));
					valid[k] = Centroid(parent.Children[k], child.Base - 1f, child.Base + span, nodeCount, nodeArc, dist, nodePos, out cStart[k])
						&& Centroid(parent.Children[k], child.Peak - span, child.Peak + 1f, nodeCount, nodeArc, dist, nodePos, out cEnd[k])
						&& (cEnd[k] - cStart[k]).sqrMagnitude > 1e-8f;
					entryRadius[k] = EntryRadius(axes[parent.Children[k]], child.Base, Mathf.Max(3f * medianEdge, 0.3f * (child.Peak - child.Base)));
					if (valid[k] && (cont == -1 || entryRadius[k] > entryRadius[cont])) cont = k;
				}
				if (cont == -1) continue;
				int contArc = parent.Children[cont];
				ArcInfo contInfo = arcs[contArc];

				// The thickest child is the limb carrying on. Its tube: axis from the parent's
				// last clean stretch to the child's first clean stretch.
				float dP = 2f * axes[p].MedianRadius;
				float dC = 2f * axes[contArc].MedianRadius;
				Vector3 a0, b0;
				if (!Centroid(p, parent.Peak - 2f * dP, parent.Peak - dP, nodeCount, nodeArc, dist, nodePos, out a0))
					Centroid(p, parent.Base - 1f, parent.Base + Mathf.Max(endBand, 0.25f * (parent.Peak - parent.Base)), nodeCount, nodeArc, dist, nodePos, out a0);
				if (!Centroid(contArc, contInfo.Base + dC, contInfo.Base + 2f * dC, nodeCount, nodeArc, dist, nodePos, out b0))
					b0 = cEnd[cont];
				if ((b0 - a0).sqrMagnitude < 1e-8f) continue;
				Vector3 tDir = (b0 - a0).normalized;
				Vector3 tA = a0 - tDir * dP, tB = b0 + tDir * dC;

				// Tube radius = how far the carrying-on limb's own surface actually reaches from the
				// axis in its clean stretches (95th percentile, not the mean: fluted bark puts
				// ridges well outside the mean radius, and those ridges must stay with the trunk).
				List<float> reach = new List<float>();
				for (int n = 0; n < nodeCount; n++)
				{
					bool parentClean = nodeArc[n] == p && dist[n] >= parent.Peak - 2f * dP && dist[n] <= parent.Peak - dP;
					bool contClean = nodeArc[n] == contArc && dist[n] >= contInfo.Base + dC && dist[n] <= contInfo.Base + 2f * dC;
					if (parentClean || contClean) reach.Add(DistanceToSegment(nodePos[n], tA, tB));
				}
				reach.Sort();
				float tubeRadius = reach.Count >= 4 ? reach[Mathf.Min(reach.Count - 1, (int)(reach.Count * 0.95f))] : Mathf.Max(axes[p].MedianRadius, axes[contArc].MedianRadius);
				float keep = tubeRadius * (1f + s.TubeRadiusTolerance);

				// Side axes run back into the tube so they reach the fused region.
				Vector3[] sA = new Vector3[count];
				for (int k = 0; k < count; k++)
					if (valid[k]) sA[k] = cStart[k] - (cEnd[k] - cStart[k]).normalized * (tubeRadius + dP);

				float window = s.ForkRegionDiameters * dP;
				int[] taken = new int[count];
				int inReach = 0;
				for (int n = 0; n < nodeCount; n++)
				{
					if (nodeArc[n] != p || dist[n] < parent.Peak - window) continue;
					inReach++;
					float dt = DistanceToSegment(nodePos[n], tA, tB);
					if (dt <= keep) continue;
					int bestK = -1;
					float best = dt;
					for (int k = 0; k < count; k++)
					{
						if (k == cont || !valid[k]) continue;
						float d = DistanceToSegment(nodePos[n], sA[k], cEnd[k]);
						if (d < best) { best = d; bestK = k; }
					}
					if (bestK >= 0) { nodeArc[n] = parent.Children[bestK]; taken[bestK]++; }
				}

				StringBuilder sb = new StringBuilder();
				sb.Append("fork at " + parent.Peak.ToString("F2") + " m: arm " + p + " continues as arm " + contArc + " (tube r " + tubeRadius.ToString("F2") + ", " + inReach + " verts in reach)");
				for (int k = 0; k < count; k++)
					if (k != cont) sb.Append("; side arm " + parent.Children[k] + (valid[k] ? " took " + taken[k] : " has no usable axis"));
				notes.Add(sb.ToString());
			}
		}

		private static float EntryRadius(Axis axis, float from, float length)
		{
			List<float> r = new List<float>();
			for (int i = 0; i < axis.Dist.Count; i++)
				if (axis.Dist[i] <= from + length) r.Add(axis.Radius[i]);
			if (r.Count == 0) return axis.MedianRadius;
			r.Sort();
			return r[r.Count / 2];
		}

		private static bool Centroid(int arc, float from, float to, int nodeCount, int[] nodeArc, float[] dist, List<Vector3> nodePos, out Vector3 centroid)
		{
			Vector3 sum = Vector3.zero;
			int n = 0;
			for (int i = 0; i < nodeCount; i++)
				if (nodeArc[i] == arc && dist[i] >= from && dist[i] <= to) { sum += nodePos[i]; n++; }
			centroid = n > 0 ? sum / n : Vector3.zero;
			return n > 0;
		}

		private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
		{
			Vector3 ab = b - a;
			float len2 = ab.sqrMagnitude;
			if (len2 < 1e-12f) return Vector3.Distance(p, a);
			float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
			return Vector3.Distance(p, a + ab * t);
		}

		private static int MajorityArc(int a0, int a1, int a2, float[] dist, int n0, int n1, int n2)
		{
			if (a0 == a1 || a0 == a2) return a0;
			if (a1 == a2) return a1;
			// All three differ (a face touching a fork point): use its farthest vertex's arc,
			// which is the child arm rather than the trunk below the crotch.
			if (dist[n0] >= dist[n1] && dist[n0] >= dist[n2]) return a0;
			return dist[n1] >= dist[n2] ? a1 : a2;
		}

		private static void MergeSmallPieces(List<Piece> pieces, int[] fn, List<Vector3> nodePos, Settings s, List<string> notes)
		{
			bool changed = true;
			while (changed && pieces.Count > 1)
			{
				changed = false;
				for (int i = 0; i < pieces.Count; i++)
				{
					Piece p = pieces[i];
					if (p.Faces.Count >= s.MinTrisPerPiece && !IsFlat(p.Faces, fn, nodePos)) continue;

					HashSet<int> myNodes = NodesOf(p.Faces, fn);
					int best = -1;
					int bestScore = int.MinValue;
					for (int j = 0; j < pieces.Count; j++)
					{
						if (j == i) continue;
						int shared = 0;
						foreach (int n in NodesOf(pieces[j].Faces, fn)) if (myNodes.Contains(n)) shared++;
						if (shared == 0) continue;
						// Prefer the same arc, then the most shared border.
						int score = shared + (pieces[j].Arc == p.Arc ? 100000 : 0);
						if (score > bestScore) { bestScore = score; best = j; }
					}
					if (best == -1) continue;

					pieces[best].Faces.AddRange(p.Faces);
					pieces[best].Start = Mathf.Min(pieces[best].Start, p.Start);
					pieces[best].End = Mathf.Max(pieces[best].End, p.End);
					pieces.RemoveAt(i);
					changed = true;
					break;
				}
			}

			foreach (Piece p in pieces)
				if (p.Faces.Count < s.MinTrisPerPiece || IsFlat(p.Faces, fn, nodePos))
					notes.Add("isolated tiny/flat piece kept (" + p.Faces.Count + " tris) - nothing touching it to merge into");
		}

		private static HashSet<int> NodesOf(List<int> pieceFaces, int[] fn)
		{
			HashSet<int> set = new HashSet<int>();
			foreach (int f in pieceFaces) { set.Add(fn[f * 3]); set.Add(fn[f * 3 + 1]); set.Add(fn[f * 3 + 2]); }
			return set;
		}

		// Flat = every node lies within a thin slab, so a convex hull would have ~zero volume.
		private static bool IsFlat(List<int> pieceFaces, int[] fn, List<Vector3> nodePos)
		{
			HashSet<int> ns = NodesOf(pieceFaces, fn);
			if (ns.Count < 4) return true;
			Vector3 mean = Vector3.zero;
			foreach (int n in ns) mean += nodePos[n];
			mean /= ns.Count;
			float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
			foreach (int n in ns)
			{
				Vector3 d = nodePos[n] - mean;
				xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z; yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
			}
			float det = xx * (yy * zz - yz * yz) - xy * (xy * zz - yz * xz) + xz * (xy * yz - yy * xz);
			float trace = xx + yy + zz;
			if (trace < 1e-12f) return true;
			// det/trace^3 is ~0 when one principal axis has no spread.
			return det / (trace * trace * trace) < 1e-5f;
		}

		private static int ArcOrder(List<ArcInfo> arcs, int arc)
		{
			int depth = 0;
			int a = arc;
			while (arcs[a].Parent >= 0) { a = arcs[a].Parent; depth++; }
			return depth * 100000 + arc;
		}

		private class MinHeap
		{
			private readonly List<int> items = new List<int>();
			private readonly List<float> keys = new List<float>();
			public int Count { get { return items.Count; } }

			public void Push(int item, float key)
			{
				items.Add(item); keys.Add(key);
				int i = items.Count - 1;
				while (i > 0)
				{
					int p = (i - 1) / 2;
					if (keys[p] <= keys[i]) break;
					Swap(i, p); i = p;
				}
			}

			public int Pop(out float key)
			{
				int top = items[0]; key = keys[0];
				int last = items.Count - 1;
				Swap(0, last);
				items.RemoveAt(last); keys.RemoveAt(last);
				int i = 0;
				while (true)
				{
					int l = i * 2 + 1, r = l + 1, m = i;
					if (l < items.Count && keys[l] < keys[m]) m = l;
					if (r < items.Count && keys[r] < keys[m]) m = r;
					if (m == i) break;
					Swap(i, m); i = m;
				}
				return top;
			}

			private void Swap(int a, int b)
			{
				int ti = items[a]; items[a] = items[b]; items[b] = ti;
				float tk = keys[a]; keys[a] = keys[b]; keys[b] = tk;
			}
		}
	}
}
