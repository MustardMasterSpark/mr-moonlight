// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using PampelGames.Shared;
using PampelGames.Shared.Editor;
using PampelGames.Shared.Utility;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PampelGames.GoreSimulator.Editor
{
    public class GlobalSettings : EditorWindow
    {
        [HideInInspector] public SO_GlobalSettings globalSettingsSo;
        private SerializedObject serializedObject;
        
        private VisualElement poolingWrapper;
        private VisualElement profilingWrapper;
        
        private Button resetButton;

        
        // Editor

        
        // Pool
        private Toggle poolCutActive;
        private Toggle poolEffectsActive;
        
        private SerializedProperty cutPreloadProperty;
        private IntegerField cutPreload;
        
        private SerializedProperty effectsPreloadProperty;
        private IntegerField effectsPreload;
        
        private SerializedProperty hidePooledObjectsProperty;
        private Toggle hidePooledObjects;
        
        private SerializedProperty createGUIProfilerButtonProperty;
        private Button createGUIProfilerButton;

        /********************************************************************************************************************************/
        private void OnEnable()
        {
            if (!globalSettingsSo)
                globalSettingsSo = PGAssetUtility.LoadAsset<SO_GlobalSettings>(Constants.GlobalSettings);
            
            serializedObject ??= new SerializedObject(globalSettingsSo);

            CreateEditorWindow();
            CreateEditorWrapper();
            CreateRuntimeWrapper();
            CreateResetButton();

            BindElements();
            
            
        }

        private void CreateEditorWindow()
        {
            string[] elementNames = new string[2];
            elementNames[0] = "Pool";
            elementNames[1] = "Profiling";
            
            PGEditorWindowSetup.CreateEditorWindow("Gore Simulator - Global Settings", elementNames, out var _parentElement, out var _elementsArray);
            
            poolingWrapper = _elementsArray[0];
            profilingWrapper = _elementsArray[1];

            rootVisualElement.Add(_parentElement);
        }

        private void CreateEditorWrapper()
        {

        }

        private void CreateRuntimeWrapper()
        {
            poolCutActive = new Toggle("Pool Parts");
            poolCutActive.tooltip = "Utilize pooling for detached parts and effects.\n" +
                                 "Make sure to call 'SceneCleanup()' on active Gore Simulators or in the API before closing a scene to properly release pooled objects.";
            poolCutActive.PGToggleStyleDefault();
            
            poolEffectsActive = new Toggle("Pool Effects");
            poolEffectsActive.tooltip = "Utilize pooling for Spawn Effects modules.\n" +
                                        "Make sure to call 'SceneCleanup()' on active Gore Simulators or in the API before closing a scene to properly release pooled objects.";
            poolEffectsActive.PGToggleStyleDefault();
            
            
            hidePooledObjects = new Toggle();
            hidePooledObjects.PGToggleStyleDefault();
            hidePooledObjects.label = "Hide Pooled Objects";
            hidePooledObjects.tooltip = "Hide pooled objects in the scene hierarchy. This option won't have any impact on the built application.";
            
            cutPreload = new IntegerField("Parts Preload");
            cutPreload.tooltip = "GameObjects used for cut and explosion operations preloaded into the pool in Awake. " +
                                       "Shared among all Gore Simulator components.";
            cutPreload.PGClampValue();
            
            effectsPreload = new IntegerField("Effects Preload");
            effectsPreload.tooltip = "Effects preloaded into the pool in Awake. " +
                                      "Shared among all Gore Simulator components, where one pool is created for each different object.";
            effectsPreload.PGClampValue();
            

            createGUIProfilerButton = new Button();
            createGUIProfilerButton.text = "Create GUI Profiler";
            createGUIProfilerButton.tooltip = "Create a GameObject that shows runtime infos on the screen about active Gore Simulators.";
            
            poolingWrapper.Add(poolCutActive);
            poolingWrapper.Add(poolEffectsActive);
            poolingWrapper.Add(cutPreload);
            poolingWrapper.Add(effectsPreload);
            poolingWrapper.Add(hidePooledObjects);
            
            profilingWrapper.Add(createGUIProfilerButton);
        }
        
        private void CreateResetButton()
        {
            resetButton = PGEditorWindowSetup.CreateResetButton();
            resetButton.clicked += ResetValuesClicked;
            
            rootVisualElement.Add(resetButton);
        }
        
        private void ResetValuesClicked()
        {
            if (EditorUtility.DisplayDialog("Reset Settings", "Reset all global settings to their default values?", "Ok", "Cancel"))
            {
                globalSettingsSo.ResetValues();
                EditorUtility.SetDirty(globalSettingsSo);
            }
        }
        private void BindElements()
        {
            poolCutActive.PGSetupBindProperty(serializedObject, nameof(SO_GlobalSettings.poolCutActive));
            poolEffectsActive.PGSetupBindProperty(serializedObject, nameof(SO_GlobalSettings.poolEffectsActive));
            hidePooledObjectsProperty = serializedObject.FindProperty(nameof(SO_GlobalSettings.hidePooledObjects));
            hidePooledObjects.BindProperty(hidePooledObjectsProperty);
            cutPreloadProperty = serializedObject.FindProperty(nameof(SO_GlobalSettings.cutPreload));
            cutPreload.BindProperty(cutPreloadProperty);
            effectsPreloadProperty = serializedObject.FindProperty(nameof(SO_GlobalSettings.effectsPreload));
            effectsPreload.BindProperty(effectsPreloadProperty);
        }
        
        
        /********************************************************************************************************************************/

        public void CreateGUI()
        {
            
            createGUIProfilerButton.clicked += () =>
            {
                var existingGUIProfiler = FindFirstObjectByType<GUIProfiler>();
                if (existingGUIProfiler != null)
                {
                    if (EditorUtility.DisplayDialog("GUI Profiler", "There is allready a GUI Profiler in the scene.", "Remove", "Cancel"))
                    {
                        DestroyImmediate(existingGUIProfiler.gameObject);
                    }
                    return;
                }

                var newGUIProfiler = new GameObject("Gore Simulator Profiler");
                newGUIProfiler.AddComponent<GUIProfiler>();
                newGUIProfiler.transform.position = Vector3.zero;
            };
            
            PoolingVisibility();
            poolCutActive.RegisterValueChangedCallback(evt => PoolingVisibility());
            poolEffectsActive.RegisterValueChangedCallback(evt => PoolingVisibility());
        }

        private void PoolingVisibility()
        {
            hidePooledObjects.PGDisplayStyleFlex(globalSettingsSo.poolCutActive || globalSettingsSo.poolEffectsActive);
            cutPreload.PGDisplayStyleFlex(globalSettingsSo.poolCutActive);
            effectsPreload.PGDisplayStyleFlex(globalSettingsSo.poolEffectsActive);
        }
    }
}