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
	// Scene-wide batch tools from the MRM-84 (AST-116, Technie Collider Creator 2) evaluation:
	// whole-object Auto/VHACD colliders, still the right tool for solid props (rocks, logs,
	// stumps -> one clean collider each). Only touches the AST116_ColliderTest copies, never the
	// live vegetation prefabs. Branchy trees use TreeColliderTool instead.
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

		// Reports the open Prefab Stage's hulls and collider count without changing anything.
		// Exists so the MCP execute_code tool (which can't reference the Technie namespace
		// directly) can see what's painted.
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
					sb.AppendLine("  Hull '" + h.Name + "' type=" + h.type + " selectedFaces=" + h.GetSelectedFaces().Length);
			}

			MeshFilter mf = visual.GetComponent<MeshFilter>();
			if (mf != null && mf.sharedMesh != null)
				sb.AppendLine("Mesh tris: " + (mf.sharedMesh.triangles.Length / 3));

			sb.AppendLine("Existing colliders on Visual: " + visual.GetComponents<Collider>().Length);
			return sb.ToString();
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
