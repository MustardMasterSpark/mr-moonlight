using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HeurekaGames.SmartBuilder
{
    public static class SB_PreviewCacheBuilder
    {
        private class CustomPreviewItem
        {
            public string id;
            public string path;
            public GUIContent content;
        }

        private static PreviewRenderUtility previewRenderUtility;
        private static OrderedQueue<CustomPreviewItem> previewItems = new OrderedQueue<CustomPreviewItem>() { queueSize = SB_Preferences.instance.MaxSimilarItems.Value };
        private static bool isDirty;
        private static bool cleanupRunning = false;

        //[MenuItem("Tools/ClearCustomPreviews")]
        public static void ClearPreviews()
        {
            if (!cleanupRunning)
            {
                previewItems.Clear();
                StartCleanup();
            }
        }

        async static void StartCleanup()
        {
            cleanupRunning = true;
            await Task.Delay(200);
            cleanupRunning = false;
        }

        public static void SetPreviewsDirty()
        {
            isDirty = true;
        }

        internal static GUIContent GetAssetPreviewContent(SB_AssetInfo info, out bool hasPreview)
        {
            if (isDirty)
                ClearPreviews();

            if (SB_Preferences.instance.UseCustomPreview.Value)
            {
                CustomPreviewItem item;
                if (!previewItems.TryGetElement(info.id, out item))
                {
                    item = new CustomPreviewItem()
                    {
                        id = info.id,
                        path = info.path,
                        content = new GUIContent(getCustomPreview((GameObject)AssetDatabase.LoadMainAssetAtPath(info.path)))
                    };

                    previewItems.Enqueue(info.id, item);
                }

                hasPreview = item.content.image != null;
                return item.content;
            }
            else
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(info.path);
                GUIContent content = new GUIContent(AssetPreview.GetAssetPreview(asset));

                hasPreview = content.image != null;
                return content;
            }
        }

        private static Texture getCustomPreview(GameObject asset)
        {
            if (previewRenderUtility != null)
                previewRenderUtility.Cleanup();

            previewRenderUtility = new PreviewRenderUtility(true);
            System.GC.SuppressFinalize(previewRenderUtility);

            var camera = previewRenderUtility.camera;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SB_Preferences.instance.PreviewColor.Value;

            var obj = GameObject.Instantiate(asset);
            var bounds = SB_Utils.GetGameobjectBounds(obj);

            camera.transform.position = bounds.center;
            camera.transform.localRotation = SB_Preferences.instance.PreviewCameraRotation.Value;// Quaternion.Euler(25f, 235f, 0f);
            camera.transform.Translate(Vector3.back * bounds.size.magnitude, Space.Self);

            var originalTexture = CreatePreviewTexture(obj);

            GameObject.DestroyImmediate(obj);

            return originalTexture;
        }

        private static Texture CreatePreviewTexture(GameObject obj)
        {
            previewRenderUtility.BeginPreview(new Rect(0, 0, 256, 256), GUIStyle.none);

            previewRenderUtility.lights[0].transform.localEulerAngles = new Vector3(30, 210, 0);
            previewRenderUtility.lights[0].intensity = SB_Preferences.instance.PreviewLightIntensity.Value;
            previewRenderUtility.AddSingleGO(obj);
            previewRenderUtility.camera.Render();

            var renderPreview = previewRenderUtility.EndPreview();

            Texture2D copyTexture = new Texture2D(renderPreview.width, renderPreview.height, TextureFormat.RGBAHalf, false);
            Graphics.CopyTexture(renderPreview, 0, copyTexture, 0);

            return copyTexture;
        }

        internal static void UpdateCacheSize()
        {
            previewItems.SetCacheSize(SB_Preferences.instance.MaxSimilarItems.Value);
        }
    }
}