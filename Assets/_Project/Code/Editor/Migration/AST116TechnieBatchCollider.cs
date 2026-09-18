using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Technie.PhysicsCreator;
using Technie.PhysicsCreator.Rigid;

namespace MrMoonlight.EditorTools.Migration
{
	// One-off evaluation tool for MRM-84 (AST-116, Technie Collider Creator 2).
	// Removes existing colliders and generates new ones on the named root objects in the active
	// scene, then applies each instance back onto its source prefab. Only touches the
	// AST116_ColliderTest copies, never the live vegetation prefabs.
	public static class AST116TechnieBatchCollider
	{
		private const string TargetFolder = "Assets/_Project/Art/VegetationPrefabs/AST116_ColliderTest";
		private const string HullDataFolder = TargetFolder + "/Physics Hulls";

		public static bool IsRunning = false;
		public static bool CancelRequested = false;
		public static int Processed = 0;
		public static int Total = 0;
		public static List<string> Log = new List<string>();

		public static void RunOnNames(string[] names, AutoHullPreset preset, HullType hullType)
		{
			List<GameObject> targets = FindTargets(names);
			EnsureHullDataFolder();

			Total = targets.Count;
			Processed = 0;
			IsRunning = true;
			CancelRequested = false;
			Log.Clear();
			Log.Add("Matched " + targets.Count + " of " + names.Length + " requested names, preset=" + preset + ", hullType=" + hullType);

			EditorCoroutines.Execute(BatchRoutine(targets.ToArray(), preset, hullType, null));
		}

		// Same as RunOnNames but with hand-picked VHACD parameters (AutoHullPreset.Custom).
		public static void RunCustomOnNames(string[] names, float concavity, uint resolution, float minVolumePerCH, uint maxConvexHulls)
		{
			List<GameObject> targets = FindTargets(names);
			EnsureHullDataFolder();

			VhacdParameters customParams = new VhacdParameters();
			customParams.concavity = concavity;
			customParams.resolution = resolution;
			customParams.minVolumePerCH = minVolumePerCH;
			customParams.maxConvexHulls = maxConvexHulls;

			Total = targets.Count;
			Processed = 0;
			IsRunning = true;
			CancelRequested = false;
			Log.Clear();
			Log.Add("Matched " + targets.Count + " of " + names.Length + " requested names, CUSTOM concavity=" + concavity + " resolution=" + resolution + " minVolumePerCH=" + minVolumePerCH + " maxConvexHulls=" + maxConvexHulls);

			EditorCoroutines.Execute(BatchRoutine(targets.ToArray(), AutoHullPreset.Custom, HullType.Auto, customParams));
		}

		// Splits every currently-painted hull with a selection on the object open in the current
		// Prefab Stage along the mesh's own real ring seams - the actual boundaries between one
		// extruded cylinder segment and the next, found by clustering vertex positions along the
		// hull's long axis - not an even/approximate split. Each real segment becomes its own
		// Convex Hull, named "<originalName>_<n>". Then generates colliders for the result. Does
		// not touch the scene at all - operates purely on whatever prefab is open in Prefab Mode,
		// so the scene-path guard used elsewhere doesn't apply here. `onlyHullNames`: if non-null,
		// only hulls whose exact name is in this list are split - everything else is left alone.
		// Use this on a re-run so already-split hulls ("log_1_2" etc.) don't get re-processed and
		// fragmented further; pass null to process every hull with a selection (first run).
		public static void SplitPaintedHullsAtRingSeams(string expectedRootName, string[] onlyHullNames = null)
		{
			PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (stage == null)
				throw new System.InvalidOperationException("Not in a Prefab Stage - refusing to run.");

			GameObject root = stage.prefabContentsRoot;
			if (root.name != expectedRootName)
				throw new System.InvalidOperationException("Prefab Stage root is '" + root.name + "', expected '" + expectedRootName + "' - refusing to run.");

			Transform visual = root.transform.Find("Visual");
			if (visual == null)
				throw new System.InvalidOperationException("No 'Visual' child found under " + root.name);

			RigidColliderCreator creator = visual.GetComponent<RigidColliderCreator>();
			if (creator == null || creator.paintingData == null)
				throw new System.InvalidOperationException("No paintingData on " + root.name + " - paint at least one hull first.");

			Mesh mesh = visual.GetComponent<MeshFilter>().sharedMesh;
			Vector3[] verts = mesh.vertices;
			int[] tris = mesh.triangles;
			PaintingData paintingData = creator.paintingData;

			HashSet<string> nameFilter = onlyHullNames != null ? new HashSet<string>(onlyHullNames) : null;
			List<Hull> snapshot = new List<Hull>();
			foreach (Hull h in paintingData.hulls)
			{
				if (nameFilter == null || nameFilter.Contains(h.Name))
					snapshot.Add(h);
			}
			Log.Clear();

			foreach (Hull h in snapshot)
			{
				int[] faces = h.GetSelectedFaces();
				if (faces.Length == 0)
				{
					Log.Add(h.Name + ": no selected triangles, skipped");
					continue;
				}

				// Long axis of this hull's selection, via power iteration on the triangle
				// centroids' covariance matrix.
				Vector3[] centroids = new Vector3[faces.Length];
				for (int i = 0; i < faces.Length; i++)
				{
					int t = faces[i];
					centroids[i] = (verts[tris[t * 3]] + verts[tris[t * 3 + 1]] + verts[tris[t * 3 + 2]]) / 3f;
				}

				Vector3 mean = Vector3.zero;
				foreach (Vector3 c in centroids) mean += c;
				mean /= centroids.Length;

				float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
				foreach (Vector3 c in centroids)
				{
					Vector3 d = c - mean;
					xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
					yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
				}

				Vector3 axis = new Vector3(1, 1, 1).normalized;
				for (int iter = 0; iter < 50; iter++)
				{
					Vector3 next = new Vector3(
						xx * axis.x + xy * axis.y + xz * axis.z,
						xy * axis.x + yy * axis.y + yz * axis.z,
						xz * axis.x + yz * axis.y + zz * axis.z
					);
					if (next.sqrMagnitude < 1e-12f) break;
					axis = next.normalized;
				}

				// Project every vertex USED by this selection onto the axis - this is the actual
				// mesh geometry, not an approximation.
				HashSet<int> usedVerts = new HashSet<int>();
				foreach (int t in faces)
				{
					usedVerts.Add(tris[t * 3]);
					usedVerts.Add(tris[t * 3 + 1]);
					usedVerts.Add(tris[t * 3 + 2]);
				}

				Dictionary<int, float> vertProj = new Dictionary<int, float>();
				float minP = float.MaxValue, maxP = float.MinValue;
				foreach (int vi in usedVerts)
				{
					float p = Vector3.Dot(verts[vi] - mean, axis);
					vertProj[vi] = p;
					if (p < minP) minP = p;
					if (p > maxP) maxP = p;
				}

				float totalRange = maxP - minP;
				if (totalRange < 1e-6f)
				{
					Log.Add(h.Name + ": degenerate axis (too flat/small), left as one piece");
					continue;
				}

				// Cluster the projected vertex positions into distinct ring levels. Rather than a
				// fixed noise multiplier (which failed on organic geometry - natural bumps looked
				// too much like real seams), find the biggest RELATIVE jump in gap sizes: real
				// seams should be dramatically bigger than the noise between vertices on the same
				// ring, so sort gaps descending and find where the ratio to the next-smaller gap
				// peaks. Only gaps at or above that "elbow" count as real seams.
				List<float> sortedVals = new List<float>(vertProj.Values);
				sortedVals.Sort();

				List<float> gaps = new List<float>();
				for (int i = 1; i < sortedVals.Count; i++)
					gaps.Add(sortedVals[i] - sortedVals[i - 1]);

				float gapThreshold = float.MaxValue; // default: no internal seams found
				if (gaps.Count > 0)
				{
					List<float> gapsDesc = new List<float>(gaps);
					gapsDesc.Sort();
					gapsDesc.Reverse();

					int elbowCount = 0;
					float bestRatio = 1f;
					for (int i = 0; i < gapsDesc.Count - 1; i++)
					{
						if (gapsDesc[i + 1] <= 1e-9f) continue;
						float ratio = gapsDesc[i] / gapsDesc[i + 1];
						if (ratio > bestRatio)
						{
							bestRatio = ratio;
							elbowCount = i + 1;
						}
					}

					// Require a confident jump (top gaps at least ~2.5x bigger than the noise tier
					// right below them) and a tiny absolute floor to reject pure floating-point
					// noise. NOTE: this used to also require the smallest "real" gap be >=2% of
					// the WHOLE selection's span - that assumed few, large seams (true for a log
					// welded from a handful of separate mesh pieces, e.g. AP_ENV_tree_Nokmyung's
					// 3 segments). It wrongly rejected meshes built as one continuous tube with
					// many evenly-spaced loop cuts along its length (e.g. AP_AlaskaCedar_001_2:
					// 18 real ring gaps, each only ~0.7% of the total span, but a clean 6x jump
					// above the same-ring noise floor) - those got left as one un-split piece
					// that overshoots the taper/curve badly. The ratio check alone is the real
					// noise-vs-signal test; a fixed percent-of-span floor doesn't scale with how
					// many genuine rings a limb has.
					if (elbowCount > 0 && bestRatio >= 2.5f && gapsDesc[elbowCount - 1] >= totalRange * 0.002f)
					{
						gapThreshold = gapsDesc[elbowCount - 1] - 1e-9f;
					}
				}

				List<float> ringLevels = new List<float>();
				ringLevels.Add(sortedVals[0]);
				for (int i = 1; i < sortedVals.Count; i++)
				{
					if (sortedVals[i] - ringLevels[ringLevels.Count - 1] > gapThreshold)
						ringLevels.Add(sortedVals[i]);
				}

				// Assign each vertex to its nearest ring level index.
				Dictionary<int, int> vertRing = new Dictionary<int, int>();
				foreach (int vi in usedVerts)
				{
					float p = vertProj[vi];
					int best = 0;
					float bestDist = float.MaxValue;
					for (int li = 0; li < ringLevels.Count; li++)
					{
						float d = Mathf.Abs(p - ringLevels[li]);
						if (d < bestDist) { bestDist = d; best = li; }
					}
					vertRing[vi] = best;
				}

				int numSegments = Mathf.Max(1, ringLevels.Count - 1);

				// Each triangle spans between two (usually adjacent) ring levels - assign it to
				// the segment starting at the lower of the ring levels touched by its vertices.
				Dictionary<int, List<int>> segmentFaces = new Dictionary<int, List<int>>();
				foreach (int t in faces)
				{
					int r0 = vertRing[tris[t * 3]];
					int r1 = vertRing[tris[t * 3 + 1]];
					int r2 = vertRing[tris[t * 3 + 2]];
					int minRing = Mathf.Min(r0, Mathf.Min(r1, r2));
					int segId = Mathf.Clamp(minRing, 0, numSegments - 1);

					if (!segmentFaces.ContainsKey(segId)) segmentFaces[segId] = new List<int>();
					segmentFaces[segId].Add(t);
				}

				string baseName = h.Name;
				PhysicsMaterial mat = h.material;
				Color colour = h.colour;

				List<int> orderedSegIds = new List<int>(segmentFaces.Keys);
				orderedSegIds.Sort();

				// Safety net: a segment too small to form a sane/valid convex hull gets merged
				// into a neighbor, regardless of what the ring detection decided. Repeat until
				// stable, since merging can leave a newly-small merged segment.
				const int MinTrisPerSegment = 4;
				bool mergedAny = true;
				while (mergedAny && orderedSegIds.Count > 1)
				{
					mergedAny = false;
					for (int i = 0; i < orderedSegIds.Count; i++)
					{
						int segId = orderedSegIds[i];
						if (segmentFaces[segId].Count >= MinTrisPerSegment)
							continue;

						int neighborId = (i + 1 < orderedSegIds.Count) ? orderedSegIds[i + 1] : orderedSegIds[i - 1];
						segmentFaces[neighborId].AddRange(segmentFaces[segId]);
						segmentFaces.Remove(segId);
						orderedSegIds.RemoveAt(i);
						mergedAny = true;
						break;
					}
				}

				bool reusedOriginal = false;
				int segmentsCreated = 0;
				foreach (int segId in orderedSegIds)
				{
					List<int> segTris = segmentFaces[segId];
					if (segTris.Count == 0) continue;

					Hull target;
					if (!reusedOriginal)
					{
						target = h;
						reusedOriginal = true;
					}
					else
					{
						target = paintingData.AddHull(HullType.ConvexHull, mat, false, false);
					}

					target.name = baseName + "_" + (segmentsCreated + 1);
					target.type = HullType.ConvexHull;
					target.colour = colour;
					target.SetSelectedFaces(segTris, mesh);
					segmentsCreated++;
				}

				Log.Add(baseName + ": found " + ringLevels.Count + " ring levels -> " + segmentsCreated + " real segments (" + faces.Length + " tris total)");
			}

			EditorUtility.SetDirty(paintingData);

			RigidColliderCreatorWindow.ShowWindow();
			RigidColliderCreatorWindow rigidWindow = RigidColliderCreatorWindow.instance;
			Selection.activeGameObject = visual.gameObject;
			rigidWindow.GenerateColliders();

			EditorCoroutines.Execute(WaitForGenerateThenLog(rigidWindow));
		}

		// Debug helper: reports the current Prefab Stage's hull/collider state without changing
		// anything. Exists so ad-hoc inspection from outside this assembly (e.g. the MCP
		// execute_code tool, which can't reference the Technie namespace directly) can still see
		// what's painted.
		public static string DescribeCurrentPrefabStage()
		{
			PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (stage == null)
				return "No prefab stage open.";

			GameObject root = stage.prefabContentsRoot;
			Transform visual = root.transform.Find("Visual");
			if (visual == null)
				return "Prefab: " + root.name + " | No 'Visual' child found.";

			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			sb.AppendLine("Prefab: " + root.name + " (" + stage.assetPath + ")");

			RigidColliderCreator creator = visual.GetComponent<RigidColliderCreator>();
			if (creator == null)
			{
				sb.AppendLine("No RigidColliderCreator on Visual.");
			}
			else if (creator.paintingData == null)
			{
				sb.AppendLine("RigidColliderCreator exists but paintingData is null.");
			}
			else
			{
				PaintingData pd = creator.paintingData;
				sb.AppendLine("Hull count: " + pd.hulls.Count);
				foreach (Hull h in pd.hulls)
				{
					sb.AppendLine("  Hull '" + h.Name + "' type=" + h.type + " selectedFaces=" + h.GetSelectedFaces().Length);
				}
			}

			MeshFilter mf = visual.GetComponent<MeshFilter>();
			if (mf != null && mf.sharedMesh != null)
				sb.AppendLine("Mesh tris: " + (mf.sharedMesh.triangles.Length / 3));

			Collider[] existing = visual.GetComponents<Collider>();
			sb.AppendLine("Existing colliders on Visual: " + existing.Length);

			return sb.ToString();
		}

		// Diagnostic: dumps the same PCA-axis projection & gap analysis used by
		// SplitPaintedHullsAtRingSeams for one named hull, without changing anything. Used to
		// understand why a given mesh did/didn't produce real ring seams.
		public static string DumpRingProjection(string expectedRootName, string hullName)
		{
			PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (stage == null) throw new System.InvalidOperationException("Not in a Prefab Stage.");
			GameObject root = stage.prefabContentsRoot;
			if (root.name != expectedRootName) throw new System.InvalidOperationException("Wrong prefab open.");
			Transform visual = root.transform.Find("Visual");
			RigidColliderCreator creator = visual.GetComponent<RigidColliderCreator>();
			Mesh mesh = visual.GetComponent<MeshFilter>().sharedMesh;
			Vector3[] verts = mesh.vertices;
			int[] tris = mesh.triangles;
			PaintingData paintingData = creator.paintingData;

			Hull h = null;
			foreach (Hull cand in paintingData.hulls)
				if (cand.Name == hullName) h = cand;
			if (h == null) return "Hull '" + hullName + "' not found.";

			int[] faces = h.GetSelectedFaces();
			Vector3[] centroids = new Vector3[faces.Length];
			for (int i = 0; i < faces.Length; i++)
			{
				int t = faces[i];
				centroids[i] = (verts[tris[t * 3]] + verts[tris[t * 3 + 1]] + verts[tris[t * 3 + 2]]) / 3f;
			}
			Vector3 mean = Vector3.zero;
			foreach (Vector3 c in centroids) mean += c;
			mean /= centroids.Length;

			float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
			foreach (Vector3 c in centroids)
			{
				Vector3 d = c - mean;
				xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
				yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
			}
			Vector3 axis = new Vector3(1, 1, 1).normalized;
			for (int iter = 0; iter < 50; iter++)
			{
				Vector3 next = new Vector3(
					xx * axis.x + xy * axis.y + xz * axis.z,
					xy * axis.x + yy * axis.y + yz * axis.z,
					xz * axis.x + yz * axis.y + zz * axis.z
				);
				if (next.sqrMagnitude < 1e-12f) break;
				axis = next.normalized;
			}

			HashSet<int> usedVerts = new HashSet<int>();
			foreach (int t in faces)
			{
				usedVerts.Add(tris[t * 3]);
				usedVerts.Add(tris[t * 3 + 1]);
				usedVerts.Add(tris[t * 3 + 2]);
			}

			List<float> projList = new List<float>();
			foreach (int vi in usedVerts)
				projList.Add(Vector3.Dot(verts[vi] - mean, axis));
			projList.Sort();

			float totalRange = projList[projList.Count - 1] - projList[0];

			List<float> gaps = new List<float>();
			for (int i = 1; i < projList.Count; i++)
				gaps.Add(projList[i] - projList[i - 1]);
			List<float> gapsDesc = new List<float>(gaps);
			gapsDesc.Sort();
			List<float> gapsAsc = new List<float>(gapsDesc);
			gapsDesc.Reverse();

			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			sb.AppendLine("Hull '" + hullName + "': " + faces.Length + " tris, " + usedVerts.Count + " verts, axis-range=" + totalRange.ToString("F4"));
			sb.AppendLine("Bounds along axis: min=" + projList[0].ToString("F4") + " max=" + projList[projList.Count - 1].ToString("F4"));
			sb.AppendLine("Total gap count: " + gaps.Count + " (verts along axis: " + projList.Count + ")");
			sb.AppendLine("Top 25 gaps (descending): ");
			for (int i = 0; i < Mathf.Min(25, gapsDesc.Count); i++)
				sb.AppendLine("  [" + i + "] " + gapsDesc[i].ToString("F5") + " (" + (gapsDesc[i] / totalRange * 100f).ToString("F2") + "% of range)");
			sb.AppendLine("Bottom 15 gaps (ascending, smallest first): ");
			for (int i = 0; i < Mathf.Min(15, gapsAsc.Count); i++)
				sb.AppendLine("  [" + i + "] " + gapsAsc[i].ToString("F5"));
			return sb.ToString();
		}

		// Debug helper: re-runs just the "generate colliders from existing hulls" step on the
		// currently open Prefab Stage (non-blocking, via the same editor-coroutine polling used
		// elsewhere), and logs whether it actually produced colliders. Used to isolate
		// collider-generation failures from the split step itself. Check Log / console after a
		// short wait (does not block the calling thread).
		public static void RegenerateCollidersOnly(string expectedRootName)
		{
			PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (stage == null) throw new System.InvalidOperationException("Not in a Prefab Stage.");
			GameObject root = stage.prefabContentsRoot;
			if (root.name != expectedRootName) throw new System.InvalidOperationException("Wrong prefab open.");
			Transform visual = root.transform.Find("Visual");

			RigidColliderCreatorWindow.ShowWindow();
			RigidColliderCreatorWindow rigidWindow = RigidColliderCreatorWindow.instance;
			Selection.activeGameObject = visual.gameObject;
			Log.Clear();
			rigidWindow.GenerateColliders();

			EditorCoroutines.Execute(WaitForRegenerateThenLog(rigidWindow, root));
		}

		private static IEnumerator WaitForRegenerateThenLog(RigidColliderCreatorWindow rigidWindow, GameObject root)
		{
			int frames = 0;
			do
			{
				yield return null;
				frames++;
			}
			while (rigidWindow.IsGeneratingColliders && frames < 600);

			int colliderCount = root.GetComponentsInChildren<Collider>(true).Length;
			Log.Add("REGENERATE COMPLETE after " + frames + " frames, stillGenerating=" + rigidWindow.IsGeneratingColliders + ", collidersFound=" + colliderCount);
			Debug.Log("AST116_REGENERATE_COMPLETE colliders=" + colliderCount);
		}

		// Renames a hull in the currently open Prefab Stage and clears any generated colliders -
		// used to reset naming back to a clean base name before re-running a split with corrected
		// logic (avoids "log_1_1_1"-style name stacking from re-running on an already-renamed hull).
		public static string RenameHullAndClearColliders(string expectedRootName, string oldName, string newName)
		{
			PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (stage == null) throw new System.InvalidOperationException("Not in a Prefab Stage.");
			GameObject root = stage.prefabContentsRoot;
			if (root.name != expectedRootName) throw new System.InvalidOperationException("Wrong prefab open.");
			Transform visual = root.transform.Find("Visual");
			RigidColliderCreator creator = visual.GetComponent<RigidColliderCreator>();
			PaintingData paintingData = creator.paintingData;

			Hull h = null;
			foreach (Hull cand in paintingData.hulls)
				if (cand.Name == oldName) h = cand;
			if (h == null) return "Hull '" + oldName + "' not found.";

			h.name = newName;

			foreach (Collider c in visual.GetComponents<Collider>())
				Object.DestroyImmediate(c);

			EditorUtility.SetDirty(paintingData);
			return "Renamed '" + oldName + "' -> '" + newName + "', cleared generated colliders.";
		}

		// Recovery helper: merges every hull named "<base>" or "<base>_<n>" back into a single
		// hull per name in `originalNames`, discarding the extras, and removes any generated
		// colliders. Used to undo a bad split attempt without losing the original painted
		// selections (their union reconstructs exactly what was there before the split).
		public static void MergeSplitHullsBack(string expectedRootName, string[] originalNames)
		{
			PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (stage == null)
				throw new System.InvalidOperationException("Not in a Prefab Stage - refusing to run.");

			GameObject root = stage.prefabContentsRoot;
			if (root.name != expectedRootName)
				throw new System.InvalidOperationException("Prefab Stage root is '" + root.name + "', expected '" + expectedRootName + "' - refusing to run.");

			Transform visual = root.transform.Find("Visual");
			RigidColliderCreator creator = visual.GetComponent<RigidColliderCreator>();
			Mesh mesh = visual.GetComponent<MeshFilter>().sharedMesh;
			PaintingData paintingData = creator.paintingData;

			Log.Clear();

			foreach (Collider c in visual.GetComponents<Collider>())
				Object.DestroyImmediate(c);

			foreach (string baseName in originalNames)
			{
				List<Hull> matching = new List<Hull>();
				foreach (Hull h in paintingData.hulls)
				{
					if (h.Name == baseName || h.Name.StartsWith(baseName + "_"))
						matching.Add(h);
				}

				if (matching.Count == 0)
				{
					Log.Add(baseName + ": no matching hulls found, skipped");
					continue;
				}

				List<int> unionFaces = new List<int>();
				foreach (Hull h in matching)
					unionFaces.AddRange(h.GetSelectedFaces());

				Hull keep = matching[0];
				keep.name = baseName;
				keep.type = HullType.ConvexHull;
				keep.SetSelectedFaces(unionFaces, mesh);

				for (int i = 1; i < matching.Count; i++)
					paintingData.hulls.Remove(matching[i]);

				Log.Add(baseName + ": merged " + matching.Count + " hull(s) back into 1 (" + unionFaces.Count + " tris)");
			}

			EditorUtility.SetDirty(paintingData);
			Log.Add("MERGE COMPLETE - colliders removed, ready to redo the split");
		}

		private static IEnumerator WaitForGenerateThenLog(RigidColliderCreatorWindow rigidWindow)
		{
			do
			{
				yield return null;
			}
			while (rigidWindow.IsGeneratingColliders);

			Log.Add("GENERATE COMPLETE");
			Debug.Log("AST116_SPLIT_GENERATE_COMPLETE");
		}

		// Removes colliders (and any Technie data) with no replacement - for objects that never
		// had a collider before this evaluation started.
		public static void RemoveCollidersOnly(string[] names)
		{
			List<GameObject> targets = FindTargets(names);

			Total = targets.Count;
			Processed = 0;
			IsRunning = true;
			CancelRequested = false;
			Log.Clear();
			Log.Add("RemoveOnly: matched " + targets.Count + " of " + names.Length + " requested names");

			EditorCoroutines.Execute(RemoveOnlyRoutine(targets.ToArray()));
		}

		private const string RequiredScenePath = "Assets/_Project/Scenes/VegetationGallery_TechnieColliderTest.unity";

		private static List<GameObject> FindTargets(string[] names)
		{
			Scene scene = EditorSceneManager.GetActiveScene();
			if (scene.path != RequiredScenePath)
			{
				throw new System.InvalidOperationException(
					"AST116TechnieBatchCollider refused to run: active scene is '" + scene.path +
					"', not the required test scene '" + RequiredScenePath + "'. This guard exists " +
					"because this tool must never touch the live VegetationGallery.unity or its prefabs.");
			}

			GameObject[] roots = scene.GetRootGameObjects();
			HashSet<string> nameSet = new HashSet<string>(names);

			List<GameObject> targets = new List<GameObject>();
			foreach (GameObject root in roots)
			{
				if (nameSet.Contains(root.name))
					targets.Add(root);
			}
			return targets;
		}

		private static void EnsureHullDataFolder()
		{
			if (!AssetDatabase.IsValidFolder(HullDataFolder))
			{
				AssetDatabase.CreateFolder(TargetFolder, "Physics Hulls");
			}

			string[] existing = AssetDatabase.FindAssets("t:PhysicsCreatorHullFolder");
			if (existing.Length == 0)
			{
				PhysicsCreatorHullFolder marker = ScriptableObject.CreateInstance<PhysicsCreatorHullFolder>();
				AssetDatabase.CreateAsset(marker, HullDataFolder + "/PhysicsCreatorHullFolder.asset");
				AssetDatabase.SaveAssets();
			}
		}

		// Destroys any existing Collider(s) and RigidColliderCreator component(s) under rootObj,
		// deleting the PaintingData/HullData assets they referenced so nothing is left orphaned.
		private static void CleanExisting(GameObject rootObj)
		{
			RigidColliderCreator[] creators = rootObj.GetComponentsInChildren<RigidColliderCreator>(true);
			foreach (RigidColliderCreator creator in creators)
			{
				if (creator.paintingData != null)
				{
					string p = AssetDatabase.GetAssetPath(creator.paintingData);
					if (!string.IsNullOrEmpty(p)) AssetDatabase.DeleteAsset(p);
				}
				if (creator.hullData != null)
				{
					string p = AssetDatabase.GetAssetPath(creator.hullData);
					if (!string.IsNullOrEmpty(p)) AssetDatabase.DeleteAsset(p);
				}
			}

			Collider[] oldColliders = rootObj.GetComponentsInChildren<Collider>(true);
			foreach (Collider c in oldColliders)
			{
				Object.DestroyImmediate(c);
			}

			// Re-fetch: destroying colliders above doesn't destroy the creator components themselves.
			creators = rootObj.GetComponentsInChildren<RigidColliderCreator>(true);
			foreach (RigidColliderCreator creator in creators)
			{
				Object.DestroyImmediate(creator);
			}
		}

		private static IEnumerator RemoveOnlyRoutine(GameObject[] objects)
		{
			foreach (GameObject rootObj in objects)
			{
				if (CancelRequested)
				{
					Log.Add("CANCELLED before: " + rootObj.name);
					break;
				}

				CleanExisting(rootObj);
				PrefabUtility.ApplyPrefabInstance(rootObj, InteractionMode.AutomatedAction);
				Log.Add("REMOVED: " + rootObj.name);
				Processed++;

				yield return null;
			}

			IsRunning = false;
			Debug.Log("AST116_TECHNIE_REMOVE_COMPLETE " + Processed + "/" + Total);
		}

		private static IEnumerator BatchRoutine(GameObject[] objects, AutoHullPreset preset, HullType hullType, VhacdParameters customParams)
		{
			RigidColliderCreatorWindow.ShowWindow();
			RigidColliderCreatorWindow rigidWindow = RigidColliderCreatorWindow.instance;

			foreach (GameObject rootObj in objects)
			{
				if (CancelRequested)
				{
					Log.Add("CANCELLED before: " + rootObj.name);
					break;
				}

				CleanExisting(rootObj);

				MeshFilter filter = rootObj.GetComponentInChildren<MeshFilter>();
				MeshRenderer renderer = rootObj.GetComponentInChildren<MeshRenderer>();

				if (filter != null && renderer != null && filter.sharedMesh != null)
				{
					GameObject targetObj = filter.gameObject;
					Selection.activeGameObject = targetObj;

					PaintingData paintingData = RigidColliderCreatorWindow.GenerateAsset(targetObj, filter.sharedMesh);
					rigidWindow.DeleteActiveHull();
					paintingData.autoHullPreset = preset;
					Hull hull = rigidWindow.AddHull();
					hull.type = hullType;
					rigidWindow.SceneManipulator.PaintAllFaces();
					rigidWindow.GenerateColliders();

					do
					{
						yield return null;
					}
					while (rigidWindow.IsGeneratingColliders);

					PrefabUtility.ApplyPrefabInstance(rootObj, InteractionMode.AutomatedAction);

					Log.Add("OK: " + rootObj.name);
				}
				else
				{
					Log.Add("SKIP (no mesh): " + rootObj.name);
				}

				Processed++;
			}

			rigidWindow.Close();
			IsRunning = false;
			Debug.Log("AST116_TECHNIE_BATCH_COMPLETE " + Processed + "/" + Total);
		}
	}
}
