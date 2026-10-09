using HeurekaGames.Utils;
using System;
using UnityEditor;
using UnityEngine;

namespace HeurekaGames.SmartBuilder
{
    public class SB_FavoritesWindow : EditorWindow
    {
        private static SB_FavoritesWindow m_window;
        private static SB_Favorites favorites;
        private Vector2 scrollPos;
        private Texture windowIcon;

        public static void Open()
        {
            if (!m_window)
                initializeWindow();
            else
                m_window.Close();
        }

        public static class Icons
        { 
        
        }

        private static void initializeWindow()
        {
            m_window = EditorWindow.GetWindow<SB_FavoritesWindow>(false, SB_Preferences.AssetNameShort + " Favorites");
            SB_Preferences.instance.InitializeIfNeeded();

            favorites = SB_SimilarAssetData.instance.favorites;

            m_window.windowIcon = SB_EditorData.Icons.FavoriteOn;
        }
        private void initIfNeeded()
        {
            if (!m_window)
                initializeWindow();
        }

        void OnInspectorUpdate()
        {
            Repaint();
        }

        void OnGUI()
        {
            initIfNeeded();
            Heureka_WindowStyler.DrawGlobalHeader(Heureka_WindowStyler.clr_middleGreen, SB_Preferences.AssetNameShort + " Favorites");

            if (SB_SimilarAssetData.instance.favorites.Entries.Count == 0)
                Heureka_WindowStyler.DrawCenteredMessage(this, SB_EditorData.Icons.MainIcon, 260, 100, $"No favorites{Environment.NewLine}Add from main window");

            string itemToDelete = string.Empty;
            using (var scrollbar = new EditorGUILayout.ScrollViewScope(scrollPos))
            {
                scrollPos = scrollbar.scrollPosition;
                foreach (var item in favorites.Entries)
                {
                    //Load the previews
                    var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(item));
                    using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(false)))
                    {
                        if (GUILayout.Button(SB_EditorData.Content.LoadIntoMainWindow, SB_Styles.LabelBtnStyle, GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.Width(EditorGUIUtility.singleLineHeight)))
                        {
                            var mainWindow = SB_Window.OpenSmartBuilder();

                            if (asset != null)
                                mainWindow.SelectObject(asset, false);
                            else
                                Debug.LogWarning("SB: Asset not found, did you delete it?");
                        }

                        if (GUILayout.Button(SB_EditorData.Content.RemoveFavorite, SB_Styles.LabelBtnStyle, GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.Width(EditorGUIUtility.singleLineHeight)))
                        {
                            itemToDelete = item;
                        }

                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.ObjectField(asset, typeof(UnityEngine.Object), false);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(itemToDelete))
                    favorites.Entries.Remove(itemToDelete);

                GUILayout.FlexibleSpace();
            }
        }
    }
}