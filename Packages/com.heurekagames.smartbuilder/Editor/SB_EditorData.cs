using HeurekaGames.Utils;
using UnityEditor;
using UnityEngine;

namespace HeurekaGames.SmartBuilder
{
    public static class SB_EditorData
    {
        public static class Content
        {
            public static GUIContent Settings = Heureka_ResourceLoader.GetInternalContentWithTooltip("SettingsIcon", "Settings");
            public static GUIContent RefreshSimilarAssets = Heureka_ResourceLoader.GetInternalContentWithTooltip("Refresh", "Refresh cache");
            public static GUIContent FavoriteOff = Heureka_ResourceLoader.GetInternalContentWithTooltip("Favorite", "Toggle favorite");
            public static GUIContent FavoriteOn = new GUIContent()
            {
                tooltip = "Toggle favorite",
                image = SB_EditorData.Icons.FavoriteOn
            };
            public static GUIContent History = Heureka_ResourceLoader.GetInternalContentWithTooltip("UnityEditor.AnimationWindow", "Selection history");
            public static GUIContent LoadIntoMainWindow = Heureka_ResourceLoader.GetInternalContentWithTooltip("Selectable Icon", "Load into main window");
            public static GUIContent OpenFavoritesWindow = Heureka_ResourceLoader.GetInternalContentWithTooltip("Favorite", "Open Favorites window");
            public static GUIContent RemoveFavorite = new GUIContent()
            {
                tooltip = "Remove from favorites",
                image = Heureka_ResourceLoader.Icons.Clear
            };

            #if UNITY_2023_1_OR_NEWER
                static string closeBtnName = "clear";
            #else
                static string closeBtnName = "winbtn_win_close";
            #endif
            
            public static GUIContent CloseWindow = Heureka_ResourceLoader.GetInternalContentWithTooltip(closeBtnName, "Close infobox");
            public static GUIContent FindSimilar = Heureka_ResourceLoader.GetInternalContentWithTooltip("Selectable Icon", "Find similar");
            public static GUIContent PingAsset = Heureka_ResourceLoader.GetInternalContentWithTooltip("Search Icon", "Ping in project");
            public static GUIContent SelectAssetInProject = Heureka_ResourceLoader.GetInternalContentWithTooltip("Project", "Select in project");
            public static GUIContent ReplaceSelected = Heureka_ResourceLoader.GetInternalContentWithTooltip("Refresh", "Replace selected asset");
            public static GUIContent ReplaceAllOfType = Heureka_ResourceLoader.GetInternalContentWithTooltip("FilterByType", "Replace all of selected type");

        }

        public static class IconNames
        {
            public static readonly string MainIcon = "icon";
            public static readonly string FavoriteOn = "favOn";
        }

        public static class Icons
        {
            public static readonly Texture MainIcon = Heureka_ResourceLoader.GetIcon(Heureka_ResourceLoader.HeurekaPackage.SB, IconNames.MainIcon);
            public static readonly Texture FavoriteOn = Heureka_ResourceLoader.GetIcon(Heureka_ResourceLoader.HeurekaPackage.SB, IconNames.FavoriteOn);
            public static readonly Texture Store = Heureka_ResourceLoader.GetInternalIcon(Heureka_Utils.IsUnityVersionGreaterThan(2019)? "Asset Store@2x" : "AssetStore Icon");
        }
    }
}
