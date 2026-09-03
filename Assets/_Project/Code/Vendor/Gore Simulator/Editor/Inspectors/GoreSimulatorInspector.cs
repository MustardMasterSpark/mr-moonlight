// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PampelGames.Shared;
using PampelGames.Shared.Editor;
using PampelGames.Shared.Editor.EditorTools;
using PampelGames.Shared.Tools.PGInspector.Editor;
using PampelGames.Shared.Utility;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PampelGames.GoreSimulator.Editor
{
    [CustomEditor(typeof(GoreSimulator))]
    public class GoreSimulatorInspector : GoreSimulatorInspectorBase
    {
        public SO_ModuleIconList _ModuleIconList;
        public SO_ModuleStorage _ModuleStorage;
        public SO_DefaultReferences _DefaultReferences;
        public SO_ColorKeywords _ColorKeywords;

        /* Header ***********************************************************************************************************************/

        protected override string DocumentationURL()
        {
            return Constants.DocumentationURL;
        }

        protected override bool UseGlobalSettings()
        {
            return true;
        }

        protected override void OpenGlobalSettingsWindow()
        {
            MenuItems.OpenGlobalSettings();
        }

        protected override VisualElement InspectorLogo()
        {
            return new VisualElement();
        }

        protected override void AdditionalIcons()
        {
            base.AdditionalIcons();
            additionalIcon01.style.display = DisplayStyle.Flex;
            additionalIcon01.style.backgroundImage = _ModuleIconList.moduleIcons[8];
            additionalIcon01.tooltip = "Open the Combine Skinned Meshes tool.";
            additionalIcon01.RegisterCallback<ClickEvent>(evt => MenuItems.OpenCombineSkinnedMeshes());
        }

        /********************************************************************************************************************************/

        #region Properties

        private GoreSimulator _goreSimulator;

        private ToolbarToggle visibilityToggle;

        private VisualElement SetupLeftWrapper;
        private ColorField componentColor;
        private ToolbarToggle materialsSetup;
        private ToolbarToggle childrenSetup;
        private ToolbarToggle goreTagsSetup;
        private IntegerField goreTagsAmount;
        private ToolbarMenu eventsSetup;
        private GroupBox MaterialSetupGroup;
        private ObjectField cutMaterial;
        private ObjectField cutMaterialStatic;
        private ObjectField decalMaterial;
        private ObjectField physicMaterial;

        private GroupBox ChildrenSetupGroup;
        private Toggle destroyChildren;
        private SerializedProperty childrenEnumProperty;
        private EnumField childrenEnum;
        private ListView skinnedChildren;
        private ListView fixedChildren;

        private SerializedProperty storageProperty;
        private ObjectField storage;
        private Button createStorage;

        private ToolbarToggle meshSetup;
        private VisualElement meshSetupImage;
        private GroupBox MeshSetupGroup;

        private GroupBox GoreTagsSetupGroup;
        private ListView goreTags;


        private SerializedProperty setupRagdollProperty;
        private Toggle setupRagdoll;
        private SerializedProperty setupMeshCutProperty;
        private Toggle setupMeshCut;
        private SerializedProperty meshesPerBoneProperty;
        private SliderInt meshesPerBone;
        private Slider weightsThreshold;
        private VisualElement MeshCutSubSettings;
        private VisualElement RagdollSubSettings;
        private SerializedProperty setupRagdollAnimatorProperty;
        private Toggle setupRagdollAnimator;
        private SerializedProperty ragdollAnimatorProperty;
        private ObjectField ragdollAnimator;
        private SerializedProperty ragdollTotalMassProperty;
        private FloatField ragdollTotalMass;
        private SerializedProperty jointOrientationProperty;
        private EnumField jointOrientation;
        private SerializedProperty inverseDirectionProperty;
        private Toggle inverseDirection;

        private Toggle createCollider;
        private Toggle meshPartsDuplicateCollider;
        private Toggle meshPartsDuplicateComponents;

        private Button initializeComponent;
        private Button clearComponent;
        private ToolbarButton createMeshes;

        private SerializedProperty skinnedMeshRendererProperty;
        private ObjectField skinnedMeshRenderer;
        private VisualElement BonesListWrapper;
        private Label bonesListLabel;
        private VisualElement bonesListParent;
        private ToolbarMenu bonesAutoSetup;
        private ToolbarToggle onDeathEventToggle;
        private SerializedProperty bonesListClassesProperty;
        private VisualElement MeshSetupGroupBottom;
        private VisualElement InitializeButtonWrapper;

        private VisualElement GoreModuleParent;

        private GroupBox UnityEvents;

        /********************************************************************************************************************************/

        private List<GoreModuleBase> goreModuleBaseList = new();
        private readonly List<SubModuleBase> cutModuleBaseList = new();
        private readonly List<SubModuleBase> explosionModuleBaseList = new();
        private readonly List<SubModuleBase> ragdollModuleBaseList = new();
        private readonly List<string> unityEventList = new();

        #endregion

        /********************************************************************************************************************************/

        protected override void OnEnable()
        {
            _goreSimulator = target as GoreSimulator;
            base.OnEnable();
            GetDefaultReferences();
            UpdateModuleStorage();
            GetInheritorsFromModuleStorage();
            FindElements(container);
            BindElements();
            VisualizeElements();
            SetDefaultValues();
        }

        private void GetDefaultReferences()
        {
            if (_goreSimulator._defaultReferences == null)
            {
                _goreSimulator._defaultReferences = PGAssetUtility.LoadAsset<SO_DefaultReferences>(Constants.DefaultReferences);
                EditorUtility.SetDirty(_goreSimulator);
            }

            if (_ModuleStorage == null) _ModuleStorage = PGAssetUtility.LoadAsset<SO_ModuleStorage>(Constants.ModuleStorage);

            if (_goreSimulator._globalSettings == null)
            {
                _goreSimulator._globalSettings = PGAssetUtility.LoadAsset<SO_GlobalSettings>(Constants.GlobalSettings);
                EditorUtility.SetDirty(_goreSimulator);
            }

            if (_goreSimulator._ColorKeywords == null)
            {
                _goreSimulator._ColorKeywords = PGAssetUtility.LoadAsset<SO_ColorKeywords>(Constants.ColorKeywords);
                EditorUtility.SetDirty(_goreSimulator);
            }
        }

        private void UpdateModuleStorage()
        {
            if (_goreSimulator._editorInitialized) return;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var newGoreModules = PGClassUtility.CreateInstances<GoreModuleBase>(assemblies);
            var newSubModules = PGClassUtility.CreateInstances<SubModuleBase>(assemblies);

            var needsUpdate =
                ListNeedsUpdate(_ModuleStorage.goreModules, newGoreModules) ||
                ListNeedsUpdate(_ModuleStorage.subModules, newSubModules);

            if (needsUpdate)
            {
                _ModuleStorage.goreModules = newGoreModules;
                _ModuleStorage.subModules = newSubModules;
            }

            _goreSimulator._editorInitialized = true;
            EditorUtility.SetDirty(_goreSimulator);
            EditorUtility.SetDirty(_ModuleStorage);
            return;

            bool ListNeedsUpdate<T>(List<T> currentList, List<T> newList)
            {
                if (currentList == null) return true;
                if (currentList.Any(item => item == null)) return true;

                var currentTypes = currentList.Select(e => e.GetType()).OrderBy(t => t.FullName).ToList();
                var newTypes = newList.Select(e => e.GetType()).OrderBy(t => t.FullName).ToList();

                return !currentTypes.SequenceEqual(newTypes);
            }
        }

        private void GetInheritorsFromModuleStorage()
        {
            goreModuleBaseList = new List<GoreModuleBase>(_ModuleStorage.goreModules);
            var cutModuleBaseListTemp = new List<SubModuleBase>(_ModuleStorage.subModules);
            var explosionModuleBaseListTemp = new List<SubModuleBase>(_ModuleStorage.subModules);
            var ragdollModuleBaseListTemp = new List<SubModuleBase>(_ModuleStorage.subModules);

            for (var i = 0; i < cutModuleBaseListTemp.Count; i++)
            {
                if (cutModuleBaseListTemp[i] == null) continue;
                if (!cutModuleBaseListTemp[i].CompatibleCut()) continue;
                cutModuleBaseList.Add(cutModuleBaseListTemp[i]);
            }

            for (var i = 0; i < explosionModuleBaseListTemp.Count; i++)
            {
                if (explosionModuleBaseListTemp[i] == null) continue;
                if (!explosionModuleBaseListTemp[i].CompatibleExplosion()) continue;
                explosionModuleBaseList.Add(explosionModuleBaseListTemp[i]);
            }

            for (var i = 0; i < ragdollModuleBaseListTemp.Count; i++)
            {
                if (ragdollModuleBaseListTemp[i] == null) continue;
                if (!ragdollModuleBaseListTemp[i].CompatibleRagdoll()) continue;
                ragdollModuleBaseList.Add(ragdollModuleBaseListTemp[i]);
            }
        }

        private void FindElements(VisualElement root)
        {
            visibilityToggle = root.Q<ToolbarToggle>(nameof(visibilityToggle));

            storage = root.Q<ObjectField>(nameof(storage));
            createStorage = root.Q<Button>(nameof(createStorage));

            SetupLeftWrapper = root.Q<VisualElement>(nameof(SetupLeftWrapper));
            materialsSetup = root.Q<ToolbarToggle>(nameof(materialsSetup));
            childrenSetup = root.Q<ToolbarToggle>(nameof(childrenSetup));

            eventsSetup = root.Q<ToolbarMenu>(nameof(eventsSetup));
            MaterialSetupGroup = root.Q<GroupBox>(nameof(MaterialSetupGroup));
            componentColor = root.Q<ColorField>(nameof(componentColor));
            cutMaterial = root.Q<ObjectField>(nameof(cutMaterial));
            cutMaterialStatic = root.Q<ObjectField>(nameof(cutMaterialStatic));
            decalMaterial = root.Q<ObjectField>(nameof(decalMaterial));
            physicMaterial = root.Q<ObjectField>(nameof(physicMaterial));

            ChildrenSetupGroup = root.Q<GroupBox>(nameof(ChildrenSetupGroup));
            destroyChildren = root.Q<Toggle>(nameof(destroyChildren));
            childrenEnum = root.Q<EnumField>(nameof(childrenEnum));
            skinnedChildren = root.Q<ListView>(nameof(skinnedChildren));
            fixedChildren = root.Q<ListView>(nameof(fixedChildren));

            goreTagsSetup = root.Q<ToolbarToggle>(nameof(goreTagsSetup));
            goreTagsAmount = root.Q<IntegerField>(nameof(goreTagsAmount));
            GoreTagsSetupGroup = root.Q<GroupBox>(nameof(GoreTagsSetupGroup));
            goreTags = root.Q<ListView>(nameof(goreTags));

            meshSetup = root.Q<ToolbarToggle>(nameof(meshSetup));
            meshSetupImage = root.Q<VisualElement>(nameof(meshSetupImage));
            MeshSetupGroup = root.Q<GroupBox>(nameof(MeshSetupGroup));

            setupMeshCut = root.Q<Toggle>(nameof(setupMeshCut));
            setupRagdoll = root.Q<Toggle>(nameof(setupRagdoll));
            MeshCutSubSettings = root.Q<VisualElement>(nameof(MeshCutSubSettings));
            meshesPerBone = root.Q<SliderInt>(nameof(meshesPerBone));
            weightsThreshold = root.Q<Slider>(nameof(weightsThreshold));

            RagdollSubSettings = root.Q<VisualElement>(nameof(RagdollSubSettings));
            setupRagdollAnimator = root.Q<Toggle>(nameof(setupRagdollAnimator));
            ragdollAnimator = root.Q<ObjectField>(nameof(ragdollAnimator));
            ragdollTotalMass = root.Q<FloatField>(nameof(ragdollTotalMass));
            jointOrientation = root.Q<EnumField>(nameof(jointOrientation));
            inverseDirection = root.Q<Toggle>(nameof(inverseDirection));

            createCollider = root.Q<Toggle>(nameof(createCollider));
            meshPartsDuplicateCollider = root.Q<Toggle>(nameof(meshPartsDuplicateCollider));
            meshPartsDuplicateComponents = root.Q<Toggle>(nameof(meshPartsDuplicateComponents));

            initializeComponent = root.Q<Button>(nameof(initializeComponent));
            clearComponent = root.Q<Button>(nameof(clearComponent));
            createMeshes = root.Q<ToolbarButton>(nameof(createMeshes));
            skinnedMeshRenderer = root.Q<ObjectField>(nameof(skinnedMeshRenderer));

            BonesListWrapper = root.Q<VisualElement>(nameof(BonesListWrapper));
            bonesListParent = root.Q<VisualElement>(nameof(bonesListParent));
            bonesListLabel = root.Q<Label>(nameof(bonesListLabel));
            bonesAutoSetup = root.Q<ToolbarMenu>(nameof(bonesAutoSetup));
            onDeathEventToggle = root.Q<ToolbarToggle>(nameof(onDeathEventToggle));

            MeshSetupGroupBottom = root.Q<VisualElement>(nameof(MeshSetupGroupBottom));
            InitializeButtonWrapper = root.Q<VisualElement>(nameof(InitializeButtonWrapper));

            GoreModuleParent = root.Q<VisualElement>(nameof(GoreModuleParent));

            UnityEvents = root.Q<GroupBox>(nameof(UnityEvents));
        }

        private void BindElements()
        {
            storageProperty = serializedObject.FindProperty(nameof(GoreSimulator.storage));
            storage.BindProperty(storageProperty);

            componentColor.PGSetupBindProperty(serializedObject, nameof(GoreSimulator.componentColor));

            cutMaterial.PGSetupBindProperty(serializedObject, nameof(GoreSimulator.cutMaterial));
            cutMaterialStatic.PGSetupBindProperty(serializedObject, nameof(GoreSimulator.cutMaterialStatic));
            decalMaterial.PGSetupBindProperty(serializedObject, nameof(GoreSimulator.decalMaterial));
            physicMaterial.PGSetupBindProperty(serializedObject, nameof(GoreSimulator.physicMaterial));
            destroyChildren.PGSetupBindProperty(serializedObject, nameof(GoreSimulator.destroyChildren));
            childrenEnumProperty = serializedObject.FindProperty(nameof(GoreSimulator.childrenEnum));
            childrenEnum.BindProperty(childrenEnumProperty);
            skinnedChildren.PGSetupBindProperty(serializedObject, nameof(skinnedChildren));
            fixedChildren.PGSetupBindProperty(serializedObject, nameof(fixedChildren));

            setupMeshCutProperty = serializedObject.FindProperty(nameof(GoreSimulator.setupMeshCut));
            setupMeshCut.BindProperty(setupMeshCutProperty);
            meshesPerBoneProperty = serializedObject.FindProperty(nameof(GoreSimulator.meshesPerBone));
            meshesPerBone.BindProperty(meshesPerBoneProperty);
            weightsThreshold.PGSetupBindProperty(serializedObject, nameof(GoreSimulator.weightsThreshold));
            setupRagdollProperty = serializedObject.FindProperty(nameof(GoreSimulator.setupRagdoll));
            setupRagdoll.BindProperty(setupRagdollProperty);
            setupRagdollAnimatorProperty = serializedObject.FindProperty(nameof(GoreSimulator.setupRagdollAnimator));
            setupRagdollAnimator.BindProperty(setupRagdollAnimatorProperty);
            ragdollAnimatorProperty = serializedObject.FindProperty(nameof(GoreSimulator.ragdollAnimator));
            ragdollAnimator.BindProperty(ragdollAnimatorProperty);
            ragdollTotalMassProperty = serializedObject.FindProperty(nameof(GoreSimulator.ragdollTotalMass));
            ragdollTotalMass.BindProperty(ragdollTotalMassProperty);
            jointOrientationProperty = serializedObject.FindProperty(nameof(GoreSimulator.jointOrientation));
            jointOrientation.BindProperty(jointOrientationProperty);
            inverseDirectionProperty = serializedObject.FindProperty(nameof(GoreSimulator.inverseDirection));
            inverseDirection.BindProperty(inverseDirectionProperty);
            createCollider.PGSetupBindProperty(serializedObject, nameof(GoreSimulator.createCollider));
            meshPartsDuplicateCollider.PGSetupBindProperty(serializedObject, nameof(GoreSimulator.meshPartsDuplicateCollider));
            meshPartsDuplicateComponents.PGSetupBindProperty(serializedObject, nameof(GoreSimulator.meshPartsDuplicateComponents));

            skinnedMeshRendererProperty = serializedObject.FindProperty(nameof(GoreSimulator.smr));
            skinnedMeshRenderer.BindProperty(skinnedMeshRendererProperty);

            bonesListClassesProperty = serializedObject.FindProperty(nameof(GoreSimulator.bonesListClasses));
        }

        private void VisualizeElements()
        {
            Header.PGBorderWidth(0);
            IconsLeft.style.display = DisplayStyle.None;

            visibilityToggle.tooltip = "Show/Hide all modules.";
            visibilityToggle.PGBackgroundColorHover(Constants.InspectorBackgroundHover);

            materialsSetup.tooltip = "Material references.";
            materialsSetup.PGBackgroundColorHover(Constants.InspectorBackgroundHover);
            childrenSetup.tooltip = "Children Setup.";
            childrenSetup.PGBackgroundColorHover(Constants.InspectorBackgroundHover);
            goreTagsSetup.tooltip = "Gore Tags Setup.\n" + "\n" +
                                    "Tags attached to detached parts via the 'GoreTags' component:\n" +
                                    "\n" + "bool tagExists = goreTags.CharacterTagExists(tagName)\n" + "\n" +
                                    "To set Unity tags for the detached GameObject, this can be done via the Physics module.";
            goreTagsSetup.PGBackgroundColorHover(Constants.InspectorBackgroundHover);
            eventsSetup.tooltip = "Unity Events.";
            eventsSetup.PGRemoveMenuArrow(true, true);
            eventsSetup.PGBackgroundColorHover(Constants.InspectorBackgroundHover);

            InitializeButtonWrapper.style.alignSelf = new StyleEnum<Align>(Align.FlexStart); // Unity 6 issue.

            storage.tooltip = "Serializes the mesh data for this character.";
            storage.objectType = typeof(SO_Storage);
            createStorage.tooltip = "Create a new storage. Can be shared among characters if the mesh and bones match exactly.";

            var componentColorTooltip = "Sets a uniform color for this Gore Simulator component.\n" + "\n" +
                                        "Colors can be set in the 'ColorKeywords' scriptable object, found in the 'GoreSimulator/Content/Shaders' folder.\n" +
                                        "The following shader color keywords are being applied to the materials in use:" + "\n";
            for (var i = 0; i < _ColorKeywords.colorKeywords.Count; i++)
            {
                componentColorTooltip += "\n";
                componentColorTooltip += _ColorKeywords.colorKeywords[i];
            }

            componentColorTooltip += "\n";
            componentColorTooltip += "\nTo not overwrite the material colors, leave it at full black: RBG(0,0,0)";


            componentColor.tooltip = componentColorTooltip;
            cutMaterial.tooltip = "Material used for sew spots on animated meshes.";
            cutMaterial.tooltip = "Material used for sewing spots on animated meshes.";
            cutMaterialStatic.tooltip = "Material used for sewing spots on detached, non-ragdoll meshes.\n\n" +
                                        "Non-animated sewing spots are of higher quality and without stretching.";

            decalMaterial.tooltip = "Material used for projecting decals on skinned meshes via the Decal sub-module.";
            physicMaterial.tooltip = "Reference to the physics material that determines how added colliders interact with others.";
            physicMaterial.objectType = typeof(PhysicsMaterial);

            destroyChildren.PGDrawBottomLine();
            destroyChildren.tooltip =
                "Releases children (pool if activated or destroy) in the scene that have been detached on execcution when the character gets destroyed.";
            childrenEnum.tooltip = "Identifies which objects from the bone hierarchy will be seen as children for cut and explosion operations.\n" +
                                   "Rendered means only objects are included that have a renderer attached to them.\n" + "\n" +
                                   "The hierarchy can always be resetted by using the 'ResetCharacter()' method. " +
                                   "For GameObjects added to the hierarchy after Awake, 'RecordHierarchy()' can be used to include them in the system.";

            skinnedChildren.tooltip = "This refers to the character's children that have a Skinned Mesh Renderer attached.\n\n" +
                                      "When referenced here, additional meshes will be generated for these child elements during 'cut' and 'explosion' operations. " +
                                      "However, these meshes can not be cut.\n\n" +
                                      "If you wish for these meshes to be cut as well, they will need to be combined with the main mesh.\n" +
                                      "Tools -> PampelGames -> Gore Simulator -> Combine Skinned Meshes";

            fixedChildren.tooltip = "This refers to the character’s children that should remain attached to the limb during 'cut' operations.\n\n" +
                                    "During explosions, these are unparented and also processed by the explosion modules (for example, to add a Rigidbody).";

            goreTags.tooltip = "If custom tags are specified, those can be retrieved on detached parts via the 'GoreTags' component.\n" +
                               "\n" + "bool tagExists = goreTags.TagExists(tagName)";

            meshSetup.tooltip = "Bones setup.";
            meshSetup.PGBackgroundColorHover(Constants.InspectorBackgroundHover);
            initializeComponent.tooltip = "Initializes the selected bones.";
            clearComponent.tooltip = "Remove all components from the character hierarchy that have been added by this Gore Simulator.";
            createMeshes.tooltip = "Create detached bones in the scene.\n\n" +
                                   "Can be used create gore scenes, to reference in the 'Cached Parts' explosion module, or simply to verify the mesh parts while in the editor.";
            skinnedMeshRenderer.tooltip = "Skinned Mesh Renderer of the character.";
            meshesPerBone.tooltip = "Maximum amount of separable meshes associated with each registered bone.";
            meshesPerBone.lowValue = 1;
            meshesPerBone.highValue = 5;
            weightsThreshold.lowValue = 0f;
            weightsThreshold.highValue = 0.5f;
            weightsThreshold.tooltip = "Advanced Setting:\n" + "\n" +
                                       "Sets the minimum weight required for a vertex to be included in a bone.\n" + "\n" +
                                       "Increase this value to resolve issues with distorted meshes on sub-ragdolls. Note that to high values may cause meshes to overlap.";
            bonesListLabel.tooltip = "List of detachable bones.\n" +
                                     "For best results, bones that are heavily interconnected (e.g. clavicles) should be avoided.";
            bonesAutoSetup.tooltip = "Automatically creates an initial bone setup.\n" +
                                     "The Humanoid setup can often be used for generic characters as well.";
            onDeathEventToggle.tooltip = "Toggle all 'OnDeath' events.";

            setupMeshCut.tooltip = "Initializes the mesh to be used for cut and explosion operations.";
            setupRagdoll.tooltip = "Initializes ragdoll physics.";

            createCollider.tooltip = "Create colliders for the selected bones with IGoreObject components attached.";
            meshPartsDuplicateCollider.tooltip = "Duplicates existing colliders from the character bones when creating mesh parts.";
            meshPartsDuplicateComponents.tooltip =
                "Convenience option to duplicate existing components from the PooledMesh GameObject when creating mesh parts.\n\n" +
                "Alternatively, you can simply add custom components to the mesh parts manually.\n\n" +
                "The PooledMesh can be found in GoreSimulator/Content/Pool";

            var ragdollAnimatorTooltip = "Disables the animator when the ragdoll activates.\n" +
                                         "Note that the ragdoll won't function if an animator is active on the character.";
            setupRagdollAnimator.tooltip = ragdollAnimatorTooltip;
            ragdollAnimator.tooltip = ragdollAnimatorTooltip;
            ragdollAnimator.objectType = typeof(Animator);
            setupRagdollAnimator.RegisterValueChangedCallback(evt => RagdollAnimatorDisplay());
            setupRagdoll.RegisterValueChangedCallback(evt => CreateColliderDisplay());

            RagdollAnimatorDisplay();
            CreateColliderDisplay();

            ragdollTotalMass.PGClampValue();
            jointOrientation.tooltip = "The local axis the joints rotate around";
            inverseDirection.tooltip = "Toggle if the bending direction of the joint is opposite from what's expected.";
        }

        private void SetDefaultValues()
        {
            if (_goreSimulator.cutMaterial == null)
            {
                _goreSimulator.cutMaterial = _DefaultReferences.cutMaterial;
                EditorUtility.SetDirty(_goreSimulator);
            }

            if (_goreSimulator.cutMaterialStatic == null)
            {
                _goreSimulator.cutMaterialStatic = _DefaultReferences.cutMaterialStatic;
                EditorUtility.SetDirty(_goreSimulator);
            }

            if (_goreSimulator.decalMaterial == null)
            {
                _goreSimulator.decalMaterial = _DefaultReferences.skinnedDecal;
                EditorUtility.SetDirty(_goreSimulator);
            }

            if (_goreSimulator.physicMaterial == null)
            {
                _goreSimulator.physicMaterial = _DefaultReferences.physicMaterial;
                EditorUtility.SetDirty(_goreSimulator);
            }
        }


        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        protected override void DrawInspector()
        {
            DrawVisibilityToggle();
            DrawComponentSettings();
            DrawMeshSetup();

            UpdateGoreModules();
            DrawSubMenus();

            DrawEvents();
        }

        /********************************************************************************************************************************/

        private void DrawVisibilityToggle()
        {
            visibilityToggle.RegisterValueChangedCallback(evt =>
            {
                var GoreModules = GoreModuleEditorUtility.FindAllSubModules(_goreSimulator, GoreModuleParent);
                for (var i = 0; i < GoreModules.Count; i++)
                {
                    var ModuleToggle = GoreModules[i].Q<ToolbarToggle>("ModuleToggle");
                    ModuleToggle.value = visibilityToggle.value;
                }

                UpdateCutModules();
                DrawCutMenu();
            });
        }

        private void DrawComponentSettings()
        {
            materialsSetup.RegisterValueChangedCallback(evt => { MaterialSetupDisplay(); });
            MaterialSetupDisplay();
            childrenSetup.RegisterValueChangedCallback(evt => { ChildrenSetupDisplay(); });
            ChildrenSetupDisplay();
            goreTagsSetup.RegisterValueChangedCallback(evt => { GoreTagsSetupDisplay(); });
            goreTagsAmount.RegisterCallback<ClickEvent>(x => { goreTagsSetup.value = !goreTagsSetup.value; });
            GoreTagsSetupDisplay();
            goreTags.RegisterCallback<GeometryChangedEvent>(x => { GoreTagsAmountDisplay(); });
            GoreTagsAmountDisplay();

            setupRagdoll.RegisterValueChangedCallback(evt =>
            {
                CreateBonesListView();
                SubSettingsDisplay();
            });
            setupMeshCut.RegisterValueChangedCallback(evt => { SubSettingsDisplay(); });
            SubSettingsDisplay();
        }

        private void MaterialSetupDisplay()
        {
            MaterialSetupGroup.PGDisplayStyleFlex(materialsSetup.value);
        }

        private void ChildrenSetupDisplay()
        {
            ChildrenSetupGroup.PGDisplayStyleFlex(childrenSetup.value);
        }

        private void GoreTagsSetupDisplay()
        {
            GoreTagsSetupGroup.PGDisplayStyleFlex(goreTagsSetup.value);
        }

        private void GoreTagsAmountDisplay()
        {
            goreTagsAmount.PGDisplayStyleFlex(_goreSimulator.goreTags.Count > 0);
            goreTagsAmount.value = _goreSimulator.goreTags.Count;
        }

        private void SubSettingsDisplay()
        {
            MeshCutSubSettings.PGDisplayStyleFlex(_goreSimulator.setupMeshCut);
            RagdollSubSettings.PGDisplayStyleFlex(_goreSimulator.setupRagdoll);
        }


        /********************************************************************************************************************************/

        #region DrawMeshSetup

        private void DrawMeshSetup()
        {
            createMeshes.clicked += () =>
            {
                if (!_goreSimulator.meshCutInitialized)
                {
                    EditorUtility.DisplayDialog("Initialization Missing", "The character is not initialized!", "Ok");
                    return;
                }

                var createMeshesParent = new GameObject(_goreSimulator.name + " - Mesh Parts");
                var createMeshesMeshParts = createMeshesParent.AddComponent<MeshParts>();

                _goreSimulator.Initialize();
                _goreSimulator.ExecuteExplosion(out var explosionParts);
                _goreSimulator.ResetCharacter();


                // Transforming pivot point to match with original bone
                for (var i = 0; i < explosionParts.Count; i++)
                {
                    var meshPart = explosionParts[i].GetComponent<MeshPart>();
                    var mesh = meshPart.GetComponent<MeshFilter>().sharedMesh;
                    var vertices = mesh.vertices;
                    var normals = mesh.normals;
                    var verticesWorld = new List<Vector3>();
                    var normalsWorld = new List<Vector3>();

                    for (var j = 0; j < vertices.Length; j++)
                    {
                        verticesWorld.Add(meshPart.transform.TransformPoint(vertices[j]));
                        normalsWorld.Add(meshPart.transform.TransformDirection(normals[j]));
                    }

                    meshPart.transform.position = meshPart.bonePosition;
                    meshPart.transform.eulerAngles = meshPart.boneRotation;

                    for (var j = 0; j < vertices.Length; j++)
                    {
                        vertices[j] = meshPart.transform.InverseTransformPoint(verticesWorld[j]);
                        normals[j] = meshPart.transform.InverseTransformDirection(normalsWorld[j]);
                    }

                    mesh.vertices = vertices;
                    mesh.normals = normals;
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();

                    meshPart.transform.position -= _goreSimulator.smr.transform.position;

                    if (_goreSimulator.meshPartsDuplicateCollider)
                    {
                        var bones = _goreSimulator.smr.bones;
                        for (var j = 0; j < bones.Length; j++)
                            if (meshPart.boneName.Contains(bones[j].name))
                            {
                                if (bones[j].TryGetComponent<CapsuleCollider>(out var capsuleCollider))
                                {
                                    var newCollider = explosionParts[i].AddComponent<CapsuleCollider>();
                                    var sharedMaterial = capsuleCollider.sharedMaterial;
                                    PGClassUtility.CopyClassValues(capsuleCollider, newCollider);
                                    newCollider.sharedMaterial = sharedMaterial;
                                    capsuleCollider.sharedMaterial = sharedMaterial;
                                }

                                break;
                            }
                    }

                    if (_goreSimulator.meshPartsDuplicateComponents && _DefaultReferences.pooledMesh)
                    {
                        var pooledObj = _DefaultReferences.pooledMesh;
                        var originalName = meshPart.gameObject.name;
                        var components = pooledObj.GetComponents<Component>();

                        foreach (var component in components)
                        {
                            if (component is Transform) continue;
                            if (meshPart.gameObject.GetComponent(component.GetType())) continue;

                            var newComponent = meshPart.gameObject.AddComponent(component.GetType());
                            PGClassUtility.CopyClassValues(component, newComponent);
                        }

                        meshPart.gameObject.name = originalName;
                    }

                    EditorUtility.SetDirty(meshPart);
                }

                for (var i = 0; i < explosionParts.Count; i++)
                {
                    explosionParts[i].transform.SetParent(createMeshesParent.transform);
                    var meshPart = explosionParts[i].GetComponent<MeshPart>();
                    createMeshesMeshParts.meshParts.Add(meshPart);
                }


                EditorUtility.SetDirty(createMeshesParent);
            };

            meshSetup.RegisterValueChangedCallback(evt => { MeshSetupDisplay(); });

            var skinnedChildrenProperty = serializedObject.FindProperty(nameof(skinnedChildren));
            skinnedChildren.PGSetupObjectListView(skinnedChildrenProperty, _goreSimulator.skinnedChildren);
            skinnedChildren.PGObjectListViewStyle("Skinned Children");
            
            var fixedChildrenProperty = serializedObject.FindProperty(nameof(fixedChildren));
            fixedChildren.PGSetupObjectListView(fixedChildrenProperty, _goreSimulator.fixedChildren);
            fixedChildren.PGObjectListViewStyle("Fixed Children");

            storage.RegisterValueChangedCallback(evt =>
            {
                if (storage == null)
                {
                    _goreSimulator.meshCutInitialized = false;
                    _goreSimulator.ragdollInitialized = false;
                    EditorUtility.SetDirty(_goreSimulator);
                    MeshSetupPropertiesDisplay();
                    MeshSetupWarningDisplay();
                }

                MeshSetupPropertiesDisplay();
            });

            createStorage.clicked += () =>
            {
                var newFile = CreateInstance<SO_Storage>();

                var defaultDirectory = "Assets/PampelGames/GoreSimulator/Storages";
                if (!Directory.Exists(defaultDirectory)) defaultDirectory = "Assets";

                var defaultFileName = "New Storage";
                var extension = "asset";

                var filePath = EditorUtility.SaveFilePanel("Create Storage File", defaultDirectory, defaultFileName, extension);
                if (string.IsNullOrEmpty(filePath)) return;

                var assetPath = "Assets" + filePath.Substring(Application.dataPath.Length);

                AssetDatabase.CreateAsset(newFile, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _goreSimulator.storage = newFile;
                EditorUtility.SetDirty(_goreSimulator);
            };

            skinnedMeshRenderer.RegisterValueChangedCallback(evt =>
            {
                if (_goreSimulator.smr != null)
                {
                    if (_goreSimulator.bonesListClasses == null || _goreSimulator.bonesListClasses.Count == 0)
                        MeshEditorUtility.InitializeMeshSetup(_goreSimulator);
                    RagdollEditorUtility.AssignRagdollAnimator(_goreSimulator);
                    EditorUtility.SetDirty(_goreSimulator);
                    MeshSetupPropertiesDisplay();
                    CreateBonesListView();
                    return;
                }

                _goreSimulator.bonesListClasses = null;
                _goreSimulator.bonesClasses = null;
                _goreSimulator.bones = null;
                _goreSimulator.meshCutInitialized = false;
                _goreSimulator.ragdollInitialized = false;
                EditorUtility.SetDirty(_goreSimulator);
                MeshSetupPropertiesDisplay();
                MeshSetupWarningDisplay();
                CreateBonesListView();
            });

            initializeComponent.clicked -= InitializeComponent;
            initializeComponent.clicked += InitializeComponent;
            clearComponent.clicked -= ClearComponent;
            clearComponent.clicked += ClearComponent;

            bonesAutoSetup.menu.AppendAction("Humanoid", action =>
            {
                MeshEditorUtility.AutoSetupHumanoid(_goreSimulator);
                OrderAndUpdateBonesListClasses();
            });
            bonesAutoSetup.menu.AppendAction("Generic", action =>
            {
                MeshEditorUtility.AutoSetupGeneric(_goreSimulator);
                OrderAndUpdateBonesListClasses();
            });
            bonesAutoSetup.menu.AppendAction("Clear", action =>
            {
                _goreSimulator.bonesListClasses.Clear();
                MeshEditorUtility.InitializeMeshSetup(_goreSimulator);
                OrderAndUpdateBonesListClasses();
            });
            onDeathEventToggle.RegisterValueChangedCallback(evt =>
            {
                if (_goreSimulator.bonesListClasses == null || _goreSimulator.bonesListClasses.Count == 0) return;

                for (var i = 0; i < _goreSimulator.bonesListClasses.Count; i++)
                    _goreSimulator.bonesListClasses[i].sendOnDeath = onDeathEventToggle.value;
                EditorUtility.SetDirty(_goreSimulator);
            });

            MeshSetupPropertiesDisplay();
            CreateBonesListView();
            MeshSetupDisplay();
            MeshSetupWarningDisplay();
        }

        private void InitializeComponent()
        {
            var startTime = EditorApplication.timeSinceStartup;

            _goreSimulator.meshCutInitialized = false;
            _goreSimulator.ragdollInitialized = false;
            _goreSimulator.colliderInitialized = false;

            // For prefabs to work
            for (var i = 0; i < _goreSimulator.goreModules.Count; i++)
                _goreSimulator.goreModules[i]._goreSimulator = _goreSimulator;
            for (var i = 0; i < _goreSimulator.cutModules.Count; i++)
                _goreSimulator.cutModules[i]._goreSimulator = _goreSimulator;
            for (var i = 0; i < _goreSimulator.explosionModules.Count; i++)
                _goreSimulator.explosionModules[i]._goreSimulator = _goreSimulator;
            for (var i = 0; i < _goreSimulator.ragdollModules.Count; i++)
                _goreSimulator.ragdollModules[i]._goreSimulator = _goreSimulator;
            EditorUtility.SetDirty(_goreSimulator);


            if (!BonesClassEditorInit.VerifyFillBonesClasses(_goreSimulator))
            {
                EditorUtility.SetDirty(_goreSimulator);
                UpdateGoreModules();
                MeshSetupWarningDisplay();
                return;
            }

            var bakedMesh = new Mesh();
            _goreSimulator.smr.BakeMesh(bakedMesh); // Used to get world vertex positions
            var sharedMesh = _goreSimulator.smr.sharedMesh;
            bakedMesh.boneWeights = sharedMesh.boneWeights;
            bakedMesh.bindposes = sharedMesh.bindposes;

            if (_goreSimulator.setupMeshCut)
            {
                if (!BonesClassEditorInit.FillBonesClasses(_goreSimulator, bakedMesh))
                {
                    MeshSetupWarningDisplay();
                    return;
                }

                _goreSimulator.meshCutInitialized = true;
            }


            for (var i = 0; i < _goreSimulator.bonesClasses.Count; i++)
            {
                var bone = _goreSimulator.bonesClasses[i].bone.gameObject;
                var goreBone = bone.GetComponent<GoreBone>();
                if (goreBone == null) goreBone = bone.AddComponent<GoreBone>();
                goreBone.goreSimulator = _goreSimulator;
                var bonesListClass = _goreSimulator.bonesListClasses.FirstOrDefault(item => item.bone == _goreSimulator.bonesClasses[i].bone);
                if (bonesListClass != null)
                {
                    goreBone.onDeath = bonesListClass.sendOnDeath;
                    goreBone.boneTag = bonesListClass.boneTag;
                }

                _goreSimulator.bonesClasses[i].goreBone = goreBone;
            }

            if (_goreSimulator.setupRagdoll || _goreSimulator.createCollider)
            {
                ColliderEditorInit.CreateColliders(_goreSimulator.smr, bakedMesh, _goreSimulator.bonesClasses,
                    _goreSimulator.storage.bonesStorageClasses);
                _goreSimulator.colliderInitialized = true;
            }

            if (_goreSimulator.setupRagdoll)
            {
                CharacterJointEditorInit.CreateCharacterJoints(_goreSimulator);
                _goreSimulator.ragdollInitialized = true;
            }

            EditorUtility.SetDirty(_goreSimulator.storage);
            EditorUtility.SetDirty(_goreSimulator);
            DestroyImmediate(bakedMesh);

            AddInitializedGoreModules();
            UpdateGoreModules();
            DrawSubMenus();

            MeshSetupWarningDisplay();
            meshSetupImage.PGEditorTweenColor(Color.green, 0.75f);
            EditorUtility.SetDirty(_goreSimulator);

            var elapsedTimeInSeconds = EditorApplication.timeSinceStartup - startTime;
            Debug.Log($"Initialization succeeded on Skinned Mesh Renderer: {_goreSimulator.smr.gameObject.name}.\n" +
                      $"Time taken: {elapsedTimeInSeconds:F1} s");
        }

        private void ClearComponent()
        {
            if (_goreSimulator.smr == null) return;
            for (var i = 0; i < _goreSimulator.smr.bones.Length; i++)
            {
                var bone = _goreSimulator.smr.bones[i].gameObject;
                if (!bone.TryGetComponent<GoreBone>(out var goreBone)) continue;
                if (goreBone._joint != null) DestroyImmediate(goreBone._joint);
                if (goreBone._rigidbody != null) DestroyImmediate(goreBone._rigidbody);
                if (goreBone._collider != null) DestroyImmediate(goreBone._collider);
                DestroyImmediate(goreBone);
            }
        }

        private void AddInitializedGoreModules()
        {
            if (_goreSimulator.meshCutInitialized)
            {
                if (!_goreSimulator.goreModules.Any(x => x is GoreModuleCut))
                {
                    _goreSimulator.goreModules.Add(new GoreModuleCut
                    {
                        _goreSimulator = _goreSimulator
                    });

                    EditorUtility.SetDirty(_goreSimulator);
                }

                if (!_goreSimulator.goreModules.Any(x => x is GoreModuleExplosion))
                {
                    _goreSimulator.goreModules.Add(new GoreModuleExplosion
                    {
                        _goreSimulator = _goreSimulator
                    });
                    EditorUtility.SetDirty(_goreSimulator);
                }
            }

            if (_goreSimulator.ragdollInitialized)
            {
                if (!_goreSimulator.goreModules.Any(x => x is GoreModuleRagdoll))
                    _goreSimulator.goreModules.Add(new GoreModuleRagdoll
                    {
                        _goreSimulator = _goreSimulator
                    });
                if (!_goreSimulator.ragdollModules.Any(x => x is SubModuleDisableComponents))
                {
                    _goreSimulator.ragdollModules.Add(new SubModuleDisableComponents
                    {
                        _goreSimulator = _goreSimulator
                    });
                    _goreSimulator.ragdollModules[^1].ModuleAdded(typeof(GoreModuleRagdoll));
                    _goreSimulator.ragdollModules[^1].moduleType = Enums.ModuleType.Ragdoll;
                }

                EditorUtility.SetDirty(_goreSimulator);
            }
        }

        private void MeshSetupWarningDisplay()
        {
            if (_goreSimulator.setupMeshCut && (!_goreSimulator.meshCutInitialized || _goreSimulator.bonesClasses == null ||
                                                _goreSimulator.bonesClasses.Count == 0 ||
                                                _goreSimulator.bones == null || _goreSimulator.bones.Count == 0))
            {
                meshSetupImage.style.unityBackgroundImageTintColor = new StyleColor(Color.yellow);
                initializeComponent.style.color = new StyleColor(Color.yellow);
                meshSetup.value = true;
                SetupLeftWrapper.style.display = DisplayStyle.None;
                MeshSetupDisplay();
            }
            else if (_goreSimulator.setupRagdoll && !_goreSimulator.ragdollInitialized)
            {
                meshSetupImage.style.unityBackgroundImageTintColor = new StyleColor(Color.yellow);
                initializeComponent.style.color = new StyleColor(Color.yellow);
                meshSetup.value = true;
                SetupLeftWrapper.style.display = DisplayStyle.None;
                MeshSetupDisplay();
            }
            else
            {
                meshSetupImage.style.unityBackgroundImageTintColor = new StyleColor(PGColors.InspectorButton());
                initializeComponent.style.color = new StyleColor(PGColors.InspectorHeaderText());
                SetupLeftWrapper.style.display = DisplayStyle.Flex;
                MeshSetupDisplay();
            }
        }

        private void MeshSetupDisplay()
        {
            MeshSetupGroup.PGDisplayStyleFlex(meshSetup.value);
        }

        private void MeshSetupPropertiesDisplay()
        {
            skinnedMeshRenderer.PGDisplayStyleFlex(_goreSimulator.storage != null);
            BonesListWrapper.PGDisplayStyleFlex(_goreSimulator.storage != null && _goreSimulator.smr != null);
            MeshSetupGroupBottom.PGDisplayStyleFlex(_goreSimulator.storage != null && _goreSimulator.smr != null);
        }

        private void RagdollAnimatorDisplay()
        {
            ragdollAnimator.PGDisplayStyleFlex(_goreSimulator.setupRagdollAnimator);
        }

        private void CreateColliderDisplay()
        {
            createCollider.PGDisplayStyleFlex(!_goreSimulator.setupRagdoll);
        }

        /********************************************************************************************************************************/


        private void CreateBonesListView()
        {
            var bonesListView = new ListView();
            bonesListView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            bonesListView.name = "bonesListView";
            bonesListView.itemsSource = _goreSimulator.bonesListClasses;
            bonesListView.makeItem = BonesMakeItem;
            bonesListView.bindItem = BonesBindItem;
            var existingBonesListView = bonesListParent.Q<ListView>("bonesListView");
            if (existingBonesListView != null) bonesListParent.Remove(existingBonesListView);
            bonesListParent.Add(bonesListView);
        }


        private VisualElement BonesMakeItem()
        {
            var item = new VisualElement();
            var treeIndex = new Label();
            var objectField = new ObjectField();
            var addChildButton = new Button();
            var removeButton = new Button();
            var sendOnDeath = new ToolbarToggle();
            var boneTag = new EnumField();

            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.height = 24;
            item.PGMargin(0);
            item.PGPadding(0);

            objectField.objectType = typeof(Transform);
            objectField.style.flexGrow = 1f;
            objectField.style.unityTextAlign = TextAnchor.MiddleLeft;
            objectField.style.height = 20;

            addChildButton.PGSetupAddButton();
            addChildButton.tooltip = "Add Child";
            addChildButton.PGMargin(3, 0, 0, 0);
            removeButton.PGSetupRemoveButton();
            removeButton.PGMargin(4, 0, 0, 0);
            removeButton.tooltip = "Remove";

            sendOnDeath.style.width = 20;
            sendOnDeath.style.height = 20;
            sendOnDeath.PGMargin(3, 0, 0, 0);
            sendOnDeath.PGBorderWidth(1);
            var characterJointImage = _ModuleIconList.moduleIcons[4];
            sendOnDeath.style.backgroundImage = characterJointImage;
            sendOnDeath.tooltip = "If enabled, invokes the 'OnDeath' event when this bone is cut off.";

            boneTag.name = nameof(boneTag);
            boneTag.tooltip = "Custom bone tag, attached to detached parts via the GoreTags component.\n" + "\n" +
                              "bool tagExists = goreTags.BoneTagExists(BoneTag boneTag)";
            boneTag.style.width = 54;

            item.Add(treeIndex);
            item.Add(objectField);
            item.Add(removeButton);
            item.Add(addChildButton);
            item.Add(boneTag);
            item.Add(sendOnDeath);

            return item;
        }

        private void BonesBindItem(VisualElement item, int index)
        {
            var treeIndex = item.Q<Label>();
            var objectField = item.Q<ObjectField>();
            var addChildButton = item.Q<Button>("addButton");
            var removeButton = item.Q<Button>("removeButton");
            var sendOnDeath = item.Q<ToolbarToggle>();
            var boneTag = item.Q<EnumField>("boneTag");

            serializedObject.Update();

            var bonesListClass = _goreSimulator.bonesListClasses[index];

            if (index > 0 && bonesListClass.guid == 0) // Should not be neccessary. Only here for savety after bones setup update 1.5.5.
            {
                bonesListClass.guid = MathUtility.UniqueGUID();
                var parentBone = BonesListClassUtility.GetParentBone(bonesListClass.bone, _goreSimulator.bonesListClasses);
                if (parentBone != null) bonesListClass.parentGuid = parentBone.guid;
                EditorUtility.SetDirty(_goreSimulator);
            }

            if (index == 0)
            {
                item.Remove(removeButton);
                var tooltip = "The 'central bone' (e.g. pelvis). \n" + "Note: The center of the character, not the bottom root bone.";
                objectField.tooltip = tooltip;
                treeIndex.tooltip = tooltip;
                treeIndex.text = "Center";
            }
            else
            {
                var numberOfParents = BonesListClassUtility.GetNumberOfParents(bonesListClass, _goreSimulator.bonesListClasses);

                var treeIndexText = "";
                for (var i = 1; i < numberOfParents + 1; i++)
                    if (i > 1)
                        treeIndexText += "  |  ";

                treeIndex.text = treeIndexText;
            }

            var listClassProperty = bonesListClassesProperty.GetArrayElementAtIndex(index);
            var boneProperty = listClassProperty.FindPropertyRelative(nameof(BonesListClass.bone));
            objectField.BindProperty(boneProperty);
            objectField.RegisterValueChangedCallback(evt =>
            {
                if (bonesListClass.bone == null) return;
                if (!_goreSimulator.smr.bones.Contains(bonesListClass.bone))
                {
                    Debug.LogWarning(bonesListClass.bone.name + " is not part of the skinned mesh renderer bones.");
                    bonesListClass.bone = null;
                    EditorUtility.SetDirty(_goreSimulator);
                }

                if (index > 0) // None-Weighted center bone is allowed.
                    if (!BoneWeightsUtility.DoesBoneHaveWeights(_goreSimulator.smr, bonesListClass.bone.name))
                    {
                        Debug.LogWarning(bonesListClass.bone.name + " has no bone weights.");
                        bonesListClass.bone = null;
                        EditorUtility.SetDirty(_goreSimulator);
                    }

                if (_goreSimulator.bonesListClasses.Count(b => b.bone == bonesListClass.bone) > 1)
                {
                    if (bonesListClass.bone != null) Debug.LogWarning(bonesListClass.bone.name + " is already in the list.");
                    bonesListClass.bone = null;
                    EditorUtility.SetDirty(_goreSimulator);
                }
            });

            addChildButton.clicked += () =>
            {
                var newItem = new BonesListClass
                {
                    guid = MathUtility.UniqueGUID(),
                    parentGuid = bonesListClass.guid
                };
                _goreSimulator.bonesListClasses.Add(newItem);

                OrderAndUpdateBonesListClasses();
            };

            removeButton.clicked += () =>
            {
                var parentBone = _goreSimulator.bonesListClasses.Find(_item => _item.guid == bonesListClass.parentGuid);

                if (parentBone != null)
                {
                    var childBones =
                        _goreSimulator.bonesListClasses.Where(_item => _item.parentGuid == bonesListClass.guid).ToList();
                    for (var i = 0; i < childBones.Count; i++) childBones[i].parentGuid = parentBone.guid;
                }

                _goreSimulator.bonesListClasses.RemoveAt(index);
                OrderAndUpdateBonesListClasses();
            };


            var sendOnDeathProperty = listClassProperty.FindPropertyRelative(nameof(BonesListClass.sendOnDeath));
            sendOnDeath.BindProperty(sendOnDeathProperty);
            sendOnDeath.style.unityBackgroundImageTintColor =
                sendOnDeathProperty.boolValue ? new StyleColor(Color.red) : new StyleColor(Color.white);

            sendOnDeath.RegisterValueChangedCallback(evt =>
            {
                sendOnDeath.style.unityBackgroundImageTintColor = evt.newValue ? new StyleColor(Color.red) : new StyleColor(Color.white);
            });


            boneTag.BindProperty(listClassProperty.FindPropertyRelative(nameof(BonesListClass.boneTag)));
        }

        private void OrderAndUpdateBonesListClasses()
        {
            _goreSimulator.bonesListClasses = BonesListClassUtility.SortBonesList(_goreSimulator.bonesListClasses);
            _goreSimulator.meshCutInitialized = false;
            EditorUtility.SetDirty(_goreSimulator);
            CreateBonesListView();
        }

        #endregion

        /********************************************************************************************************************************/


        private void UpdateGoreModules()
        {
            GoreModuleParent.Clear();

            for (var i = 0; i < _goreSimulator.goreModules.Count; i++)
            {
                var i1 = i;
                var image = _ModuleIconList.moduleIcons[_goreSimulator.goreModules[i].imageIndex()];
                var GoreModule = GoreModuleEditorUtility.CreateGoreGroup(image, i);
                GoreModule.tooltip = _goreSimulator.goreModules[i].ModuleInfo();
                var ModuleMenu = GoreModule.Q<ToolbarMenu>("ModuleMenu");

                ModuleMenu.menu.AppendAction("Refresh Scripts", action =>
                {
                    _goreSimulator._editorInitialized = false;
                    UpdateModuleStorage();
                    GetInheritorsFromModuleStorage();
                    EditorUtility.SetDirty(_goreSimulator);
                    UpdateGoreModules();
                    DrawSubMenus();
                });

                ModuleMenu.menu.AppendAction("Remove All", action =>
                {
                    _goreSimulator.goreModules[i1].ClearSubmodules();
                    EditorUtility.SetDirty(_goreSimulator);
                    UpdateGoreModules();
                    DrawSubMenus();
                });
                GoreModuleParent.Add(GoreModule);
            }


            if (!_goreSimulator.goreModules.OfType<GoreModuleCut>().Any())
            {
                _goreSimulator.cutModules.Clear();
                EditorUtility.SetDirty(_goreSimulator);
            }

            if (!_goreSimulator.goreModules.OfType<GoreModuleExplosion>().Any())
            {
                _goreSimulator.explosionModules.Clear();
                EditorUtility.SetDirty(_goreSimulator);
            }

            if (!_goreSimulator.goreModules.OfType<GoreModuleRagdoll>().Any())
            {
                _goreSimulator.ragdollModules.Clear();
                EditorUtility.SetDirty(_goreSimulator);
            }

            GoreModulesDisplay();
        }

        private void GoreModulesDisplay()
        {
            var GoreModuleCut = GoreModuleEditorUtility.FindSubModule<GoreModuleCut>(_goreSimulator, GoreModuleParent);
            var GoreModuleExplosion = GoreModuleEditorUtility.FindSubModule<GoreModuleExplosion>(_goreSimulator, GoreModuleParent);
            var GoreModuleRagdoll = GoreModuleEditorUtility.FindSubModule<GoreModuleRagdoll>(_goreSimulator, GoreModuleParent);

            if (GoreModuleCut != null && GoreModuleExplosion != null)
            {
                GoreModuleCut.PGDisplayStyleFlex(_goreSimulator.meshCutInitialized);
                GoreModuleExplosion.PGDisplayStyleFlex(_goreSimulator.meshCutInitialized);
            }

            if (GoreModuleRagdoll != null) GoreModuleRagdoll.PGDisplayStyleFlex(_goreSimulator.ragdollInitialized);
        }

        /********************************************************************************************************************************/

        private void DrawSubMenus()
        {
            DrawCutMenu();
            UpdateCutModules();

            DrawExplosionMenu();
            UpdateExplosionModules();

            DrawRagdollMenu();
            UpdateRagdollModules();
        }

        /********************************************************************************************************************************/


        #region Draw Cut Menu

        private void DrawCutMenu()
        {
            var GoreModule = GoreModuleEditorUtility.FindSubModule<GoreModuleCut>(_goreSimulator, GoreModuleParent);
            if (GoreModule == null) return;

            var addSubGoreMenu = GoreModule.Q<ToolbarMenu>("addSubGoreMenu");
            addSubGoreMenu.menu.MenuItems().Clear();

            for (var i = 0; i < cutModuleBaseList.Count; i++)
            {
                var moduleName = cutModuleBaseList[i].ModuleName();
                if (_goreSimulator.cutModules.Any(m => m.ModuleName() == moduleName)) continue;
                var i1 = i;
                addSubGoreMenu.menu.AppendAction(moduleName, action => OnCutMenuSelected(action, i1));
            }

            UpdateCutSubIcons();
        }

        private void OnCutMenuSelected(DropdownMenuAction action, int index)
        {
            var instance = (SubModuleBase) Activator.CreateInstance(cutModuleBaseList[index].GetType());
            instance._goreSimulator = _goreSimulator;
            instance.ModuleAdded(typeof(GoreModuleCut));
            instance.moduleType = Enums.ModuleType.Cut;
            _goreSimulator.cutModules.Add(instance);
            EditorUtility.SetDirty(_goreSimulator);
            var GoreModuleCut = GoreModuleEditorUtility.FindSubModule<GoreModuleCut>(_goreSimulator, GoreModuleParent);
            var ModuleToggle = GoreModuleCut.Q<ToolbarToggle>("ModuleToggle");
            ModuleToggle.value = true;
            UpdateCutModules();
            DrawCutMenu();
        }

        private void UpdateCutModules()
        {
            serializedObject.Update();

            var GoreModule = GoreModuleEditorUtility.FindSubModule<GoreModuleCut>(_goreSimulator, GoreModuleParent);
            if (GoreModule == null) return;

            var SubModuleParent = GoreModule.Q<GroupBox>("SubModuleParent");
            SubModuleParent.Clear();
            for (var i = 0; i < _goreSimulator.cutModules.Count; i++)
            {
                var image = _ModuleIconList.moduleIcons[_goreSimulator.cutModules[i].imageIndex()];
                var SubModule = GoreModuleEditorUtility.CreateSubGroup(image, _goreSimulator.cutModules[i].ModuleName());
                SubModule.tooltip = _goreSimulator.cutModules[i].ModuleInfo();
                var addSubGoreMenu = SubModule.Q<ToolbarMenu>("SubModuleMenu");
                var i1 = i;

                addSubGoreMenu.menu.AppendAction("Remove", action =>
                {
                    _goreSimulator.cutModules.RemoveAt(i1);
                    EditorUtility.SetDirty(_goreSimulator);
                    UpdateCutModules();
                    DrawCutMenu();
                });

                var SubModuleProperties = SubModule.Q<VisualElement>("SubModuleProperties");
                var subModulePropertyField = new PropertyField();
                var moduleArray = serializedObject.FindProperty(nameof(GoreSimulator.cutModules));
                var moduleProperty = moduleArray.GetArrayElementAtIndex(i);
                subModulePropertyField.BindProperty(moduleProperty);
                SubModuleProperties.Add(subModulePropertyField);

                SubModuleParent.Add(SubModule);
            }
        }

        private void UpdateCutSubIcons()
        {
            serializedObject.Update();
            var GoreModule = GoreModuleEditorUtility.FindSubModule<GoreModuleCut>(_goreSimulator, GoreModuleParent);
            if (GoreModule == null) return;
            var SubIcons = GoreModule.Q<VisualElement>("SubIcons");
            SubIcons.Clear();
            for (var i = 0; i < _goreSimulator.cutModules.Count; i++)
            {
                var image = _ModuleIconList.moduleIcons[_goreSimulator.cutModules[i].imageIndex()];
                var subIcon = GoreModuleEditorUtility.CreateSubImage(image, _goreSimulator.cutModules[i].ModuleName());
                SubIcons.Add(subIcon);
            }
        }

        #endregion

        #region Draw Explosion Menu

        private void DrawExplosionMenu()
        {
            var GoreModule = GoreModuleEditorUtility.FindSubModule<GoreModuleExplosion>(_goreSimulator, GoreModuleParent);
            if (GoreModule == null) return;

            var addSubGoreMenu = GoreModule.Q<ToolbarMenu>("addSubGoreMenu");
            addSubGoreMenu.menu.MenuItems().Clear();

            for (var i = 0; i < explosionModuleBaseList.Count; i++)
            {
                var moduleName = explosionModuleBaseList[i].ModuleName();
                if (_goreSimulator.explosionModules.Any(m => m.ModuleName() == moduleName)) continue;
                var i1 = i;
                addSubGoreMenu.menu.AppendAction(moduleName, action => OnExplosionMenuSelected(action, i1));
            }

            UpdateExplosionSubIcons();
        }

        private void OnExplosionMenuSelected(DropdownMenuAction action, int index)
        {
            var instance = (SubModuleBase) Activator.CreateInstance(explosionModuleBaseList[index].GetType());
            instance._goreSimulator = _goreSimulator;
            instance.ModuleAdded(typeof(GoreModuleExplosion));
            instance.moduleType = Enums.ModuleType.Explosion;
            _goreSimulator.explosionModules.Add(instance);
            EditorUtility.SetDirty(_goreSimulator);
            var GoreModuleExplosion = GoreModuleEditorUtility.FindSubModule<GoreModuleExplosion>(_goreSimulator, GoreModuleParent);
            var ModuleToggle = GoreModuleExplosion.Q<ToolbarToggle>("ModuleToggle");
            ModuleToggle.value = true;
            UpdateExplosionModules();
            DrawExplosionMenu();
        }

        private void UpdateExplosionModules()
        {
            serializedObject.Update();

            var GoreModule = GoreModuleEditorUtility.FindSubModule<GoreModuleExplosion>(_goreSimulator, GoreModuleParent);
            if (GoreModule == null) return;

            var SubModuleParent = GoreModule.Q<GroupBox>("SubModuleParent");
            SubModuleParent.Clear();
            for (var i = 0; i < _goreSimulator.explosionModules.Count; i++)
            {
                var image = _ModuleIconList.moduleIcons[_goreSimulator.explosionModules[i].imageIndex()];
                var SubModule = GoreModuleEditorUtility.CreateSubGroup(image, _goreSimulator.explosionModules[i].ModuleName());
                SubModule.tooltip = _goreSimulator.explosionModules[i].ModuleInfo();
                var addSubGoreMenu = SubModule.Q<ToolbarMenu>("SubModuleMenu");
                var i1 = i;

                addSubGoreMenu.menu.AppendAction("Remove", action =>
                {
                    _goreSimulator.explosionModules.RemoveAt(i1);
                    EditorUtility.SetDirty(_goreSimulator);
                    UpdateExplosionModules();
                    DrawExplosionMenu();
                });

                var SubModuleProperties = SubModule.Q<VisualElement>("SubModuleProperties");
                var subModulePropertyField = new PropertyField();
                var moduleArray = serializedObject.FindProperty(nameof(GoreSimulator.explosionModules));
                var moduleProperty = moduleArray.GetArrayElementAtIndex(i);
                subModulePropertyField.BindProperty(moduleProperty);
                SubModuleProperties.Add(subModulePropertyField);

                SubModuleParent.Add(SubModule);
            }
        }

        private void UpdateExplosionSubIcons()
        {
            serializedObject.Update();
            var GoreModule = GoreModuleEditorUtility.FindSubModule<GoreModuleExplosion>(_goreSimulator, GoreModuleParent);
            if (GoreModule == null) return;
            var SubIcons = GoreModule.Q<VisualElement>("SubIcons");
            SubIcons.Clear();
            for (var i = 0; i < _goreSimulator.explosionModules.Count; i++)
            {
                var image = _ModuleIconList.moduleIcons[_goreSimulator.explosionModules[i].imageIndex()];
                var subIcon = GoreModuleEditorUtility.CreateSubImage(image, _goreSimulator.explosionModules[i].ModuleName());
                SubIcons.Add(subIcon);
            }
        }

        #endregion

        #region Draw Ragdoll Menu

        private void DrawRagdollMenu()
        {
            var GoreModule = GoreModuleEditorUtility.FindSubModule<GoreModuleRagdoll>(_goreSimulator, GoreModuleParent);
            if (GoreModule == null) return;

            var addSubGoreMenu = GoreModule.Q<ToolbarMenu>("addSubGoreMenu");
            addSubGoreMenu.menu.MenuItems().Clear();

            for (var i = 0; i < ragdollModuleBaseList.Count; i++)
            {
                var moduleName = ragdollModuleBaseList[i].ModuleName();
                if (_goreSimulator.ragdollModules.Any(m => m.ModuleName() == moduleName)) continue;
                var i1 = i;
                addSubGoreMenu.menu.AppendAction(moduleName, action => OnRagdollMenuSelected(action, i1));
            }

            UpdateRagdollSubIcons();
        }

        private void OnRagdollMenuSelected(DropdownMenuAction action, int index)
        {
            var instance = (SubModuleBase) Activator.CreateInstance(ragdollModuleBaseList[index].GetType());
            instance._goreSimulator = _goreSimulator;
            instance.ModuleAdded(typeof(GoreModuleRagdoll));
            instance.moduleType = Enums.ModuleType.Ragdoll;
            _goreSimulator.ragdollModules.Add(instance);
            EditorUtility.SetDirty(_goreSimulator);
            var GoreModuleRagdoll = GoreModuleEditorUtility.FindSubModule<GoreModuleRagdoll>(_goreSimulator, GoreModuleParent);
            var ModuleToggle = GoreModuleRagdoll.Q<ToolbarToggle>("ModuleToggle");
            ModuleToggle.value = true;
            UpdateRagdollModules();
            DrawRagdollMenu();
        }

        private void UpdateRagdollModules()
        {
            serializedObject.Update();

            var GoreModule = GoreModuleEditorUtility.FindSubModule<GoreModuleRagdoll>(_goreSimulator, GoreModuleParent);
            if (GoreModule == null) return;

            var SubModuleParent = GoreModule.Q<GroupBox>("SubModuleParent");
            SubModuleParent.Clear();
            for (var i = 0; i < _goreSimulator.ragdollModules.Count; i++)
            {
                var image = _ModuleIconList.moduleIcons[_goreSimulator.ragdollModules[i].imageIndex()];
                var SubModule = GoreModuleEditorUtility.CreateSubGroup(image, _goreSimulator.ragdollModules[i].ModuleName());
                SubModule.tooltip = _goreSimulator.ragdollModules[i].ModuleInfo();
                var addSubGoreMenu = SubModule.Q<ToolbarMenu>("SubModuleMenu");
                var i1 = i;

                addSubGoreMenu.menu.AppendAction("Remove", action =>
                {
                    _goreSimulator.ragdollModules.RemoveAt(i1);
                    EditorUtility.SetDirty(_goreSimulator);
                    UpdateRagdollModules();
                    DrawRagdollMenu();
                });

                var SubModuleProperties = SubModule.Q<VisualElement>("SubModuleProperties");
                var subModulePropertyField = new PropertyField();
                var moduleArray = serializedObject.FindProperty(nameof(GoreSimulator.ragdollModules));
                var moduleProperty = moduleArray.GetArrayElementAtIndex(i);
                subModulePropertyField.BindProperty(moduleProperty);
                SubModuleProperties.Add(subModulePropertyField);

                SubModuleParent.Add(SubModule);
            }
        }

        private void UpdateRagdollSubIcons()
        {
            serializedObject.Update();
            var GoreModule = GoreModuleEditorUtility.FindSubModule<GoreModuleRagdoll>(_goreSimulator, GoreModuleParent);
            if (GoreModule == null) return;
            var SubIcons = GoreModule.Q<VisualElement>("SubIcons");
            SubIcons.Clear();
            for (var i = 0; i < _goreSimulator.ragdollModules.Count; i++)
            {
                var image = _ModuleIconList.moduleIcons[_goreSimulator.ragdollModules[i].imageIndex()];
                var subIcon = GoreModuleEditorUtility.CreateSubImage(image, _goreSimulator.ragdollModules[i].ModuleName());
                SubIcons.Add(subIcon);
            }
        }

        #endregion

        /********************************************************************************************************************************/

        private void DrawEvents()
        {
            PGEventSystemEditorSetup.DrawEventClass(nameof(_goreSimulator.onExecuteClass), _goreSimulator.onExecuteClass, serializedObject,
                eventsSetup, UnityEvents, true);
            PGEventSystemEditorSetup.DrawEventClass(nameof(_goreSimulator.onExecuteCutClass), _goreSimulator.onExecuteCutClass, serializedObject,
                eventsSetup, UnityEvents, true);
            PGEventSystemEditorSetup.DrawEventClass(nameof(_goreSimulator.onExecuteExplosionClass), _goreSimulator.onExecuteExplosionClass,
                serializedObject, eventsSetup, UnityEvents, true);
            PGEventSystemEditorSetup.DrawEventClass(nameof(_goreSimulator.onExecuteRagdollClass), _goreSimulator.onExecuteRagdollClass,
                serializedObject, eventsSetup, UnityEvents, true);
            PGEventSystemEditorSetup.DrawEventClass(nameof(_goreSimulator.onCharacterReset), _goreSimulator.onCharacterReset, serializedObject,
                eventsSetup, UnityEvents, true);
            PGEventSystemEditorSetup.DrawEventClass(nameof(_goreSimulator.onDeath), _goreSimulator.onDeath, serializedObject,
                eventsSetup, UnityEvents, true);
            PGEventSystemEditorSetup.DrawEventClass(nameof(_goreSimulator.onDestroy), _goreSimulator.onDestroy, serializedObject,
                eventsSetup, UnityEvents, true);
        }
    }
}