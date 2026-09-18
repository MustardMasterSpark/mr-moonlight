using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Technie.PhysicsCreator;
using Technie.PhysicsCreator.Rigid;

namespace MrMoonlight.EditorTools.Migration
{
	// Wipes Technie paint, Technie data assets and every collider from the AST-116 test-copy
	// prefabs, except the ones listed as kept, so Carlos can repaint from scratch. Test copies
	// only (the folder below); the live vegetation prefabs are never touched.
	//
	//   TreeColliderReset.Run(keep, dryRun: true)   report only
	//   TreeColliderReset.Run(keep, dryRun: false)  do it, then re-read every prefab to verify
	public static class TreeColliderReset
	{
		private const string Folder = "Assets/_Project/Art/VegetationPrefabs/AST116_ColliderTest";

		public static string Run(string[] keepPrefabNames, bool dryRun)
		{
			if (PrefabStageUtility.GetCurrentPrefabStage() != null && !dryRun)
			{
				string open = PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot.name;
				if (System.Array.IndexOf(keepPrefabNames, open) < 0)
					throw new System.InvalidOperationException("'" + open + "' is open in Prefab Mode and would be reset - close it first.");
			}

			HashSet<string> keep = new HashSet<string>(keepPrefabNames);
			StringBuilder sb = new StringBuilder();
			sb.AppendLine((dryRun ? "DRY RUN - " : "") + "Reset of " + Folder + " (kept: " + string.Join(", ", keepPrefabNames) + ")");

			// Technie data assets referenced by kept prefabs must survive even if a reset prefab
			// somehow points at the same asset.
			HashSet<Object> keptData = new HashSet<Object>();
			List<string> paths = new List<string>();
			foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }))
				paths.Add(AssetDatabase.GUIDToAssetPath(guid));
			paths.Sort();
			foreach (string path in paths)
			{
				GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (!keep.Contains(asset.name)) continue;
				foreach (RigidColliderCreator c in asset.GetComponentsInChildren<RigidColliderCreator>(true))
				{
					if (c.paintingData != null) keptData.Add(c.paintingData);
					if (c.hullData != null) keptData.Add(c.hullData);
				}
			}

			int prefabsChanged = 0, collidersRemoved = 0, creatorsRemoved = 0, objectsRemoved = 0, assetsDeleted = 0;
			List<string> keptFound = new List<string>();
			List<string> assetPathsToDelete = new List<string>();

			foreach (string path in paths)
			{
				GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (keep.Contains(asset.name)) { keptFound.Add(asset.name); continue; }

				int cols = asset.GetComponentsInChildren<Collider>(true).Length;
				RigidColliderCreator[] creators = asset.GetComponentsInChildren<RigidColliderCreator>(true);
				RigidColliderCreatorChild[] childs = asset.GetComponentsInChildren<RigidColliderCreatorChild>(true);
				if (cols == 0 && creators.Length == 0 && childs.Length == 0) continue;

				foreach (RigidColliderCreator c in creators)
				{
					foreach (Object data in new Object[] { c.paintingData, c.hullData })
					{
						if (data == null || keptData.Contains(data)) continue;
						string dataPath = AssetDatabase.GetAssetPath(data);
						if (!string.IsNullOrEmpty(dataPath) && dataPath.StartsWith(Folder) && !assetPathsToDelete.Contains(dataPath))
							assetPathsToDelete.Add(dataPath);
					}
				}

				sb.AppendLine("  " + asset.name + ": " + cols + " colliders, " + creators.Length + " Technie creators, " + childs.Length + " Technie child objects");
				collidersRemoved += cols;
				creatorsRemoved += creators.Length;
				prefabsChanged++;
				if (dryRun) continue;

				GameObject root = PrefabUtility.LoadPrefabContents(path);
				try
				{
					// Technie child-collider objects hold nothing else; remove them whole.
					foreach (RigidColliderCreatorChild child in root.GetComponentsInChildren<RigidColliderCreatorChild>(true))
					{
						GameObject go = child.gameObject;
						bool onlyGenerated = true;
						foreach (Component comp in go.GetComponents<Component>())
							if (!(comp is Transform || comp is Collider || comp is RigidColliderCreatorChild)) onlyGenerated = false;
						if (onlyGenerated && go.transform.childCount == 0 && go != root) { Object.DestroyImmediate(go); objectsRemoved++; }
						else Object.DestroyImmediate(child);
					}
					foreach (Collider col in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
					foreach (RigidColliderCreator c in root.GetComponentsInChildren<RigidColliderCreator>(true)) Object.DestroyImmediate(c);
					PrefabUtility.SaveAsPrefabAsset(root, path);
				}
				finally
				{
					PrefabUtility.UnloadPrefabContents(root);
				}
			}

			sb.AppendLine("Prefabs to reset: " + prefabsChanged + " | colliders: " + collidersRemoved + " | Technie creators: " + creatorsRemoved + " | Technie data assets: " + assetPathsToDelete.Count);
			foreach (string p in assetPathsToDelete) sb.AppendLine("  data asset: " + p);
			sb.AppendLine("Kept (untouched): " + string.Join(", ", keptFound.ToArray()));
			foreach (string k in keep)
				if (!keptFound.Contains(k)) sb.AppendLine("WARNING: kept name '" + k + "' matched no prefab in the folder");

			if (!dryRun)
			{
				foreach (string p in assetPathsToDelete)
					if (AssetDatabase.DeleteAsset(p)) assetsDeleted++;
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
				sb.AppendLine("Deleted data assets: " + assetsDeleted + " | removed generated child objects: " + objectsRemoved);
				sb.Append(Verify(keep, paths));
			}
			return sb.ToString();
		}

		// Re-reads every prefab from disk after the reset.
		private static string Verify(HashSet<string> keep, List<string> paths)
		{
			StringBuilder sb = new StringBuilder();
			int bad = 0;
			foreach (string path in paths)
			{
				GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (asset == null || keep.Contains(asset.name)) continue;
				int cols = asset.GetComponentsInChildren<Collider>(true).Length;
				int creators = asset.GetComponentsInChildren<RigidColliderCreator>(true).Length;
				int missing = 0;
				foreach (Transform t in asset.GetComponentsInChildren<Transform>(true))
					missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
				if (cols + creators + missing > 0)
				{
					bad++;
					sb.AppendLine("VERIFY PROBLEM: " + asset.name + " still has " + cols + " colliders, " + creators + " creators, " + missing + " missing scripts");
				}
			}
			sb.AppendLine(bad == 0 ? "VERIFY OK: every reset prefab reads back with 0 colliders and 0 Technie components" : "VERIFY FAILED on " + bad + " prefabs");
			return sb.ToString();
		}
	}
}
