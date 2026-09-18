#if TCC_HAS_BURST && TCC_HAS_COLLECTIONS
#define TCC_ENABLE_DYN_COLLIDER
#endif

// Unity 6.1 means we can use mesh LOD
#if UNITY_6000_1_OR_NEWER
#define TCC_USE_MESH_LOD
#endif

using UnityEditor;
using UnityEngine;

namespace Technie.PhysicsCreator.Dynamic
{

	[CustomEditor(typeof(DynamicSkinnedCollider))]
	public class DynamicSkinnedColliderEditor : Editor
	{
		private DynamicSkinnedCollider ourTarget;

		private SerializedProperty skinnedRendererProp;
		private SerializedProperty updateBehaviourProp;
		private SerializedProperty throttledIntervalProp;

		private SerializedProperty isTriggerProp;
		private SerializedProperty isConvexProp;
		private SerializedProperty physicsMaterialProp;
		private SerializedProperty cookingOptionsProp;

		private SerializedProperty lodProp;

		private SerializedProperty layerOverridePriorityProp;
		private SerializedProperty includeLayersProp;
		private SerializedProperty excludeLayersProp;

		private bool colliderFoldout;

		private bool hadChanges;

		private void OnEnable()
		{
			if (target == null)
				return;

			ourTarget = target as DynamicSkinnedCollider;

			skinnedRendererProp = serializedObject.FindProperty(nameof(DynamicSkinnedCollider.skinnedRenderer));
			updateBehaviourProp = serializedObject.FindProperty(nameof(DynamicSkinnedCollider.updateBehaviour));
			throttledIntervalProp = serializedObject.FindProperty(nameof(DynamicSkinnedCollider.throttledInterval));

			isTriggerProp = serializedObject.FindProperty(nameof(DynamicSkinnedCollider.isTrigger));
			isConvexProp = serializedObject.FindProperty(nameof(DynamicSkinnedCollider.convex));
			physicsMaterialProp = serializedObject.FindProperty(nameof(DynamicSkinnedCollider.physicsMaterial));
			cookingOptionsProp = serializedObject.FindProperty(nameof(DynamicSkinnedCollider.cookingOptions));

			lodProp = serializedObject.FindProperty(nameof(DynamicSkinnedCollider.lod));

			layerOverridePriorityProp = serializedObject.FindProperty(nameof(DynamicSkinnedCollider.layerOverridePriority));
			includeLayersProp = serializedObject.FindProperty(nameof(DynamicSkinnedCollider.includeLayers));
			excludeLayersProp = serializedObject.FindProperty(nameof(DynamicSkinnedCollider.excludeLayers));
		}

		private void OnDisable()
		{
			if (hadChanges)
			{
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
			}
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

#if TCC_ENABLE_DYN_COLLIDER
			DrawCustomInspector();
#else
			string msg = "Unity.Collections and Unity.Burst libraries are required to use this component.\n"
					+ "Please add them to your project via the Package Manager.";
			EditorGUILayout.HelpBox(msg, MessageType.Error);
#endif
		}

		private void DrawCustomInspector()
		{
			EditorGUI.BeginChangeCheck();

			EditorGUILayout.PropertyField(skinnedRendererProp);
			EditorGUILayout.PropertyField(updateBehaviourProp);

			bool isThrottled = ((DynamicSkinnedCollider.UpdateBehaviour)updateBehaviourProp.intValue == DynamicSkinnedCollider.UpdateBehaviour.Throttled);
			EditorGUI.BeginDisabledGroup(!isThrottled);
			EditorGUILayout.IntSlider(throttledIntervalProp, 0, 60);
			EditorGUI.EndDisabledGroup();

#if TCC_USE_MESH_LOD
			string lodLabel = "Simplifaction level (LOD)";
			SkinnedMeshRenderer renderer = skinnedRendererProp.objectReferenceValue as SkinnedMeshRenderer;
			Mesh targetMesh = renderer != null ? renderer.sharedMesh : null;
			int lodCount = targetMesh != null ? targetMesh.lodCount : 0;
			if (lodCount > 1)
			{
				EditorGUILayout.IntSlider(lodProp, 0, lodCount-1, lodLabel);
				int numTriangles = 0;
				int clampedLod = Mathf.Clamp(lodProp.intValue, 0, lodCount-1);
				for (int s=0; s<targetMesh.subMeshCount; s++)
				{
					MeshLodRange lodRange = targetMesh.GetLod(s, clampedLod);
					numTriangles += (int)lodRange.indexCount / 3;
				}
				string triCount = "";
				if (numTriangles > 1000)
				{
					triCount = ((float)numTriangles / 1000.0f).ToString("0.0") + "k";
				}
				else
				{
					triCount = numTriangles.ToString();
				}
				EditorGUI.indentLevel++;
				EditorGUILayout.HelpBox(string.Format("Using {0} triangles from lod {1}", triCount, clampedLod), MessageType.None);
				EditorGUI.indentLevel--;
			}
			else if (lodCount == 1)
			{
				// Have mesh, but no lods
				string enableLodsMsg = "Enable mesh LOD generation (in model's import settings) to use collider LOD";
				EditorGUILayout.HelpBox(enableLodsMsg, MessageType.Info);
				EditorGUI.BeginDisabledGroup(true);
				EditorGUILayout.IntSlider(lodProp, 0, 7, lodLabel);
				EditorGUI.EndDisabledGroup();
			}
			else if (lodCount == 0)
			{
				// No renderer or no mesh - just show the LOD slider but disabled
				EditorGUI.BeginDisabledGroup(true);
				EditorGUILayout.IntSlider(lodProp, 0, 7, lodLabel);
				EditorGUI.EndDisabledGroup();
			}
#endif

				EditorGUI.indentLevel++;
			colliderFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(colliderFoldout, new GUIContent("Collider options", Icons.Active.hullIcon)); // FIXME: Proper icon ref here
			if (colliderFoldout)
			{
				EditorGUILayout.PropertyField(isConvexProp);

				EditorGUI.BeginDisabledGroup(!isConvexProp.boolValue);
				EditorGUILayout.PropertyField(isTriggerProp);
				EditorGUI.EndDisabledGroup();

				EditorGUILayout.PropertyField(physicsMaterialProp);
				EditorGUILayout.PropertyField(cookingOptionsProp);

				EditorGUILayout.LabelField("Layer Overrides", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(layerOverridePriorityProp);
				EditorGUILayout.PropertyField(includeLayersProp);
				EditorGUILayout.PropertyField(excludeLayersProp);
			}
			EditorGUI.indentLevel--;

#if !TCC_USE_MESH_LOD
			string noLodSupportMsg = "Collider LOD is only supported for Unity 6.1 or newer.";
			EditorGUILayout.HelpBox(noLodSupportMsg, MessageType.Info);
#endif

			if (EditorGUI.EndChangeCheck())
			{
				EditorUtility.SetDirty(target);
				hadChanges = true;
			}

			hadChanges = serializedObject.ApplyModifiedProperties() || hadChanges;

		}
	}

} // namespace Technie.PhysicsCreator.Dynamic