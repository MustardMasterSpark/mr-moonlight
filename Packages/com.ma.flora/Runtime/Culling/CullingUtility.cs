// Copyright © Magnetic Arcade. All Rights Reserved.

using MA.InternalBridge;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

#if HAS_PACKAGE_UNITY_URP
using UnityEngine.Rendering.Universal;
#endif

#if HAS_PACKAGE_UNITY_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace MA.Flora
{
    internal enum StaticLightingRenderMode
    {
        None        = 0,
        LightMapped = 1,
        LightProbes = 2,
    }

    internal static class MaterialPropertyBlockPool
    {
        private static UnityEngine.Pool.ObjectPool<MaterialPropertyBlock> s_Instance = new UnityEngine.Pool.ObjectPool<MaterialPropertyBlock>(() => new MaterialPropertyBlock(), p => p.Clear());
        public static PooledObject<MaterialPropertyBlock> Get(out MaterialPropertyBlock mpb) => s_Instance.Get(out mpb);
        public static MaterialPropertyBlock Get() => s_Instance.Get();
        public static void Release(MaterialPropertyBlock mpb) => s_Instance.Release(mpb);
    }

    internal static class CullingUtility
    {
        public static bool SceneHasLightProbes()
        {
            if (LightProbesBridge.GetCount() == 0)
                return false;
            if (!FloraSystem.Active)
                return false;

            return FloraSystem.Instance.AllowLegacyLightProbes;
        }

        private static Mesh s_BillboardMesh;

        public static Mesh GetBillboardMesh()
        {
            if (s_BillboardMesh == null)
            {
                s_BillboardMesh = new Mesh();
                s_BillboardMesh.name = "Billboard Mesh";
                s_BillboardMesh.hideFlags = HideFlags.HideAndDontSave;
                s_BillboardMesh.vertices = new Vector3[6];
                s_BillboardMesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
                s_BillboardMesh.SetUVs(
                    0,
                    new[]
                    {
                        new Vector2(0,0),
                        new Vector2(0,1),
                        new Vector2(1,1),
                        new Vector2(0,0),
                        new Vector2(1,1),
                        new Vector2(1,0),
                    });
                s_BillboardMesh.SetUVs(
                    1,
                    new[]
                    {
                        new Vector4(1, 1, 0, 0),
                        new Vector4(1, 1, 0, 0),
                        new Vector4(1, 1, 0, 0),
                        new Vector4(1, 1, 0, 0),
                        new Vector4(1, 1, 0, 0),
                        new Vector4(1, 1, 0, 0),
                    });
                s_BillboardMesh.UploadMeshData(true);
            }

            return s_BillboardMesh;
        }

        private static Mesh s_TerrainDetailBillboardMesh;

        public static Mesh GetTerrainDetailBillboardMesh()
        {
            if (s_TerrainDetailBillboardMesh == null)
            {
                s_TerrainDetailBillboardMesh = new Mesh();

                const float offset = -0.1f; // move pivot 10% up from the bottom
                Vector3[] vertices =
                {
                    new Vector3(-0.5f, offset, 0),
                    new Vector3(0.5f, offset, 0),
                    new Vector3(-0.5f, 1 + offset, 0),
                    new Vector3(0.5f, 1 + offset, 0)
                };
                s_TerrainDetailBillboardMesh.vertices = vertices;

                Vector3[] normals =
                {
                    -Vector3.forward,
                    -Vector3.forward,
                    -Vector3.forward,
                    -Vector3.forward
                };
                s_TerrainDetailBillboardMesh.normals = normals;

                // Tangents (x, y = billboard extrusion axis, z = 0, w = -1)
                Vector4[] tangents =
                {
                    new Vector4(1, 0, 0, -1),
                    new Vector4(1, 0, 0, -1),
                    new Vector4(1, 0, 0, -1),
                    new Vector4(1, 0, 0, -1)
                };
                s_TerrainDetailBillboardMesh.tangents = tangents;

                Vector2[] uv =
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(0, 1),
                    new Vector2(1, 1)
                };
                s_TerrainDetailBillboardMesh.uv = uv;

                // Colors (gradient in alpha)
                Color[] colors =
                {
                    new Color(1f, 1f, 1f, 0f),
                    new Color(1f, 1f, 1f, 0f),
                    new Color(1f, 1f, 1f, 1f),
                    new Color(1f, 1f, 1f, 1f)
                };
                s_TerrainDetailBillboardMesh.colors = colors;

                int[] tris =
                {
                    0, 2, 1,
                    2, 3, 1
                };
                s_TerrainDetailBillboardMesh.triangles = tris;

                s_TerrainDetailBillboardMesh.bounds = new Bounds(Vector3.up * 0.5f, Vector3.one);
                s_TerrainDetailBillboardMesh.hideFlags = HideFlags.HideAndDontSave;
                s_TerrainDetailBillboardMesh.name = "Flora Terrain Detail Mesh";
                s_TerrainDetailBillboardMesh.UploadMeshData(true);
            }

            return s_TerrainDetailBillboardMesh;
        }

        public static ulong GetSceneCullingMaskFromCamera(Camera camera)
        {
#if UNITY_EDITOR
            if (camera.overrideSceneCullingMask != 0)
                return camera.overrideSceneCullingMask;

            if (camera.scene.IsValid())
                return UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(camera.scene);

            return camera.cameraType switch
            {
                CameraType.SceneView => UnityEditor.SceneManagement.SceneCullingMasks.MainStageSceneViewObjects,
                _                    => UnityEditor.SceneManagement.SceneCullingMasks.GameViewObjects
            };
#else
            return 0;
#endif
        }

        public static float GetMaximumShadowDistance(Camera camera)
        {
#if HAS_PACKAGE_UNITY_URP
            if (camera.TryGetComponent(out UniversalAdditionalCameraData additionalCameraData) && !additionalCameraData.renderShadows)
                return 0;
#endif

            float shadowDistance = RenderPipelineManager.currentPipeline switch
            {
#if HAS_PACKAGE_UNITY_URP
                UniversalRenderPipeline => UniversalRenderPipeline.asset?.shadowDistance ?? QualitySettings.shadowDistance,
#endif
#if HAS_PACKAGE_UNITY_HDRP
                HDRenderPipeline        => HDCamera.GetOrCreate(camera).volumeStack.GetComponent<HDShadowSettings>().maxShadowDistance.value,
#endif
                _ => QualitySettings.shadowDistance
            };

            return shadowDistance;
        }

        private const int LightmapIndexMask          = 0xffff;
        private const int LightmapIndexInfluenceOnly = 0xfffe;

        public static StaticLightingRenderMode StaticLightingModeFromRenderer(Renderer renderer)
        {
            var lightmapIndex = renderer.lightmapIndex & LightmapIndexMask;
            var staticLightingMode = lightmapIndex switch
            {
                >= LightmapIndexInfluenceOnly or < 0 => StaticLightingRenderMode.LightProbes,
                >= 0                                 => StaticLightingRenderMode.LightMapped
            };

            return staticLightingMode;
        }

        public static float CalculateMeshLodConstant(LODParameters lodParams, float screenRelativeMetric, float meshLodThreshold)
        {
            return meshLodThreshold * screenRelativeMetric / lodParams.cameraPixelHeight;
        }

        public static float CalculateFOVHalfAngle(float fieldOfView)
        {
            return math.tan(math.radians(fieldOfView) * 0.5f);
        }

        public static float CalculateScreenRelativeMetricNoBias(LODParameters lodParams)
        {
            if (lodParams.isOrthographic)
            {
                return 2.0f * lodParams.orthoSize;
            }

            // Half angle at 90 degrees is 1.0 (So we skip halfAngle / 1.0 calculation)
            float halfAngle = CalculateFOVHalfAngle(lodParams.fieldOfView);
            return 2.0f * halfAngle;
        }

        public static float CalculateLODScreenRelativeMetric(LODParameters lodParams, float lodBias)
        {
            float screenRelativeMetric;
            if (lodParams.isOrthographic)
            {
                screenRelativeMetric = 2.0f * lodParams.orthoSize;
            }
            else
            {
                // Half angle at 90 degrees is 1.0 (So we skip halfAngle / 1.0 calculation)
                screenRelativeMetric = 2.0f * CalculateFOVHalfAngle(lodParams.fieldOfView);
            }

            return screenRelativeMetric / lodBias;
        }

        public static int NumFramesInFlight
        {
            get
            {
                // The number of frames in flight at the same time
                // depends on the Graphics device that we are using.
                // This number tells how long we need to keep the buffers
                // for a given frame alive. For example, if this is 4,
                // we can reclaim the buffers for a frame after 4 frames have passed.
                int numFrames = 0;

                switch (SystemInfo.graphicsDeviceType)
                {
                    case GraphicsDeviceType.Vulkan:
                    case GraphicsDeviceType.Direct3D11:
                    case GraphicsDeviceType.Direct3D12:
                    case GraphicsDeviceType.PlayStation4:
                    case GraphicsDeviceType.PlayStation5:
                    case GraphicsDeviceType.XboxOne:
                    case GraphicsDeviceType.GameCoreXboxOne:
                    case GraphicsDeviceType.GameCoreXboxSeries:
                    case GraphicsDeviceType.OpenGLCore:
                    case GraphicsDeviceType.OpenGLES3:
                    case GraphicsDeviceType.PlayStation5NGGC:
                        numFrames = 3;
                        break;
                    case GraphicsDeviceType.Switch:
                    case GraphicsDeviceType.Metal:
                    default:
                        numFrames = 4;
                        break;
                }

                // Use at least as many frames as the quality settings have, but use a platform
                // specific lower limit in any case.
                numFrames = math.max(numFrames, QualitySettings.maxQueuedFrames);

                return numFrames;
            }
        }

        public static Bounds CalculateWorldBounds(this GameObject gameObject)
        {
            if (gameObject == null)
                return AxisAlignedBox.Empty;

            var bounds = AxisAlignedBox.Empty;
            var lods = gameObject.GetLODs();
            foreach (var lod in lods)
            {
                foreach (var renderer in lod.renderers)
                {
                    if (renderer is MeshRenderer meshRenderer)
                    {
                        bounds += meshRenderer.bounds;
                    }
                }
            }

            return bounds;
        }

        public static Bounds CalculateLocalBounds(this GameObject gameObject)
        {
            var toGameObjectSpace = gameObject.transform.worldToLocalMatrix;
            var bounds = gameObject.CalculateWorldBounds();
            return bounds.TransformBy(toGameObjectSpace);
        }
    }
}
