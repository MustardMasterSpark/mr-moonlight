using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DDT
{
    public class DrawDebugTools : MonoBehaviour
    {
        #region ========== Variables ==========

        private static DrawDebugTools instance;

        [Header("Settings")] [Tooltip("Settings asset for the debug camera, float graphs and agent paths")]
        public DDTSettings m_DDTSettings;

        // Lines
        private const int InitialLineCapacity = 256;

        private BatchedLine[] m_TransientLines;
        private int m_TransientLineCount;
        private BatchedLine[] m_PersistentLines;
        private int m_PersistentLineCount;

        private Mesh m_Mesh, m_BillboardMesh;
        private int m_LineMeshFrame = -1;
        private bool m_HasLineMesh;
        private Vector3[] m_ShapeScratch;
        private List<Collider> m_ColliderScratch;
        private List<Vector3> m_MeshPositions;
        private List<Color32> m_MeshColors;
        private int[] m_MeshIndices;

        // Quads
        private List<DebugBillboard> m_DebugBillboardsList;
        private MaterialPropertyBlock m_QuadMatPropertyBlock;

        // Materials
        private bool m_DidReportMissingShader;
        private Material m_LineMaterial;
        private Material m_QuadMaterial;

        // Shader paths, relative to a Resources folder
        private const string LineShaderResourcePath = "Shaders/DDT_VertexColorLine";
        private const string BillboardShaderResourcePath = "Shaders/DDT_Billboard";

        // Length used to draw a ray whose max distance is infinite
        private const float InfiniteRayDisplayLength = 1000.0f;

        // Colors used by DrawRaycastHit
        private static readonly Color RayHitColor = new Color(0.0f, 0.792f, 0.298f, 1.0f);
        private static readonly Color RayMissColor = new Color(1.0f, 0.72f, 0.15f, 1.0f);
        private static readonly Color RayDetailColor = new Color(0.071f, 0.722f, 0.788f, 1.0f);
        private static readonly Color RayLabelColor = new Color(0.949f, 0.961f, 0.969f, 1.0f);

        // Text
        private const int DefaultTextFontSize = 48;
        private const float DefaultTextScale = 0.05f;
        private List<DebugText> m_DebugTextsList;
        private GameObject m_3DTextsParent;
        private GameObject m_3DTextPrefab;
        private List<TextMesh> m_3DTextsList;

        // Debug camera
        private GameObject m_DebugCameraPrefab;
        private DDTCamera m_DebugCamera;
        private List<Camera> m_GameCamerasList;
        private bool m_IsCursorVisible = false;
        private CursorLockMode m_CursorLockMode;
        private float m_TimeScaleBeforeFreeze = 1.0f;
        private bool m_DidFreezeTime = false;

        private GameObject m_MainCamera = null;
        private bool m_DebugCameraIsActive = false;

        // Debug float
        private List<DebugFloatGraph> m_FloatGraphsList;
        private Dictionary<string, DebugFloatGraph> m_FloatGraphsByName;


        // Log message
        private List<DebugLogMessage> m_LogMessagesList;
        GameObject m_DDTCanvasPrefab;
        DDTCanvas m_DDTCanvas;

        #region ========== Properties ==========

        public static DrawDebugTools Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                instance = FindAnyObjectByType<DrawDebugTools>();
                if (instance != null)
                    return instance;

                // No instance is created outside play mode
                if (!Application.isPlaying)
                    return null;

                GameObject Host = new GameObject("DrawDebugTools");
                instance = Host.AddComponent<DrawDebugTools>();

                return instance;
            }

            set { instance = value; }
        }

        public DDTCamera DebugCamera
        {
            get { return m_DebugCamera; }

            set { m_DebugCamera = value; }
        }

        public GameObject MainCamera
        {
            get { return m_MainCamera; }

            set { m_MainCamera = value; }
        }

        public bool DebugCameraIsActive
        {
            get { return m_DebugCameraIsActive; }

            set { m_DebugCameraIsActive = value; }
        }

        /// <summary>
        /// Time scale the game was running at when the debug camera froze time.
        /// </summary>
        public float TimeScaleBeforeFreeze
        {
            get { return m_TimeScaleBeforeFreeze; }
        }

        #endregion

        #endregion

        #region ========== Initialization ==========

        private void Awake()
        {
            // Keep the first instance and remove any later duplicate. Disabling it here
            // stops OnEnable and Update running before the deferred Destroy lands.
            if (instance != null && instance != this)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }

            instance = this;

            // Survive scene changes
            DontDestroyOnLoad(gameObject);

            // Init batched lines
            m_TransientLines = new BatchedLine[InitialLineCapacity];
            m_PersistentLines = new BatchedLine[InitialLineCapacity];
            m_MeshPositions = new List<Vector3>(InitialLineCapacity * 2);
            m_MeshColors = new List<Color32>(InitialLineCapacity * 2);
            m_MeshIndices = new int[0];
            m_ShapeScratch = new Vector3[0];
            m_ColliderScratch = new List<Collider>();

            EnsureMeshes();

            // Init debug text list
            m_DebugTextsList = new List<DebugText>();
            m_3DTextsList = new List<TextMesh>();
            m_3DTextPrefab = Resources.Load<GameObject>("Prefabs/DDT3DText");

            // Float graphs
            m_FloatGraphsList = new List<DebugFloatGraph>();
            m_FloatGraphsByName = new Dictionary<string, DebugFloatGraph>();

            // Debug quads
            m_DebugBillboardsList = new List<DebugBillboard>();
            m_QuadMatPropertyBlock = new MaterialPropertyBlock();

            // Debug camera
            m_DebugCameraPrefab = Resources.Load<GameObject>("Prefabs/DDTDebugCamera");
            m_GameCamerasList = new List<Camera>();

            // Init log messages list
            m_LogMessagesList = new List<DebugLogMessage>();
            m_DDTCanvasPrefab = Resources.Load<GameObject>("Prefabs/DDTCanvas");

            // Make sure settings params are valid
            if (m_DDTSettings == null)
            {
                m_DDTSettings = Resources.Load<DDTSettings>("Settings/DDTSettings");
            }

            if (m_DDTSettings == null)
            {
                m_DDTSettings = ScriptableObject.CreateInstance<DDTSettings>();
            }
        }

        private void Start()
        {
            InitializeMaterials();
        }

        /// <summary>
        /// Creates the line and billboard meshes, replacing either if it has been destroyed.
        /// </summary>
        private void EnsureMeshes()
        {
            if (m_Mesh == null)
            {
                m_Mesh = new Mesh();
                m_Mesh.name = "DDT Lines";
                m_Mesh.hideFlags = HideFlags.HideAndDontSave;
                m_Mesh.indexFormat = IndexFormat.UInt32;
                m_Mesh.MarkDynamic();
            }

            if (m_BillboardMesh == null)
            {
                m_BillboardMesh = new Mesh();
                m_BillboardMesh.name = "DDT Billboard";
                m_BillboardMesh.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;

            DestroyOwned(m_Mesh);
            DestroyOwned(m_BillboardMesh);
            DestroyOwned(m_LineMaterial);
            DestroyOwned(m_QuadMaterial);
        }

        /// <summary>Destroys an object this component created, in play mode or the editor.</summary>
        private static void DestroyOwned(UnityEngine.Object Target)
        {
            if (Target == null)
                return;

            if (Application.isPlaying)
                Destroy(Target);
            else
                DestroyImmediate(Target);
        }

        private void OnEnable()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipelineManager.beginCameraRendering += OnScriptableCameraRendering;
            else
                Camera.onPreCull += OnCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnScriptableCameraRendering;
            Camera.onPreCull -= OnCameraRendering;
        }

        private void OnScriptableCameraRendering(ScriptableRenderContext Context, Camera RenderingCamera)
        {
            OnCameraRendering(RenderingCamera);
        }

        /// <summary>
        /// Builds the line mesh once for the frame, then submits it for this camera.
        /// </summary>
        private void OnCameraRendering(Camera RenderingCamera)
        {
            if (RenderingCamera == null || !ShouldDrawForCamera(RenderingCamera))
                return;

            if (m_LineMeshFrame != Time.frameCount)
            {
                m_LineMeshFrame = Time.frameCount;
                m_HasLineMesh = HandleDrawingListOfLines();
            }

            if (!m_HasLineMesh)
                return;

            Graphics.DrawMesh(m_Mesh, Matrix4x4.identity, m_LineMaterial, GetDrawLayer(), RenderingCamera);
        }

        /// <summary>
        /// True for game and scene view cameras, false for every other camera type.
        /// </summary>
        private static bool ShouldDrawForCamera(Camera RenderingCamera)
        {
            return RenderingCamera.cameraType == CameraType.Game
                   || RenderingCamera.cameraType == CameraType.SceneView;
        }

        private int GetDrawLayer()
        {
            return m_DDTSettings != null ? m_DDTSettings.m_DrawLayer : 0;
        }

        /// <summary>
        /// Clears transient lines that no camera rendered.
        /// </summary>
        private void DiscardUnrenderedLines()
        {
            if (m_LineMeshFrame < Time.frameCount - 1)
                m_TransientLineCount = 0;
        }

        private void InitializeMaterials()
        {
            if (m_DidReportMissingShader)
                return;

            if (!m_LineMaterial)
                m_LineMaterial = CreateMaterial(LineShaderResourcePath, "Hidden/Internal-Colored");

            if (!m_QuadMaterial)
                m_QuadMaterial = CreateMaterial(BillboardShaderResourcePath, "Unlit/Transparent");

            m_DidReportMissingShader = !m_LineMaterial || !m_QuadMaterial;
        }

        /// <summary>
        /// Builds a material from a shader under Resources, falling back to a built-in shader
        /// looked up by name.
        /// </summary>
        private static Material CreateMaterial(string ResourcePath, string FallbackShaderName)
        {
            Shader Source = Resources.Load<Shader>(ResourcePath);

            if (Source == null)
            {
                Source = Shader.Find(FallbackShaderName);

                if (Source == null)
                {
                    Debug.LogError($"[DDT] Neither Resources/{ResourcePath} nor {FallbackShaderName} "
                                   + "could be loaded, so nothing will be drawn.");
                    return null;
                }
            }

            Material Result = new Material(Source);
            Result.hideFlags = HideFlags.HideAndDontSave;
            return Result;
        }

        #endregion

        #region ========== Update Function ==========

        private void Update()
        {
            // Reset pos and rot
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            DiscardUnrenderedLines();

            HandleDebugCamera();
            HandleDrawingListOfBillboards();
            HandleDrawingListOfTexts();
            HandleListOfLogMessagesList();
            HandleDrawingListOfFloatGraphs();
        }
        #endregion

        #region ========== Camera Debug ==========

        private void HandleDebugCamera()
        {
            // Toggle debug camera
            if (DDTInput.GetKeyDown(m_DDTSettings.m_ToggleDebugCameraKey))
            {
                ToggleDebugCamera();
            }

            if (m_DebugCameraIsActive)
            {
                // Debug camera raycast
                RaycastHit DebugHitInfos;
                if (Physics.Raycast(m_DebugCamera.transform.position, m_DebugCamera.transform.forward, out DebugHitInfos,
                    1000.0f))
                {
                    // Draw normal line
                    DrawLine(DebugHitInfos.point, DebugHitInfos.point + DebugHitInfos.normal,
                        m_DDTSettings.m_DebugNormalVectColor);
                }

                // Draw other cameras
                foreach (var CamItem in m_GameCamerasList)
                {
                    DrawCamera(CamItem, m_DDTSettings.m_MainCamShapeColor);
                }

                // Update debug camera ui infos
                if (EnsureCanvas())
                    m_DDTCanvas.UpdateDebugCamera(DebugHitInfos);
            }
        }

        public void ToggleDebugCamera()
        {
            // Set cameras list
            m_GameCamerasList.Clear();
            m_GameCamerasList.AddRange(FindObjectsByType<Camera>(FindObjectsSortMode.None));

            if (m_DebugCameraIsActive)
            {
                m_DebugCamera.gameObject.SetActive(false);
                m_DebugCameraIsActive = false;

                // Deactivate game debug infos ui
                if (m_DDTCanvas && m_DDTCanvas.DebugCameraInfos)
                    m_DDTCanvas.DebugCameraInfos.gameObject.SetActive(false);

                // Cursor state
                Cursor.visible = m_IsCursorVisible;
                Cursor.lockState = m_CursorLockMode;

                // Time scale
                if (m_DidFreezeTime)
                {
                    Time.timeScale = m_TimeScaleBeforeFreeze;
                    m_DidFreezeTime = false;
                }
            }
            else
            {
                // Create debug camera if doesn't exist
                if (m_DebugCamera == null)
                {
                    m_DebugCamera = GameObject.Instantiate(m_DebugCameraPrefab).GetComponent<DDTCamera>();
                    m_DebugCamera.transform.SetParent(transform);
                }
                else
                {
                        m_DebugCamera.gameObject.SetActive(true);
                }

                // Get the current main camera
                m_MainCamera = Camera.main != null ? Camera.main.gameObject : null;

                // Set debug camera
                if (m_MainCamera)
                {
                    // Set pos / rot
                    m_DebugCamera.transform.position = m_MainCamera.transform.position;
                    m_DebugCamera.transform.rotation = m_MainCamera.transform.rotation;
                    // Set cam flag
                    m_DebugCamera.GetComponent<Camera>().clearFlags = m_MainCamera.GetComponent<Camera>().clearFlags;
                    m_DebugCamera.GetComponent<Camera>().backgroundColor =
                        m_MainCamera.GetComponent<Camera>().backgroundColor;
                    // Set fov
                    m_DebugCamera.GetComponent<Camera>().fieldOfView = m_MainCamera.GetComponent<Camera>().fieldOfView;
                    // Set rot variables
                    m_DebugCamera.InitializeCamRotation();
                }
                else
                {
                    Debug.LogError("[DDT] No main camera in the scene.");
                }

                // Cursor state
                m_IsCursorVisible = Cursor.visible;
                m_CursorLockMode = Cursor.lockState;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;


                m_DebugCameraIsActive = true;

                // Set time scale to 0
                if (m_DDTSettings.m_IsDebugCamFreezeTime)
                {
                    m_TimeScaleBeforeFreeze = Time.timeScale;
                    m_DidFreezeTime = true;
                    Time.timeScale = 0.0f;
                }
            }
        }

        #endregion

        #region ========== Drawing Functions ==========

        /// <summary>
        /// Draw a wire sphere
        /// </summary>
        /// <param name="Center">Position of the sphere</param>
        /// <param name="Radius">Radius of the sphere</param>
        /// <param name="Segments">Segments count that form the sphere</param>
        /// <param name="Color">Color of the sphere</param>
        /// <param name="LifeTime">Lifetime before stop drawing the sphere</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawSphere(Vector3 Center, float Radius, int Segments, Color Color, float LifeTime = 0.0f)
        {
            if (DrawDebugTools.Instance == null)
                return;
            DrawSphere(Center, Quaternion.identity, Radius, Segments, Color, LifeTime);
        }

        /// <summary>
        /// Method to draw a wire sphere
        /// </summary>
        /// <param name="Center">Position of the sphere</param>
        /// <param name="Rotation"></param>
        /// <param name="Radius">Radius of the sphere</param>
        /// <param name="Segments">Segments count that form the sphere</param>
        /// <param name="Color">Color of the sphere</param>
        /// <param name="LifeTime">Lifetime before stop drawing the sphere</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawSphere(Vector3 Center, Quaternion Rotation, float Radius, int Segments, Color Color, float LifeTime = 0.0f)
        {
            if (DrawDebugTools.Instance == null)
                return;

            Segments = Mathf.Max(Segments, 4);
            Segments = (int) Mathf.Round((float) Segments / 4.0f) * 4;

            float AngleInc = 2.0f * Mathf.PI / (float) Segments;

            for (int i = 0; i < Segments; i++)
            {
                float PolarAngle = 0.0f;
                float AzimuthalAngle = AngleInc * i;

                float Point_1_X = Mathf.Sin(PolarAngle) * Mathf.Cos(AzimuthalAngle);
                float Point_1_Y = Mathf.Cos(PolarAngle);
                float Point_1_Z = Mathf.Sin(PolarAngle) * Mathf.Sin(AzimuthalAngle);

                float Point_2_X;
                float Point_2_Y;
                float Point_2_Z;

                // The polar angle sweeps pole to pole
                for (int J = 0; J < Segments / 2 + 1; J++)
                {
                    Point_2_X = Mathf.Sin(PolarAngle) * Mathf.Cos(AzimuthalAngle);
                    Point_2_Y = Mathf.Cos(PolarAngle);
                    Point_2_Z = Mathf.Sin(PolarAngle) * Mathf.Sin(AzimuthalAngle);

                    float Point_3_X = Mathf.Sin(PolarAngle) * Mathf.Cos(AzimuthalAngle + AngleInc);
                    float Point_3_Y = Mathf.Cos(PolarAngle);
                    float Point_3_Z = Mathf.Sin(PolarAngle) * Mathf.Sin(AzimuthalAngle + AngleInc);

                    Vector3 Point_1 = new Vector3(Point_1_X, Point_1_Y, Point_1_Z) * Radius + Center;
                    Vector3 Point_2 = new Vector3(Point_2_X, Point_2_Y, Point_2_Z) * Radius + Center;
                    Vector3 Point_3 = new Vector3(Point_3_X, Point_3_Y, Point_3_Z) * Radius + Center;

                    // At J zero both points sit on the pole
                    if (J > 0)
                        InternalDrawLine(Point_1, Point_2, Center, Rotation, Color, LifeTime);

                    InternalDrawLine(Point_2, Point_3, Center, Rotation, Color, LifeTime);

                    Point_1_X = Point_2_X;
                    Point_1_Y = Point_2_Y;
                    Point_1_Z = Point_2_Z;

                    PolarAngle += AngleInc;
                }
            }
                }

        /// <summary>
        /// Draw a 3D line in space
        /// </summary>
        /// <param name="LineStart">Position of the line start</param>
        /// <param name="LineEnd">Position of the line end</param>
        /// <param name="Color">Color of the line</param>
        /// <param name="LifeTime">Line life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawLine(Vector3 LineStart, Vector3 LineEnd, Color Color, float LifeTime = 0.0f)
        {
            if (DrawDebugTools.Instance == null)
                return;
            DrawDebugTools.Instance.AddLine(new BatchedLine(LineStart, LineEnd, Vector3.zero,
                Quaternion.identity, Color, LifeTime));
        }

        /// <summary>
        /// Draw a 3D point in space
        /// </summary>
        /// <param name="Position">Position of the point</param>
        /// <param name="Size">Size of the point</param>
        /// <param name="Color">Color of the point</param>
        /// <param name="LifeTime">Point life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawPoint(Vector3 Position, float Size, Color Color, float LifeTime = 0.0f)
        {
            // X
            InternalDrawLine(Position + new Vector3(-Size / 2.0f, 0.0f, 0.0f),
                Position + new Vector3(Size / 2.0f, 0.0f, 0.0f), Position, Quaternion.identity, Color, LifeTime);
            // Y
            InternalDrawLine(Position + new Vector3(0.0f, -Size / 2.0f, 0.0f),
                Position + new Vector3(0.0f, Size / 2.0f, 0.0f), Position, Quaternion.identity, Color, LifeTime);
            // Z
            InternalDrawLine(Position + new Vector3(0.0f, 0.0f, -Size / 2.0f),
                Position + new Vector3(0.0f, 0.0f, Size / 2.0f), Position, Quaternion.identity, Color, LifeTime);
        }

        /// <summary>
        /// Draw directional arrow
        /// </summary>
        /// <param name="ArrowStart">Arrow start position</param>
        /// <param name="ArrowEnd">Arrow end position</param>
        /// <param name="ArrowSize">Arrow size</param>
        /// <param name="Color">Arrow color</param>
        /// <param name="LifeTime">Arrow life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawDirectionalArrow(Vector3 ArrowStart, Vector3 ArrowEnd, float ArrowSize, Color Color,
            float LifeTime = 0.0f)
        {
            InternalDrawLine(ArrowStart, ArrowEnd, ArrowStart, Quaternion.identity, Color, LifeTime);

            Vector3 Dir;
            Vector3 Right;
            Vector3 Up;
            InternalGetDirectionBasis(ArrowEnd - ArrowStart, out Dir, out Right, out Up);

            InternalDrawLine(ArrowEnd, ArrowEnd + (Right - Dir) * ArrowSize, ArrowStart, Quaternion.identity,
                Color, LifeTime);
            InternalDrawLine(ArrowEnd, ArrowEnd + (-Right - Dir) * ArrowSize, ArrowStart, Quaternion.identity,
                Color, LifeTime);
        }

        /// <summary>
        /// Draw a box
        /// </summary>
        /// <param name="Center">Center position of the box</param>
        /// <param name="Rotation">Rotation of the box</param>
        /// <param name="Size">The size of the box</param>
        /// <param name="Color">Color of the box</param>
        /// <param name="LifeTime">Box life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawBox(Vector3 Center, Quaternion Rotation, Vector3 Size, Color Color, float LifeTime = 0.0f)
        {
            InternalDrawLine(Center + new Vector3(Size.x, Size.y, Size.z) / 2.0f,
                Center + new Vector3(Size.x, -Size.y, Size.z) / 2.0f, Center, Rotation, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(Size.x, -Size.y, Size.z) / 2.0f,
                Center + new Vector3(-Size.x, -Size.y, Size.z) / 2.0f, Center, Rotation, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(-Size.x, -Size.y, Size.z) / 2.0f,
                Center + new Vector3(-Size.x, Size.y, Size.z) / 2.0f, Center, Rotation, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(-Size.x, Size.y, Size.z) / 2.0f,
                Center + new Vector3(Size.x, Size.y, Size.z) / 2.0f, Center, Rotation, Color, LifeTime);

            InternalDrawLine(Center + new Vector3(Size.x, Size.y, -Size.z) / 2.0f,
                Center + new Vector3(Size.x, -Size.y, -Size.z) / 2.0f, Center, Rotation, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(Size.x, -Size.y, -Size.z) / 2.0f,
                Center + new Vector3(-Size.x, -Size.y, -Size.z) / 2.0f, Center, Rotation, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(-Size.x, -Size.y, -Size.z) / 2.0f,
                Center + new Vector3(-Size.x, Size.y, -Size.z) / 2.0f, Center, Rotation, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(-Size.x, Size.y, -Size.z) / 2.0f,
                Center + new Vector3(Size.x, Size.y, -Size.z) / 2.0f, Center, Rotation, Color, LifeTime);

            InternalDrawLine(Center + new Vector3(Size.x, Size.y, Size.z) / 2.0f,
                Center + new Vector3(Size.x, Size.y, -Size.z) / 2.0f, Center, Rotation, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(Size.x, -Size.y, Size.z) / 2.0f,
                Center + new Vector3(Size.x, -Size.y, -Size.z) / 2.0f, Center, Rotation, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(-Size.x, -Size.y, Size.z) / 2.0f,
                Center + new Vector3(-Size.x, -Size.y, -Size.z) / 2.0f, Center, Rotation, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(-Size.x, Size.y, Size.z) / 2.0f,
                Center + new Vector3(-Size.x, Size.y, -Size.z) / 2.0f, Center, Rotation, Color, LifeTime);
        }

        /// <summary>
        /// Draw a 3D circle
        /// </summary>
        /// <param name="Center">Center position of the circle</param>
        /// <param name="Rotation">Rotation of the circle</param>
        /// <param name="Radius">Radius of the circle</param>
        /// <param name="Segments">Segments count in the circle</param>
        /// <param name="Color">Color of the circle</param>
        /// <param name="LifeTime">Circle life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawCircle(Vector3 Center, Quaternion Rotation, float Radius, int Segments, Color Color,
            float LifeTime = 0.0f)
        {
            Segments = Mathf.Max(Segments, 4);
            Segments = (int) Mathf.Round((float) Segments / 4.0f) * 4;

            float AngleInc = 2.0f * Mathf.PI / (float) Segments;

            float Angle = 0.0f;
            for (int i = 0; i < Segments; i++)
            {
                Vector3 Point_1 = Center + Radius * new Vector3(Mathf.Cos(Angle), 0.0f, Mathf.Sin(Angle));
                Angle += AngleInc;
                Vector3 Point_2 = Center + Radius * new Vector3(Mathf.Cos(Angle), 0.0f, Mathf.Sin(Angle));
                InternalDrawLine(Point_1, Point_2, Center, Rotation, Color, LifeTime);
            }
        }

        /// <summary>
        /// Draw a 3D circle on a plane defined by axis (XZ, XY, YZ)
        /// </summary>
        /// <param name="Center">Center position of the circle</param>
        /// <param name="Radius">Radius of the circle</param>
        /// <param name="Segments">Segments count in the circle</param>
        /// <param name="Color">Color of the circle</param>
        /// <param name="DrawPlaneAxis">Plane axis to draw circle in (XZ, XY, YZ)</param>
        /// <param name="LifeTime">Circle life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawCircle(Vector3 Center, float Radius, int Segments, Color Color,
            EDrawPlaneAxis DrawPlaneAxis = EDrawPlaneAxis.XZ, float LifeTime = 0.0f)
        {
            Segments = Mathf.Max(Segments, 4);
            Segments = (int) Mathf.Round((float) Segments / 4.0f) * 4;

            float AngleInc = 2.0f * Mathf.PI / (float) Segments;

            float Angle = 0.0f;
            switch (DrawPlaneAxis)
            {
                case EDrawPlaneAxis.XZ:
                    for (int i = 0; i < Segments; i++)
                    {
                        Vector3 Point_1 = Center + Radius * new Vector3(Mathf.Cos(Angle), 0.0f, Mathf.Sin(Angle));
                        Angle += AngleInc;
                        Vector3 Point_2 = Center + Radius * new Vector3(Mathf.Cos(Angle), 0.0f, Mathf.Sin(Angle));
                        InternalDrawLine(Point_1, Point_2, Center, Quaternion.identity, Color, LifeTime);
                    }

                    break;
                case EDrawPlaneAxis.XY:
                    for (int i = 0; i < Segments; i++)
                    {
                        Vector3 Point_1 = Center + Radius * new Vector3(Mathf.Cos(Angle), Mathf.Sin(Angle), 0.0f);
                        Angle += AngleInc;
                        Vector3 Point_2 = Center + Radius * new Vector3(Mathf.Cos(Angle), Mathf.Sin(Angle), 0.0f);
                        InternalDrawLine(Point_1, Point_2, Center, Quaternion.identity, Color, LifeTime);
                    }

                    break;
                case EDrawPlaneAxis.YZ:
                    for (int i = 0; i < Segments; i++)
                    {
                        Vector3 Point_1 = Center + Radius * new Vector3(0.0f, Mathf.Sin(Angle), Mathf.Cos(Angle));
                        Angle += AngleInc;
                        Vector3 Point_2 = Center + Radius * new Vector3(0.0f, Mathf.Sin(Angle), Mathf.Cos(Angle));
                        InternalDrawLine(Point_1, Point_2, Center, Quaternion.identity, Color, LifeTime);
                    }

                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Draw a 3D coordinates
        /// </summary>
        /// <param name="Position">Position of the coordinates</param>
        /// <param name="Rotation">Rotation of the coordinates</param>
        /// <param name="Scale">Scale of the coordinate</param>
        /// <param name="LifeTime">Coordinates lifetime</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void Draw3DCoordinates(Vector3 Position, Quaternion Rotation, float Scale, float LifeTime = 0.0f)
        {
            InternalDrawLine(Position, Position + new Vector3(Scale, 0.0f, 0.0f), Position, Rotation, Color.red, LifeTime);
            InternalDrawLine(Position, Position + new Vector3(0.0f, Scale, 0.0f), Position, Rotation, Color.green,
                LifeTime);
            InternalDrawLine(Position, Position + new Vector3(0.0f, 0.0f, Scale), Position, Rotation, Color.blue, LifeTime);
        }

        /// <summary>
        /// Draw a 3D cylinder
        /// </summary>
        /// <param name="Start">Cylinder start position</param>
        /// <param name="End">Cylinder end position</param>
        /// <param name="Radius">Cylinder radius</param>
        /// <param name="Segments">Cylinder segments count</param>
        /// <param name="Color">Color of the cylinder</param>
        /// <param name="LifeTime">Cylinder life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawCylinder(Vector3 Start, Vector3 End, float Radius, int Segments, Color Color,
            float LifeTime = 0.0f)
        {
            Vector3 Center = (Start + End) / 2.0f;
            InternalDrawCylinder(Start, End, Quaternion.identity, Center, Radius, Segments, Color, LifeTime);
        }

        /// <summary>
        /// Draw a 3D cone
        /// </summary>
        /// <param name="Position">Cone position</param>
        /// <param name="Direction">Cone direction</param>
        /// <param name="Length">Cone length</param>
        /// <param name="AngleWidth">Cone angle width in degrees, clamped to between 0 and 180</param>
        /// <param name="AngleHeight">Cone angle height in degrees, clamped to between 0 and 180</param>
        /// <param name="Segments">Cone segments count</param>
        /// <param name="Color">Cone color</param>
        /// <param name="LifeTime">Cone life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawCone(Vector3 Position, Vector3 Direction, float Length, float AngleWidth, float AngleHeight,
            int Segments, Color Color, float LifeTime = 0.0f)
        {
            if (DrawDebugTools.Instance == null)
                return;

            Segments = Mathf.Max(Segments, 4);

            float SmallNumber = 0.001f;
            float Angle1 = Mathf.Clamp(AngleHeight * Mathf.Deg2Rad, SmallNumber, Mathf.PI - SmallNumber);
            float Angle2 = Mathf.Clamp(AngleWidth * Mathf.Deg2Rad, SmallNumber, Mathf.PI - SmallNumber);

            float SinX2 = Mathf.Sin(0.5f * Angle1);
            float SinY2 = Mathf.Sin(0.5f * Angle2);

            float SqrSinX2 = SinX2 * SinX2;
            float SqrSinY2 = SinY2 * SinY2;

            float TanX2 = Mathf.Tan(0.5f * Angle1);
            float TanY2 = Mathf.Tan(0.5f * Angle2);

            Vector3[] ConeVerts = DrawDebugTools.Instance.GetShapeScratch(Segments);

            for (int i = 0; i < Segments; i++)
            {
                float AngleFragment = (float) i / (float) (Segments);
                float ThiAngle = 2.0f * Mathf.PI * AngleFragment;
                float PhiAngle = Mathf.Atan2(Mathf.Sin(ThiAngle) * SinY2, Mathf.Cos(ThiAngle) * SinX2);
                float SinPhiAngle = Mathf.Sin(PhiAngle);
                float CosPhiAngle = Mathf.Cos(PhiAngle);
                float SqrSinPhi = SinPhiAngle * SinPhiAngle;
                float SqrCosPhi = CosPhiAngle * CosPhiAngle;

                float RSq = SqrSinX2 * SqrSinY2 / (SqrSinX2 * SqrSinPhi + SqrSinY2 * SqrCosPhi);
                float R = Mathf.Sqrt(RSq);
                float Sqr = Mathf.Sqrt(1 - RSq);
                float Alpha = R * CosPhiAngle;
                float Beta = R * SinPhiAngle;


                ConeVerts[i].x = (1 - 2 * RSq);
                ConeVerts[i].y = 2 * Sqr * Alpha;
                ConeVerts[i].z = 2 * Sqr * Beta;
            }

            Vector3 ConeDirection = Direction.normalized;

            Vector3 AngleFromDirection = Quaternion.LookRotation(ConeDirection, Vector3.up).eulerAngles -
                                         new Vector3(0.0f, 90.0f, 0.0f);
            Quaternion Q = Quaternion.Euler(new Vector3(AngleFromDirection.z, AngleFromDirection.y, -AngleFromDirection.x));
            Matrix4x4 M = Matrix4x4.TRS(Position, Q, Vector3.one * Length);

            Vector3 CurrentPoint = Vector3.zero;
            Vector3 PrevPoint = Vector3.zero;
            Vector3 FirstPoint = Vector3.zero;

            for (int i = 0; i < Segments; i++)
            {
                CurrentPoint = M.MultiplyPoint(ConeVerts[i]);
                DrawLine(Position, CurrentPoint, Color, LifeTime);

                if (i == 0)
                {
                    FirstPoint = CurrentPoint;
                }
                else
                {
                    DrawLine(PrevPoint, CurrentPoint, Color, LifeTime);
                }

                PrevPoint = CurrentPoint;
            }

            DrawLine(CurrentPoint, FirstPoint, Color, LifeTime);
        }

        /// <summary>
        /// Draw 3D text
        /// </summary>
        /// <param name="Position">Position of the text</param>
        /// <param name="Rotation">Rotation of the text</param>
        /// <param name="Text">Text string</param>
        /// <param name="Anchor">Text anchor</param>
        /// <param name="TextColor">Text color</param>
        /// <param name="TextSize">Text size</param>
        /// <param name="LifeTime">Text life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawString3D(Vector3 Position, Quaternion Rotation, string Text, TextAnchor Anchor,
            Color TextColor, float TextSize = 1.0f, float LifeTime = 0.0f)
        {
            InternalAddDebugText(Text, Anchor, Position, Rotation, TextColor, TextSize, LifeTime);
        }

        /// <summary>
        /// Draw 3D text towards camera
        /// </summary>
        /// <param name="Position">Position of the text</param>
        /// <param name="Text">Text string</param>
        /// <param name="Anchor">Text anchor</param>
        /// <param name="TextColor">Text color</param>
        /// <param name="TextSize">Text size</param>
        /// <param name="LifeTime">Text life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawString3D(Vector3 Position, string Text, TextAnchor Anchor, Color TextColor,
            float TextSize = 1.0f, float LifeTime = 0.0f)
        {
            if (Camera.main == null)
                return;

            InternalAddDebugText(Text, Anchor, Position,
                Quaternion.LookRotation((Camera.main.transform.position - Position).normalized), TextColor, TextSize,
                LifeTime);
        }

        /// <summary>
        /// Draw camera frustum
        /// </summary>
        /// <param name="Camera">Target camera</param>
        /// <param name="Color">Frustum color</param>
        /// <param name="LifeTime">Frustum life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawFrustum(Camera Camera, Color Color, float LifeTime = 0.0f)
        {
            if (DrawDebugTools.Instance == null)
                return;

            Camera SourceCamera = Camera;
            if (DrawDebugTools.Instance.DebugCameraIsActive && DrawDebugTools.Instance.MainCamera != null)
                SourceCamera = DrawDebugTools.Instance.MainCamera.GetComponent<Camera>();

            if (SourceCamera == null)
                return;

            Plane[] FrustumPlanes = GeometryUtility.CalculateFrustumPlanes(SourceCamera);
            Vector3[] FrustumCorners = DrawDebugTools.Instance.GetShapeScratch(8);
            const int NearCornerOffset = 0;
            const int FarCornerOffset = 4;

            Plane TempPlane = FrustumPlanes[1];
            FrustumPlanes[1] = FrustumPlanes[2];
            FrustumPlanes[2] = TempPlane;

            for (int i = 0; i < 4; i++)
            {
                FrustumCorners[NearCornerOffset + i] =
                    DrawDebugTools.Instance.GetIntersectionPointOfPlanes(FrustumPlanes[4], FrustumPlanes[i],
                        FrustumPlanes[(i + 1) % 4]);
                FrustumCorners[FarCornerOffset + i] =
                    DrawDebugTools.Instance.GetIntersectionPointOfPlanes(FrustumPlanes[5], FrustumPlanes[i],
                        FrustumPlanes[(i + 1) % 4]);
            }

            for (int i = 0; i < 4; i++)
            {
                InternalDrawLine(FrustumCorners[NearCornerOffset + i],
                    FrustumCorners[NearCornerOffset + (i + 1) % 4], Vector3.zero, Quaternion.identity,
                    Color, LifeTime);
                InternalDrawLine(FrustumCorners[FarCornerOffset + i],
                    FrustumCorners[FarCornerOffset + (i + 1) % 4], Vector3.zero, Quaternion.identity,
                    Color, LifeTime);
                InternalDrawLine(FrustumCorners[NearCornerOffset + i], FrustumCorners[FarCornerOffset + i],
                    Vector3.zero, Quaternion.identity, Color, LifeTime);
            }
        }

        /// <summary>
        /// Draw 3D capsule
        /// </summary>
        /// <param name="Center">Center position of the capsule</param>
        /// <param name="HalfHeight">Capsule half height</param>
        /// <param name="Radius">Capsule radius</param>
        /// <param name="Rotation">Capsule rotation</param>
        /// <param name="Color">Capsule color</param>
        /// <param name="LifeTime">Capsule life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawCapsule(Vector3 Center, float HalfHeight, float Radius, Quaternion Rotation, Color Color,
            float LifeTime = 0.0f)
        {
            int Segments = 16;

            Matrix4x4 M = Matrix4x4.TRS(Vector3.zero, Rotation, Vector3.one);

            Vector3 AxisX = M.MultiplyVector(Vector3.right);
            Vector3 AxisY = M.MultiplyVector(Vector3.up);
            Vector3 AxisZ = M.MultiplyVector(Vector3.forward);

            float HalfMaxed = Mathf.Max(HalfHeight - Radius, 0.0f);
            Vector3 TopPoint = Center + HalfMaxed * AxisY;
            Vector3 BottomPoint = Center - HalfMaxed * AxisY;

            InternalDrawCapsuleCircle(TopPoint, AxisX, AxisZ, Color, Radius, Segments, LifeTime);
            InternalDrawCapsuleCircle(BottomPoint, AxisX, AxisZ, Color, Radius, Segments, LifeTime);

            InternalDrawHalfCircle(TopPoint, AxisX, AxisY, Color, Radius, Segments, LifeTime);
            InternalDrawHalfCircle(TopPoint, AxisZ, AxisY, Color, Radius, Segments, LifeTime);

            InternalDrawHalfCircle(BottomPoint, AxisX, -AxisY, Color, Radius, Segments, LifeTime);
            InternalDrawHalfCircle(BottomPoint, AxisZ, -AxisY, Color, Radius, Segments, LifeTime);

            InternalDrawLine(TopPoint + Radius * AxisX, BottomPoint + Radius * AxisX, Vector3.zero, Quaternion.identity,
                Color, LifeTime);
            InternalDrawLine(TopPoint - Radius * AxisX, BottomPoint - Radius * AxisX, Vector3.zero, Quaternion.identity,
                Color, LifeTime);
            InternalDrawLine(TopPoint + Radius * AxisZ, BottomPoint + Radius * AxisZ, Vector3.zero, Quaternion.identity,
                Color, LifeTime);
            InternalDrawLine(TopPoint - Radius * AxisZ, BottomPoint - Radius * AxisZ, Vector3.zero, Quaternion.identity,
                Color, LifeTime);
        }

        /// <summary>
        /// Draw a 3D representation of the main camera
        /// </summary>
        /// <param name="Color">Camera shape color</param>
        /// <param name="LifeTime">Shape life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawActiveCamera(Color Color, float LifeTime = 0.0f)
        {
            if (DrawDebugTools.Instance == null)
                return;

            Camera ActiveCam = Camera.main;
            if (DrawDebugTools.Instance.DebugCameraIsActive && DrawDebugTools.Instance.MainCamera != null)
                ActiveCam = DrawDebugTools.Instance.MainCamera.GetComponent<Camera>();
            InternalDrawCamera(ActiveCam, Color, LifeTime);
        }

        /// <summary>
        /// Draw a 3D representation of a camera
        /// </summary>
        /// <param name="Cam">Camera to draw</param>
        /// <param name="Color">Camera representation color</param>
        /// <param name="LifeTime">Shape life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawCamera(Camera Cam, Color Color, float LifeTime = 0.0f)
        {
            InternalDrawCamera(Cam, Color, LifeTime);
        }

        /// <summary>
        /// Draw a representation of a Plane
        /// </summary>
        /// <param name="Position">The position where to draw the plane</param>
        /// <param name="Plane">The plane you want to draw</param>
        /// <param name="PlaneSize">Size of the drawn grid that represent the plane</param>
        /// <param name="LifeTime">How long the plane will be visible on screen</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawPlane(Vector3 Position, Plane Plane, float PlaneSize = 1.0f, float LifeTime = 0.0f)
        {
            DrawDirectionalArrow(Position, Position + Plane.normal * PlaneSize, PlaneSize * 0.2f, Color.white, LifeTime);
            DrawGrid(Position, Plane.normal, PlaneSize, PlaneSize, LifeTime);
        }

        /// <summary>
        /// Draw a grid in 3D space
        /// </summary>
        /// <param name="Position">Position of the grid</param>
        /// <param name="GridSize">Grid size</param>
        /// <param name="CellSize">Grid cell size</param>
        /// <param name="LifeTime">Grid life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawGrid(Vector3 Position, float GridSize, float CellSize, float LifeTime = 0.0f)
        {
            DrawGrid(Position, Vector3.up, GridSize, CellSize, LifeTime);
        }

        /// <summary>
        /// Draw a grid in 3D space
        /// </summary>
        /// <param name="Position">Position of the grid</param>
        /// <param name="Normal">Normal vector of the grid</param>
        /// <param name="GridSize">Grid size</param>
        /// <param name="CellSize">Grid cell size</param>
        /// <param name="LifeTime">Grid life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawGrid(Vector3 Position, Vector3 Normal, float GridSize, float CellSize, float LifeTime = 0.0f)
        {
            if (DrawDebugTools.Instance == null)
                return;

            Color MajorLinesColor = new Color(0.8f, 0.8f, 0.8f, 1.0f);
            Color OtherLinesColor = new Color(0.6f, 0.6f, 0.6f, 1.0f);
            float HalfGridSize = GridSize / 2.0f;
            Quaternion GridRot = Quaternion.LookRotation(Normal);

            // Draw rectangle
            InternalDrawLine(new Vector3(Position.x - HalfGridSize, Position.y - HalfGridSize, Position.z),
                new Vector3(Position.x - HalfGridSize, Position.y + HalfGridSize, Position.z),
                Position, GridRot, MajorLinesColor, LifeTime);

            InternalDrawLine(new Vector3(Position.x - HalfGridSize, Position.y + HalfGridSize, Position.z),
                new Vector3(Position.x + HalfGridSize, Position.y + HalfGridSize, Position.z),
                Position, GridRot, MajorLinesColor, LifeTime);

            InternalDrawLine(new Vector3(Position.x + HalfGridSize, Position.y + HalfGridSize, Position.z),
                new Vector3(Position.x + HalfGridSize, Position.y - HalfGridSize, Position.z),
                Position, GridRot, MajorLinesColor, LifeTime);

            InternalDrawLine(new Vector3(Position.x + HalfGridSize, Position.y - HalfGridSize, Position.z),
                new Vector3(Position.x - HalfGridSize, Position.y - HalfGridSize, Position.z),
                Position, GridRot, MajorLinesColor, LifeTime);

            // Draw centered axis
            InternalDrawLine(new Vector3(Position.x - HalfGridSize, Position.y, Position.z),
                new Vector3(Position.x + HalfGridSize, Position.y, Position.z),
                Position, GridRot, new Color(0.8f, 0.3f, 0.3f, 1.0f), LifeTime);
            InternalDrawLine(new Vector3(Position.x, Position.y - HalfGridSize, Position.z),
                new Vector3(Position.x, Position.y + HalfGridSize, Position.z),
                Position, GridRot, new Color(0.3f, 0.3f, 0.8f, 1.0f), LifeTime);

            int CellNum = (int) Mathf.Ceil((GridSize / CellSize) / 2.0f) - 1;

            // Draw grid lines
            for (int i = -CellNum; i <= CellNum; i++)
            {
                if (i == 0) continue;
                Vector3 V1 = new Vector3(Position.x + i * (CellSize), Position.y - HalfGridSize, Position.z);
                Vector3 V2 = new Vector3(Position.x + i * (CellSize), Position.y + HalfGridSize, Position.z);
                InternalDrawLine(V1, V2, Position, GridRot, OtherLinesColor, LifeTime);

                V1 = new Vector3(Position.x - HalfGridSize, Position.y + i * (CellSize), Position.z);
                V2 = new Vector3(Position.x + HalfGridSize, Position.y + i * (CellSize), Position.z);
                InternalDrawLine(V1, V2, Position, GridRot, OtherLinesColor, LifeTime);
            }

        }

        /// <summary>
        /// Draw a measure tool for the distance between two points
        /// </summary>
        /// <param name="Start">Start position</param>
        /// <param name="End">End position</param>
        /// <param name="Color">Color of the measure tool</param>
        /// <param name="TextSize">Size of the distance label</param>
        /// <param name="LifeTime">Draw life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawDistance(Vector3 Start, Vector3 End, Color Color, float TextSize = 1.0f,
            float LifeTime = 0.0f)
        {
            float Dist = Vector3.Distance(Start, End);
            Vector3 DistTextPos = (Start + End) / 2.0f;
            float DistEndSize = 0.3f;
            InternalDrawLine(Start, End, DistTextPos, Quaternion.identity, Color, LifeTime);

            Vector3 DistDir;
            Vector3 RightDir;
            Vector3 UpDir;
            InternalGetDirectionBasis(End - Start, out DistDir, out RightDir, out UpDir);

            InternalDrawLine(Start - RightDir * DistEndSize, Start + RightDir * DistEndSize, DistTextPos,
                Quaternion.identity, Color, LifeTime);
            InternalDrawLine(End - RightDir * DistEndSize, End + RightDir * DistEndSize, DistTextPos, Quaternion.identity,
                Color, LifeTime);

            if (Camera.main == null)
                return;

            DrawString3D(DistTextPos, Quaternion.LookRotation(Camera.main.transform.position - DistTextPos),
                Dist.ToString("F2"), TextAnchor.MiddleCenter, Color.white, TextSize, LifeTime);
        }

        /// <summary>
        /// Draw a ray and its RaycastHit: impact point, surface normal, distance and the collider hit.
        /// A ray that hits nothing is drawn to its full length.
        /// </summary>
        /// <param name="Origin">The starting point of the ray in world coordinates</param>
        /// <param name="Direction">The direction of the ray</param>
        /// <param name="MaxDistance">The max distance the ray should check for collisions</param>
        /// <param name="HitInfos">Information about where the closest collider was hit</param>
        /// <param name="LifeTime">Draw life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawRaycastHit(Vector3 Origin, Vector3 Direction,
            float MaxDistance, RaycastHit HitInfos, float LifeTime = 0.0f)
        {
            Vector3 Dir = Direction.normalized;
            bool IsInfinite = float.IsInfinity(MaxDistance) || float.IsNaN(MaxDistance);

            if (HitInfos.collider != null)
            {
                Color RestColor = RayDetailColor;
                RestColor.a = 0.25f;
                Color OutlineColor = RayDetailColor;
                OutlineColor.a = 0.7f;
                Color BoundsColor = RayDetailColor;
                BoundsColor.a = 0.2f;

                // Draws the ray to the impact, and fades the rest of its length
                DrawCircle(Origin, Quaternion.FromToRotation(Vector3.up, Dir), 0.1f, 12, RayHitColor, LifeTime);
                DrawLine(Origin, HitInfos.point, RayHitColor, LifeTime);
                if (!IsInfinite && MaxDistance > HitInfos.distance)
                    DrawLine(HitInfos.point, Origin + Dir * MaxDistance, RestColor, LifeTime);

                // Marks the impact with rings on the hit surface and draws the normal
                Quaternion SurfaceRotation = Quaternion.FromToRotation(Vector3.up, HitInfos.normal);
                Vector3 RingCenter = HitInfos.point + HitInfos.normal * 0.01f;
                DrawCircle(RingCenter, SurfaceRotation, 0.15f, 16, RayHitColor, LifeTime);
                DrawCircle(RingCenter, SurfaceRotation, 0.3f, 24, RayHitColor, LifeTime);
                Vector3 NormalTip = HitInfos.point + HitInfos.normal;
                DrawDirectionalArrow(HitInfos.point, NormalTip, 0.15f, RayDetailColor, LifeTime);

                // Outlines the collider and its bounds, and labels the distance and collider name
                InternalDrawCollider(HitInfos.collider, OutlineColor, LifeTime);
                DrawBounds(HitInfos.collider.bounds, BoundsColor, LifeTime);
                DrawString3D((Origin + HitInfos.point) / 2.0f, HitInfos.distance.ToString("F2") + " m",
                    TextAnchor.LowerCenter, RayLabelColor, 1.0f, LifeTime);
                DrawString3D(NormalTip + HitInfos.normal * 0.1f, HitInfos.collider.name, TextAnchor.LowerCenter,
                    RayDetailColor, 0.8f, LifeTime);
            }
            else
            {
                // Draws the ray to its full length, capped with a ring
                Vector3 End = Origin + Dir * (IsInfinite ? InfiniteRayDisplayLength : MaxDistance);
                Quaternion RayRotation = Quaternion.FromToRotation(Vector3.up, Dir);
                DrawCircle(Origin, RayRotation, 0.1f, 12, RayMissColor, LifeTime);
                DrawLine(Origin, End, RayMissColor, LifeTime);
                DrawCircle(End, RayRotation, 0.15f, 16, RayMissColor, LifeTime);
            }
        }

        /// <summary>
        /// Log message text on screen
        /// </summary>
        /// <param name="LogMessage">Log message string</param>
        /// <param name="Color">Log message color</param>
        /// <param name="LifeTime">Log life time in scaled seconds, so it pauses with Time.timeScale</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void Log(string LogMessage, Color Color, float LifeTime = 0.0f)
        {
            if (DrawDebugTools.Instance == null)
                return;
            DrawDebugTools.Instance.m_LogMessagesList.Add(new DebugLogMessage(LogMessage, Color, LifeTime));
        }

        /// <summary>
        /// Simplified function to log message text on screen
        /// </summary>
        /// <param name="LogMessage">Log message string</param>
        /// <param name="LifeTime">Log life time in scaled seconds, so it pauses with Time.timeScale</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void Log(string LogMessage, float LifeTime = 0.0f)
        {
            if (DrawDebugTools.Instance == null)
                return;
            DrawDebugTools.Instance.m_LogMessagesList.Add(new DebugLogMessage(LogMessage, Color.white, LifeTime));
        }

        /// <summary>
        /// Draw a float graph on screen
        /// </summary>
        /// <param name="UniqueGraphName">A unique name for the graph</param>
        /// <param name="FloatValueToDebug">Float variable to debug</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawFloatGraph(string UniqueGraphName, float FloatValueToDebug)
        {
            DrawFloatGraph(UniqueGraphName, FloatValueToDebug, Color.white);
        }

        /// <summary>
        /// Draw a float graph on screen
        /// </summary>
        /// <param name="UniqueGraphName">A unique name for the graph</param>
        /// <param name="FloatValueToDebug">Float variable to debug</param>
        /// <param name="GraphColor">Color of the graph line</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawFloatGraph(string UniqueGraphName, float FloatValueToDebug, Color GraphColor)
        {
            if (DrawDebugTools.Instance == null)
                return;

            float TimeBeforeRemoveInactiveGraph = 2.0f;

            DebugFloatGraph Graph;
            if (!Instance.m_FloatGraphsByName.TryGetValue(UniqueGraphName, out Graph))
            {
                Graph = new DebugFloatGraph(UniqueGraphName, Instance.GetFloatGraphSamplesCount(), 1.0f, true,
                    TimeBeforeRemoveInactiveGraph, GraphColor);
                Instance.m_FloatGraphsList.Add(Graph);
                Instance.m_FloatGraphsByName.Add(UniqueGraphName, Graph);
            }

            Graph.AddValue(FloatValueToDebug);
        }

        /// <summary>
        /// Draw the colliders on a game object and its children
        /// </summary>
        /// <param name="Object">Game object that its colliders will be drawn</param>
        /// <param name="Color">Colliders color</param>
        /// <param name="LifeTime">Drawing time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawObjectColliders(GameObject Object, Color Color, float LifeTime = 0.0f)
        {
            if (DrawDebugTools.Instance == null)
                return;

            List<Collider> Colliders = DrawDebugTools.Instance.m_ColliderScratch;
            Object.GetComponentsInChildren(Colliders);

            for (int i = 0; i < Colliders.Count; i++)
            {
                InternalDrawCollider(Colliders[i], Color, LifeTime);
            }
        }

        /// <summary>
        /// Draw bounds
        /// </summary>
        /// <param name="InBounds">Bounds to draw</param>
        /// <param name="Color">Drawing color</param>
        /// <param name="LifeTime">Draw life time</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void DrawBounds(Bounds InBounds, Color Color, float LifeTime = 0.0f)
        {
            DrawBox(InBounds.center, Quaternion.identity, InBounds.size, Color, LifeTime);
        }

        /// <summary>
        /// Draw quad with texture, used mainly for billboards. Do not call in Update.
        /// </summary>
        /// <param name="Billboard">Quad object to draw</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void AddBillboardToDrawList(DebugBillboard Billboard)
        {
            if (DrawDebugTools.Instance == null)
                return;

            if (DrawDebugTools.Instance.m_DebugBillboardsList.Count != 0 &&
                DrawDebugTools.Instance.m_DebugBillboardsList.Contains(Billboard))
                return;

            DrawDebugTools.Instance.m_DebugBillboardsList.Add(Billboard);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        [System.Diagnostics.Conditional("DDT_ENABLED")]
        public static void RemoveBillboardFromList(DebugBillboard Billboard)
        {
            if (DrawDebugTools.Instance == null)
                return;

            if (DrawDebugTools.Instance.m_DebugBillboardsList.Count > 0)
            {
                DrawDebugTools.Instance.m_DebugBillboardsList.Remove(Billboard);
            }
        }

        #region Private Internal Functions

        private static void InternalDrawCollider(Collider ColliderItem, Color Color, float LifeTime = 0.0f)
        {
            if (ColliderItem == null)
                return;

            Transform ColliderTransform = ColliderItem.transform;
            Vector3 WorldScale = ColliderTransform.lossyScale;

            BoxCollider BoxColliderComp = ColliderItem as BoxCollider;
            if (BoxColliderComp != null)
            {
                Vector3 ColliderPos = ColliderTransform.TransformPoint(BoxColliderComp.center);
                Vector3 BoxSize = new Vector3(BoxColliderComp.size.x * WorldScale.x,
                    BoxColliderComp.size.y * WorldScale.y,
                    BoxColliderComp.size.z * WorldScale.z);

                DrawBox(ColliderPos, ColliderTransform.rotation, BoxSize, Color, LifeTime);
                return;
            }

            SphereCollider SphereColliderComp = ColliderItem as SphereCollider;
            if (SphereColliderComp != null)
            {
                Vector3 ColliderPos = ColliderTransform.TransformPoint(SphereColliderComp.center);
                float ScaledRadius = SphereColliderComp.bounds.extents.x;

                DrawSphere(ColliderPos, ColliderTransform.rotation, ScaledRadius, 8, Color, LifeTime);
                return;
            }

            CapsuleCollider CapsuleColliderComp = ColliderItem as CapsuleCollider;
            if (CapsuleColliderComp != null)
            {
                Vector3 ColliderPos = ColliderTransform.TransformPoint(CapsuleColliderComp.center);

                // direction is 0 for X, 1 for Y, 2 for Z. DrawCapsule draws along the y axis of
                // the rotation it is given. Height scales with the axis the capsule points
                // along, radius with the larger of the other two.
                Quaternion AxisRotation;
                float HeightScale;
                float RadiusScale;

                switch (CapsuleColliderComp.direction)
                {
                    case 0:
                        AxisRotation = Quaternion.Euler(0.0f, 0.0f, -90.0f);
                        HeightScale = Mathf.Abs(WorldScale.x);
                        RadiusScale = Mathf.Max(Mathf.Abs(WorldScale.y), Mathf.Abs(WorldScale.z));
                        break;

                    case 2:
                        AxisRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
                        HeightScale = Mathf.Abs(WorldScale.z);
                        RadiusScale = Mathf.Max(Mathf.Abs(WorldScale.x), Mathf.Abs(WorldScale.y));
                        break;

                    default:
                        AxisRotation = Quaternion.identity;
                        HeightScale = Mathf.Abs(WorldScale.y);
                        RadiusScale = Mathf.Max(Mathf.Abs(WorldScale.x), Mathf.Abs(WorldScale.z));
                        break;
                }

                float ScaledRadius = CapsuleColliderComp.radius * RadiusScale;
                float ScaledHeight = CapsuleColliderComp.height * 0.5f * HeightScale;

                DrawCapsule(ColliderPos, ScaledHeight, ScaledRadius,
                    ColliderTransform.rotation * AxisRotation, Color, LifeTime);
                return;
            }

            // Mesh and terrain colliders are drawn as their world bounds
            DrawBounds(ColliderItem.bounds, Color, LifeTime);
        }

        /// <summary>
        /// Builds an orthonormal basis around Direction. Right and Up are perpendicular to
        /// Direction and to each other, including when Direction is parallel to world up.
        /// </summary>
        private static void InternalGetDirectionBasis(Vector3 Direction, out Vector3 Forward, out Vector3 Right,
            out Vector3 Up)
        {
            Forward = Direction.sqrMagnitude > 0.0f ? Direction.normalized : Vector3.forward;

            Vector3 Reference = Mathf.Abs(Vector3.Dot(Forward, Vector3.up)) > 0.999f ? Vector3.right : Vector3.up;
            Right = Vector3.Cross(Reference, Forward).normalized;
            Up = Vector3.Cross(Forward, Right).normalized;
        }

        private static void InternalDrawLine(Vector3 LineStart, Vector3 LineEnd, Vector3 Center, Quaternion Rotation,
            Color Color, float LifeTime = 0.0f)
        {
            if (DrawDebugTools.Instance == null)
                return;
            DrawDebugTools.Instance.AddLine(new BatchedLine(LineStart, LineEnd, Center, Rotation, Color, LifeTime));
        }

        private static void InternalDrawCapsuleCircle(Vector3 Base, Vector3 X, Vector3 Z, Color Color, float Radius, int Segments, float LifeTime = 0.0f)
        {
            if (DrawDebugTools.Instance == null)
                return;

            float AngleDelta = 2.0f * Mathf.PI / Segments;
            Vector3 LastPoint = Base + X * Radius;

            for (int i = 0; i < Segments; i++)
            {
                Vector3 Point = Base + (X * Mathf.Cos(AngleDelta * (i + 1)) + Z * Mathf.Sin(AngleDelta * (i + 1))) * Radius;
                DrawDebugTools.InternalDrawLine(LastPoint, Point, Base, Quaternion.identity, Color, LifeTime);
                LastPoint = Point;
            }
        }

        private static void InternalDrawCamera(Camera Camera, Color Color, float LifeTime = 0.0f)
        {
            if (DrawDebugTools.Instance == null)
                return;

            if (Camera == null) return;

            float CamBoxDepth = 0.4f;
            float CamBoxHeight = 0.3f;
            float CamBoxWidth = 0.2f;
            float CamCylRadius = 0.25f;
            float CamCylDistance = 0.55f;

            Vector3 CamPos = Camera.transform.position - Camera.transform.forward * CamBoxDepth;
            Quaternion CamRot = Camera.transform.rotation;
            DrawDebugTools.DrawSphere(Camera.transform.position, 0.05f, 4, Color, LifeTime);
            // Box
            DrawDebugTools.DrawBox(CamPos, CamRot, new Vector3(CamBoxWidth, CamBoxHeight, CamBoxDepth) * 2.0f, Color,
                LifeTime);

            // Two cylinders
            Vector3 V1 = CamPos + new Vector3(CamBoxWidth / 2.0f, (CamBoxHeight) + CamCylRadius, -CamCylDistance / 2.0f);
            Vector3 V2 = CamPos + new Vector3(-CamBoxWidth / 2.0f, (CamBoxHeight) + CamCylRadius, -CamCylDistance / 2.0f);

            InternalDrawCylinder(V1, V2, CamRot, CamPos, CamCylRadius, 8, Color, LifeTime);
            V1 += new Vector3(0.0f, 0.0f, CamCylDistance);
            V2 += new Vector3(0.0f, 0.0f, CamCylDistance);
            InternalDrawCylinder(V1, V2, CamRot, CamPos, CamCylRadius, 8, Color, LifeTime);

            // Zoom
            Vector3 Extent = new Vector3(CamBoxWidth * 0.7f, CamBoxHeight * 0.7f, CamBoxDepth * 0.7f);
            Vector3 Center = CamPos + new Vector3(0.0f, 0.0f, CamBoxDepth);

            InternalDrawLine(Center + new Vector3(Extent.x, Extent.y, 0.0f),
                Center + new Vector3(Extent.x, -Extent.y, 0.0f), CamPos, CamRot, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(Extent.x, -Extent.y, 0.0f),
                Center + new Vector3(-Extent.x, -Extent.y, 0.0f), CamPos, CamRot, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(-Extent.x, -Extent.y, 0.0f),
                Center + new Vector3(-Extent.x, Extent.y, 0.0f), CamPos, CamRot, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(-Extent.x, Extent.y, 0.0f),
                Center + new Vector3(Extent.x, Extent.y, 0.0f), CamPos, CamRot, Color, LifeTime);

            float ZoomDepth = CamBoxDepth;
            float v = 3.0f;
            InternalDrawLine(Center + new Vector3(Extent.x * v, Extent.y * v, ZoomDepth),
                Center + new Vector3(Extent.x * v, -Extent.y * v, ZoomDepth), CamPos, CamRot, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(Extent.x * v, -Extent.y * v, ZoomDepth),
                Center + new Vector3(-Extent.x * v, -Extent.y * v, ZoomDepth), CamPos, CamRot, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(-Extent.x * v, -Extent.y * v, ZoomDepth),
                Center + new Vector3(-Extent.x * v, Extent.y * v, ZoomDepth), CamPos, CamRot, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(-Extent.x * v, Extent.y * v, ZoomDepth),
                Center + new Vector3(Extent.x * v, Extent.y * v, ZoomDepth), CamPos, CamRot, Color, LifeTime);

            InternalDrawLine(Center + new Vector3(Extent.x, Extent.y, 0.0f),
                Center + new Vector3(Extent.x * v, Extent.y * v, ZoomDepth), CamPos, CamRot, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(Extent.x, -Extent.y, 0.0f),
                Center + new Vector3(Extent.x * v, -Extent.y * v, ZoomDepth), CamPos, CamRot, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(-Extent.x, -Extent.y, 0.0f),
                Center + new Vector3(-Extent.x * v, -Extent.y * v, ZoomDepth), CamPos, CamRot, Color, LifeTime);
            InternalDrawLine(Center + new Vector3(-Extent.x, Extent.y, 0.0f),
                Center + new Vector3(-Extent.x * v, Extent.y * v, ZoomDepth), CamPos, CamRot, Color, LifeTime);
        }

        private static void InternalDrawCylinder(Vector3 Start, Vector3 End, Quaternion Rotation, Vector3 Center,
            float Radius, int Segments, Color Color, float LifeTime = 0.0f)
        {
            if (DrawDebugTools.Instance == null)
                return;

            Segments = Mathf.Max(Segments, 4);

            Vector3 CylinderUp;
            Vector3 CylinderRight;
            Vector3 CylinderForward;
            InternalGetDirectionBasis(End - Start, out CylinderUp, out CylinderRight, out CylinderForward);
            float CylinderHeight = (End - Start).magnitude;

            float AngleInc = 2.0f * Mathf.PI / (float) Segments;

            // Debug End
            float Angle = 0.0f;
            Vector3 P_1;
            Vector3 P_2;
            Vector3 P_3;
            Vector3 P_4;

            Vector3 RotatedVect;
            for (int i = 0; i < Segments; i++)
            {
                RotatedVect = Quaternion.AngleAxis(Mathf.Rad2Deg * Angle, CylinderUp) * CylinderRight * Radius;

                P_1 = Start + RotatedVect;
                P_2 = P_1 + CylinderUp * CylinderHeight;

                // Draw lines
                DrawDebugTools.InternalDrawLine(P_1, P_2, Center, Rotation, Color, LifeTime);

                Angle += AngleInc;
                RotatedVect = Quaternion.AngleAxis(Mathf.Rad2Deg * Angle, CylinderUp) * CylinderRight * Radius;

                P_3 = Start + RotatedVect;
                P_4 = P_3 + CylinderUp * CylinderHeight;

                // Draw lines
                DrawDebugTools.InternalDrawLine(P_1, P_3, Center, Rotation, Color, LifeTime);
                DrawDebugTools.InternalDrawLine(P_2, P_4, Center, Rotation, Color, LifeTime);
            }
        }

        private static void InternalAddDebugText(string Text, TextAnchor Anchor, Vector3 Position, Quaternion Rotation,
            Color Color, float Size, float LifeTime)
        {
            if (DrawDebugTools.Instance == null)
                return;

            DrawDebugTools.Instance.m_DebugTextsList.Add(new DebugText(Text, Anchor, Position, Rotation, Color, Size,
                LifeTime));
        }

        private static void InternalDrawHalfCircle(Vector3 Base, Vector3 X, Vector3 Z, Color Color, float Radius, int Segments, float LifeTime = 0.0f)
        {
            if (DrawDebugTools.Instance == null)
                return;

            float AngleDelta = 2.0f * Mathf.PI / Segments;
            Vector3 LastPoint = Base + X * Radius;

            for (int i = 0; i < (Segments / 2); i++)
            {
                Vector3 Point = Base + (X * Mathf.Cos(AngleDelta * (i + 1)) + Z * Mathf.Sin(AngleDelta * (i + 1))) * Radius;
                DrawDebugTools.InternalDrawLine(LastPoint, Point, Base, Quaternion.identity, Color, LifeTime);
                LastPoint = Point;
            }
        }

        #endregion

        #endregion

        #region ========== Handle Drawing Lines/Quads ==========

        /// <summary>Fills the line mesh for this frame. False when there is nothing to draw.</summary>
        private bool HandleDrawingListOfLines()
        {
            int LineCount = m_TransientLineCount + m_PersistentLineCount;
            if (LineCount == 0)
                return false;

            // Check material is set
            if (!m_LineMaterial)
            {
                InitializeMaterials();

                if (!m_LineMaterial)
                    return false;
            }

            EnsureMeshes();
            EnsureMeshCapacity(LineCount);

            m_MeshPositions.Clear();
            m_MeshColors.Clear();

            Vector3 BoundsMin = m_TransientLineCount > 0 ? m_TransientLines[0].Start : m_PersistentLines[0].Start;
            Vector3 BoundsMax = BoundsMin;

            AppendLinesToMesh(m_TransientLines, m_TransientLineCount, ref BoundsMin, ref BoundsMax);
            AppendLinesToMesh(m_PersistentLines, m_PersistentLineCount, ref BoundsMin, ref BoundsMax);

            int VertexCount = LineCount * 2;

            m_Mesh.Clear(true);
            m_Mesh.indexFormat = IndexFormat.UInt32;
            m_Mesh.SetVertices(m_MeshPositions, 0, VertexCount);
            m_Mesh.SetColors(m_MeshColors, 0, VertexCount);
            m_Mesh.SetIndices(m_MeshIndices, 0, VertexCount, MeshTopology.Lines, 0, false);
            m_Mesh.bounds = new Bounds((BoundsMin + BoundsMax) * 0.5f, BoundsMax - BoundsMin);

            // Transient lines live for one frame
            m_TransientLineCount = 0;

            // Update lines
            for (int i = m_PersistentLineCount - 1; i >= 0; i--)
            {
                m_PersistentLines[i].RemainLifeTime -= Time.deltaTime;
                if (m_PersistentLines[i].RemainLifeTime <= 0.0f)
                {
                    m_PersistentLineCount--;
                    m_PersistentLines[i] = m_PersistentLines[m_PersistentLineCount];
                }
            }

            return true;
        }

        /// <summary>Copies a line buffer into the mesh lists, growing the drawn bounds to fit.</summary>
        private void AppendLinesToMesh(BatchedLine[] Lines, int Count, ref Vector3 BoundsMin, ref Vector3 BoundsMax)
        {
            for (int i = 0; i < Count; i++)
            {
                Vector3 Start = Lines[i].Start;
                Vector3 End = Lines[i].End;
                Color32 Color = Lines[i].Color;

                m_MeshPositions.Add(Start);
                m_MeshPositions.Add(End);
                m_MeshColors.Add(Color);
                m_MeshColors.Add(Color);

                BoundsMin = Vector3.Min(BoundsMin, Vector3.Min(Start, End));
                BoundsMax = Vector3.Max(BoundsMax, Vector3.Max(Start, End));
            }
        }

        /// <summary>
        /// Grows the mesh scratch buffers to hold LineCount lines and numbers the indices
        /// added by the growth.
        /// </summary>
        private void EnsureMeshCapacity(int LineCount)
        {
            int VertexCount = LineCount * 2;

            if (m_MeshPositions.Capacity < VertexCount)
                m_MeshPositions.Capacity = VertexCount;

            if (m_MeshColors.Capacity < VertexCount)
                m_MeshColors.Capacity = VertexCount;

            if (m_MeshIndices.Length < VertexCount)
            {
                int PreviousLength = m_MeshIndices.Length;
                int NewLength = Mathf.Max(VertexCount, InitialLineCapacity * 2);
                System.Array.Resize(ref m_MeshIndices, NewLength);

                for (int i = PreviousLength; i < NewLength; i++)
                {
                    m_MeshIndices[i] = i;
                }
            }
        }

        private void HandleDrawingListOfBillboards()
        {
            if (m_DebugBillboardsList.Count == 0)
                return;

            // Check material is set
            if (!m_QuadMaterial)
            {
                InitializeMaterials();

                if (!m_QuadMaterial)
                    return;
            }

            EnsureMeshes();
            BuildBillboardMesh();

            for (int i = 0; i < m_DebugBillboardsList.Count; i++)
            {
                DebugBillboard Billboard = m_DebugBillboardsList[i];
                if (Billboard == null || Billboard.IsHidden)
                {
                    continue;
                }

                // An untextured billboard falls back to the material default
                m_QuadMatPropertyBlock.Clear();
                if (Billboard.QuadTexture)
                {
                    m_QuadMatPropertyBlock.SetTexture("_MainTex", Billboard.QuadTexture);
                }

                Quaternion Rotation = Quaternion.Euler(Billboard.EulerRotation);
                if (Billboard.IsBillboard)
                {
                    Rotation = GetCamLookAtRotation(Billboard.Position, true);
                }

                // Width and height scale the unit quad
                Matrix4x4 BillboardTransform = Matrix4x4.TRS(Billboard.Position, Rotation,
                    new Vector3(Billboard.Width, Billboard.Height, 1.0f));

                Graphics.DrawMesh(m_BillboardMesh, BillboardTransform, m_QuadMaterial, GetDrawLayer(),
                    null, 0, m_QuadMatPropertyBlock);
            }
        }

        /// <summary>Builds the shared unit quad spanning 0,0 to 1,1 on the XY plane, once.</summary>
        private void BuildBillboardMesh()
        {
            if (m_BillboardMesh.vertexCount != 0)
                return;

            m_BillboardMesh.SetVertices(new List<Vector3>
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(1.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 1.0f, 0.0f),
                new Vector3(1.0f, 1.0f, 0.0f)
            });

            m_BillboardMesh.SetIndices(new[]
            {
                // lower left triangle
                0, 2, 1,
                // upper right triangle
                2, 3, 1
            }, MeshTopology.Triangles, 0);

            m_BillboardMesh.normals = new[]
            {
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward,
                -Vector3.forward
            };

            m_BillboardMesh.uv = new[]
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(1.0f, 0.0f),
                new Vector2(0.0f, 1.0f),
                new Vector2(1.0f, 1.0f)
            };
        }

        public void AddLine(BatchedLine Line)
        {
            if (Line.RemainLifeTime > 0.0f)
                AppendLine(ref m_PersistentLines, ref m_PersistentLineCount, Line);
            else
                AppendLine(ref m_TransientLines, ref m_TransientLineCount, Line);
        }

        public void AddRangeLine(List<BatchedLine> LinesList)
        {
            for (int i = 0; i < LinesList.Count; i++)
            {
                AddLine(LinesList[i]);
            }
        }

        /// <summary>
        /// A reusable point buffer for shapes that need one while they are being generated.
        /// Valid only for the duration of a single draw call.
        /// </summary>
        private Vector3[] GetShapeScratch(int Count)
        {
            if (m_ShapeScratch.Length < Count)
                System.Array.Resize(ref m_ShapeScratch, Count);

            return m_ShapeScratch;
        }

        private static void AppendLine(ref BatchedLine[] Buffer, ref int Count, BatchedLine Line)
        {
            if (Count == Buffer.Length)
                System.Array.Resize(ref Buffer, Buffer.Length * 2);

            Buffer[Count] = Line;
            Count++;
        }

        private void HandleDrawingListOfTexts()
        {
            if (m_DebugTextsList.Count == 0 && m_3DTextsList.Count == 0)
                return;

            if (m_3DTextsParent == null)
            {
                m_3DTextsParent = new GameObject("3DTexts");
                m_3DTextsParent.transform.SetParent(transform);
                m_3DTextsParent.transform.localPosition = Vector3.zero;
            }

            // Grow the pool to cover this frame's texts
            while (m_3DTextsList.Count < m_DebugTextsList.Count)
            {
                m_3DTextsList.Add(Instantiate3DText());
            }

            for (int i = 0; i < m_3DTextsList.Count; i++)
            {
                TextMesh Text = m_3DTextsList[i];

                // Pooled entries past the current count stay for the next frame, hidden
                if (i >= m_DebugTextsList.Count)
                {
                    if (Text.gameObject.activeSelf)
                        Text.gameObject.SetActive(false);
                    continue;
                }

                DebugText Source = m_DebugTextsList[i];

                if (!Text.gameObject.activeSelf)
                    Text.gameObject.SetActive(true);

                Text.text = Source.m_TextString;
                Text.transform.position = Source.m_TextPosition;
                Text.transform.rotation = Source.m_TextRotation;

                // Negative x mirrors the glyphs back for the camera-facing rotation
                Text.transform.localScale = new Vector3(-DefaultTextScale, DefaultTextScale, DefaultTextScale)
                                            * Mathf.Max(Source.m_Size, 0.0001f);
                Text.fontSize = DefaultTextFontSize;
                Text.color = Source.m_TextColor;
                Text.anchor = Source.m_TextAnchor;
            }

            // Update text life time
            for (int i = m_DebugTextsList.Count - 1; i >= 0; i--)
            {
                m_DebugTextsList[i].m_RemainLifeTime -= Time.deltaTime;
                if (m_DebugTextsList[i].m_RemainLifeTime <= 0.0f)
                {
                    m_DebugTextsList.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Resolves the canvas, taking one already in the scene before creating its own.
        /// False when neither is available.
        /// </summary>
        private bool EnsureCanvas()
        {
            if (m_DDTCanvas != null)
                return true;

            m_DDTCanvas = FindAnyObjectByType<DDTCanvas>();
            if (m_DDTCanvas != null)
                return true;

            if (m_DDTCanvasPrefab == null)
                return false;

            m_DDTCanvas = GameObject.Instantiate(m_DDTCanvasPrefab, transform).GetComponent<DDTCanvas>();
            return m_DDTCanvas != null;
        }

        private void HandleDrawingListOfFloatGraphs()
        {
            // The canvas is only built once there is something to put on it
            if (m_FloatGraphsList.Count == 0 && m_DDTCanvas == null)
                return;

            if (!EnsureCanvas())
                return;

            m_DDTCanvas.UpdateGraphFloats(m_FloatGraphsList);

            // Update text life time
            for (int i = m_FloatGraphsList.Count - 1; i >= 0; i--)
            {
                m_FloatGraphsList[i].m_TimeBeforeRemoveCounter += Time.deltaTime;
                if (m_FloatGraphsList[i].m_TimeBeforeRemoveCounter >= m_FloatGraphsList[i].m_TimeBeforeRemove)
                {
                    m_FloatGraphsByName.Remove(m_FloatGraphsList[i].m_UniqueFloatName);
                    m_FloatGraphsList.RemoveAt(i);
                }
            }
        }

        private void HandleListOfLogMessagesList()
        {
            if (m_LogMessagesList.Count == 0 && m_DDTCanvas == null)
                return;

            if (!EnsureCanvas())
                return;

            m_DDTCanvas.UpdateLogTexts(m_LogMessagesList);

            // Update log life time
            for (int i = m_LogMessagesList.Count - 1; i >= 0; i--)
            {
                m_LogMessagesList[i].m_RemainingTime -= Time.deltaTime;
                if (m_LogMessagesList[i].m_RemainingTime <= 0.0f)
                {
                    m_LogMessagesList.RemoveAt(i);
                }
            }
        }

        private TextMesh Instantiate3DText()
        {
            GameObject TextMeshObj = GameObject.Instantiate(m_3DTextPrefab);
            TextMeshObj.transform.SetParent(m_3DTextsParent.transform);
            TextMeshObj.name = "3DText-" + TextMeshObj.GetInstanceID();
            return TextMeshObj.GetComponent<TextMesh>();
        }

        #endregion

        #region ========== Helper Functions ==========

        private Vector3 GetIntersectionPointOfPlanes(Plane Plane_1, Plane Plane_2, Plane Plane_3)
        {
            return ((-Plane_1.distance * Vector3.Cross(Plane_2.normal, Plane_3.normal)) +
                    (-Plane_2.distance * Vector3.Cross(Plane_3.normal, Plane_1.normal)) +
                    (-Plane_3.distance * Vector3.Cross(Plane_1.normal, Plane_2.normal))) /
                   (Vector3.Dot(Plane_1.normal, Vector3.Cross(Plane_2.normal, Plane_3.normal)));
        }

        public int GetFloatGraphSamplesCount()
        {
            return m_DDTSettings != null ? m_DDTSettings.m_FloatGraphSamplesCount : 100;
        }

        /// <summary>
        /// Clears the pending freeze restore when the debug camera panel sets a non-zero time
        /// scale.
        /// </summary>
        public void NotifyTimeScaleChangedByPanel()
        {
            if (!Mathf.Approximately(Time.timeScale, 0.0f))
                m_DidFreezeTime = false;
        }

        public void FlushDebugLines()
        {
            m_TransientLineCount = 0;
            m_PersistentLineCount = 0;
        }

        private Quaternion GetCamLookAtRotation(Vector3 Target, bool IsInverted = false)
        {
            if(Camera.main == null)
                return Quaternion.identity;
            return (Quaternion.LookRotation((Camera.main.transform.position - Target) * (IsInverted?-1.0f:1.0f), Vector3.up));
        }

        #endregion
    }


    #region ========== Enums / Structures / Helper Classes ==========
    /// <summary>
    /// One debug line between two world endpoints. A rotation is applied about the pivot as
    /// the line is constructed.
    /// </summary>
    [System.Serializable]
    public struct BatchedLine
    {
        public Vector3 Start;
        public Vector3 End;
        public Color32 Color;
        public float RemainLifeTime;

        public BatchedLine(Vector3 InStart, Vector3 InEnd, Vector3 InPivotPoint, Quaternion InRotation, Color InColor, float InRemainLifeTime)
        {
            if (InRotation == Quaternion.identity)
            {
                Start = InStart;
                End = InEnd;
            }
            else
            {
                Start = InRotation * (InStart - InPivotPoint) + InPivotPoint;
                End = InRotation * (InEnd - InPivotPoint) + InPivotPoint;
            }

            Color = InColor;
            RemainLifeTime = InRemainLifeTime;
        }
    };

    [System.Serializable]
    public class DebugBillboard
    {
        public Texture m_QuadTexture;
        private Vector3 m_Position;
        private Vector3 m_EulerRotation;
        private float m_Width = 1.0f;
        private float m_Height = 1.0f;
        private bool m_IsHidden = false;
        private bool m_IsBillboard = true;


        public Texture QuadTexture
        {
            get => m_QuadTexture;
            set => m_QuadTexture = value;
        }

        public Vector3 Position
        {
            get => m_Position;
            set => m_Position = value;
        }

        public Vector3 EulerRotation
        {
            get => m_EulerRotation;
            set => m_EulerRotation = value;
        }

        public float Width
        {
            get => m_Width == 0.0f ? 1.0f : m_Width;
            set => m_Width = value;
        }

        public float Height
        {
            get => m_Height == 0.0f ? 1.0f : m_Height;
            set => m_Height = value;
        }

        public bool IsHidden
        {
            get => m_IsHidden;
            set => m_IsHidden = value;
        }

        public bool IsBillboard
        {
            get => m_IsBillboard;
            set => m_IsBillboard = value;
        }

        public DebugBillboard(Texture QuadTexture)
        {
            this.m_QuadTexture = QuadTexture;
            m_Width = 1.0f;
            m_Height = 1.0f;
            m_IsHidden = true;
        }

        public DebugBillboard(Texture QuadTexture, Vector3 Position, Vector3 EulerRotation)
        {
            this.m_QuadTexture = QuadTexture;
            this.m_Position = Position;
            this.m_EulerRotation = EulerRotation;
        }

        public DebugBillboard(Texture QuadTexture, Vector3 Position, Vector3 EulerRotation, float Width = 1.0f, float Height = 1.0f)
        {
            this.m_QuadTexture = QuadTexture;
            this.m_Position = Position;
            this.m_EulerRotation = EulerRotation;
            this.m_Width = Width;
            this.m_Height = Height;
        }

        public DebugBillboard(Texture QuadTexture, Vector3 Position, Vector3 EulerRotation, float Width = 1.0f, float Height = 1.0f, bool IsHidden = true, bool IsBillboard = true)
        {
            this.m_QuadTexture = QuadTexture;
            this.m_Position = Position;
            this.m_EulerRotation = EulerRotation;
            this.m_Width = Width;
            this.m_Height = Height;
            this.m_IsHidden = IsHidden;
            this.m_IsBillboard = IsBillboard;
        }
    }

    public enum EDrawPlaneAxis
    {
        XZ,
        XY,
        YZ
    };

    [System.Serializable]
    public class DebugFloatGraph
    {
        public string m_UniqueFloatName = "";
        public int m_SamplesCount = 20;
        public List<float> m_FloatValuesList;
        public float m_GraphValueLength;
        private float m_CachedMinimum;
        private float m_CachedMaximum;
        public bool m_AutoAdjustMinMaxRange = false;
        public float m_TimeBeforeRemove;
        public float m_TimeBeforeRemoveCounter = 0.0f;
        public Color m_GraphColor;

        public DebugFloatGraph()
        {
        }

        public DebugFloatGraph(string UniqueFloatName, int SamplesCount, float GraphValueLength, bool AutoAdjustMinMaxRange, float TimeBeforeRemove, Color GraphColor)
        {
            m_UniqueFloatName = UniqueFloatName;
            m_SamplesCount = SamplesCount;
            m_FloatValuesList = new List<float>();
            m_GraphValueLength = GraphValueLength;
            m_AutoAdjustMinMaxRange = AutoAdjustMinMaxRange;
            m_TimeBeforeRemove = TimeBeforeRemove;
            m_GraphColor = GraphColor;
        }

        public void AddValue(float NewFloatVal)
        {
            m_FloatValuesList.Add(NewFloatVal);

            // Auto adjust graph length
            if (m_AutoAdjustMinMaxRange)
            {
                if (Mathf.Abs(NewFloatVal) > m_GraphValueLength)
                {
                    m_GraphValueLength = Mathf.Abs(NewFloatVal);
                }
            }

            // Remove the oldest sample once the window is full
            if (GetDidReachMaxSamples())
                m_FloatValuesList.RemoveAt(0);

            RefreshRange();

            m_TimeBeforeRemoveCounter = 0.0f;
        }

        /// <summary>Scans the sample window once and caches its range.</summary>
        private void RefreshRange()
        {
            if (m_FloatValuesList.Count == 0)
            {
                m_CachedMinimum = 0.0f;
                m_CachedMaximum = 0.0f;
                return;
            }

            float Smallest = m_FloatValuesList[0];
            float Biggest = m_FloatValuesList[0];
            for (int i = 1; i < m_FloatValuesList.Count; i++)
            {
                float Value = m_FloatValuesList[i];
                if (Value < Smallest)
                    Smallest = Value;
                if (Value > Biggest)
                    Biggest = Value;
            }

            m_CachedMinimum = Smallest;
            m_CachedMaximum = Biggest;
        }

        public bool GetDidReachMaxSamples()
        {
            return m_FloatValuesList.Count > m_SamplesCount;
        }

        public float GetMinimumValue()
        {
            return m_CachedMinimum;
        }

        public float GetMaximumValue()
        {
            return m_CachedMaximum;
        }
    }

    [System.Serializable]
    public class DebugLogMessage
    {
        public string   m_LogMessageText;
        public Color    m_Color;
        public float    m_RemainingTime;

        public DebugLogMessage(string LogMessageText, Color Color, float LifeTime)
        {
            m_LogMessageText = LogMessageText;
            m_Color = Color;
            m_RemainingTime = LifeTime;
        }
    }

    [System.Serializable]
    public class DebugText
    {
        public string m_TextString;
        public TextAnchor m_TextAnchor;
        public Vector3 m_TextPosition;
        public Quaternion m_TextRotation;
        public Color m_TextColor;
        public float m_Size;
        public float m_RemainLifeTime;

        public DebugText()
        {
        }

        public DebugText(string Text, TextAnchor TextAnchor, Vector3 TextPosition, Quaternion TextRotation, Color Color, float Size, float LifeTime)
        {
            m_TextString = Text;
            m_TextAnchor = TextAnchor;
            m_TextPosition = TextPosition;
            m_TextRotation = TextRotation;
            m_TextColor = Color;
            m_Size = Size;
            m_RemainLifeTime = LifeTime;
        }
    }

    #endregion
}
