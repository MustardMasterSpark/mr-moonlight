using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using HeurekaGames.SmartReplacer;
using HeurekaGames.Utils;

namespace HeurekaGames.SmartBuilder
{
    public class SB_Window : EditorWindow
    {
        private static string VERSIONNUM = "1.1.1";
        private const int WINDOWMENUITEMPRIO = 11;
        internal static readonly Heureka_ResourceLoader.HeurekaPackage myPackage = Heureka_ResourceLoader.HeurekaPackage.SB;
        private static SB_Window m_window;

        private SB_SimilarAssetData similarAssetData;
        private SB_Preferences preferences;

        private SB_History history;

        private int selectionHistoryIndex = 0;
        [System.NonSerialized] private int softSelectionIndex = -1; //Which asset is selected in grid
        [System.NonSerialized] private int infoSelectionIndex = -1; //Which asset is selected in info box

        private Vector2 scrollPos;
        private int firstScrollIndex = 0;
        private int lastScrollIndex = 0;
        private int columns = 3;

        private Rect lastRect = new Rect(0, 0, 64, 64); //Initial value
        private Rect scrollArea = new Rect(0, 0, 64 * 3, 64 * 5); //Initial value
        private Rect assetStorePopupButtonRect;

        private bool selectionLocked;
        private bool requestingRepaint;

        private List<List<string>> tagRows = new List<List<string>>();
        private Editor gameObjectEditor;
        private Rect cachedPreviewRect;
        private SB_AssetInfo mouseClick;
        private Rect infoAreaPopupButtonRect;
        private Rect rightClickPopupButtonRect;

        #region menuitems
        [UnityEditor.MenuItem("Tools/" + SB_Preferences.AssetName + "/" + SB_Preferences.AssetName + " _%#&b", priority = WINDOWMENUITEMPRIO)]
        [UnityEditor.MenuItem("Window/Heureka/" + SB_Preferences.AssetName + "/" + SB_Preferences.AssetName, priority = WINDOWMENUITEMPRIO)]
        public static SB_Window OpenSmartBuilder()
        {
            initializeWindow();
            return m_window;
        }

        [UnityEditor.MenuItem("Tools/" + SB_Preferences.AssetName + "/Force Select _%#&s", false)]
        [UnityEditor.MenuItem("Window/Heureka/" + SB_Preferences.AssetName + "/Force Select", false)]
        public static void SelectCurrent()
        {
            initializeWindow();
            m_window.SelectObject(Selection.activeObject, false);
        }

        [UnityEditor.MenuItem("Tools/" + SB_Preferences.AssetName + "/Force Select _%#&s", true)]
        [UnityEditor.MenuItem("Window/Heureka/" + SB_Preferences.AssetName + "/Force Select", true)]
        public static bool ValidateSelectCurrent()
        {
            return (Selection.activeObject != null);
        }

        [UnityEditor.MenuItem("Tools/" + SB_Preferences.AssetName + "/Toggle Lock _#l", false)]//_%#&l
        [UnityEditor.MenuItem("Window/Heureka/" + SB_Preferences.AssetName + "/Toggle Lock", false)]
        public static void ToggleLock()
        {
            m_window.SelectObject(Selection.activeObject, true);
        }

        [UnityEditor.MenuItem("Tools/" + SB_Preferences.AssetName + "/Toggle Lock _#l", true)]
        [UnityEditor.MenuItem("Window/Heureka/" + SB_Preferences.AssetName + "/Toggle Lock", true)]
        public static bool ValidateToggleLock()
        {
            return (m_window != null) && Selection.activeObject != null;
        }
        #endregion

        private static void initializeWindow()
        {
            m_window = EditorWindow.GetWindow<SB_Window>(false, SB_Preferences.AssetName);
            m_window.titleContent.image = SB_EditorData.Icons.MainIcon;
            m_window.similarAssetData = SB_SimilarAssetData.instance;
            m_window.similarAssetData.InitializeIfNeeded();

#if UNITY_2021_1_OR_NEWER
            SB_SceneViewDrawer.AllowDraw(true);
#endif
        }

        void OnProjectChange() //Todo use asset postprocessor instead?
        {
            similarAssetData.ValidateCache();
            similarAssetData.RemoveObsoleteFavoritesEntries();

            if (history.TryRemoveObsoleteGUIDs())
                selectionHistoryIndex = Mathf.Max(0, history.Entries.Count() - 1);
        }

        private void OnEnable()
        {
            VERSIONNUM = Heureka_Utils.GetVersionNumber<SB_Window>();

            history = new SB_History();
            selectionHistoryIndex = Mathf.Max(0, history.Entries.Count() - 1);

            similarAssetData = SB_SimilarAssetData.instance;
            similarAssetData.InitializeIfNeeded();

            preferences = SB_Preferences.instance;
            preferences.InitializeIfNeeded();

            ForceSelectionChange(false);
        }

        void OnInspectorUpdate()
        {
            if (!m_window)
                initializeWindow();

            //Has been set in async timer that force updates after selection ahs changed (Its because assetpreviews are loaded asyncronously)
            if (requestingRepaint)
            {
                this.Repaint();
                requestingRepaint = false;
            }

            if (similarAssetData.SimilarAssets != null)
            {
                similarAssetData.ClearObsolete();
                var oldColumns = columns;
                columns = Mathf.FloorToInt((scrollArea.width - 10) / (lastRect.width + 2)); //Cant figure out how to get the width of the handle of the scrollbar, or the spacing between elements :( So using arbitrary fixed numbers :(
                columns = Mathf.Clamp(columns, 1, columns);

                var oldfirstScrollIndex = firstScrollIndex;
                firstScrollIndex = ((int)(scrollPos.y / lastRect.height) * columns);
                firstScrollIndex = Mathf.Clamp(firstScrollIndex, 0, Mathf.Max(0, similarAssetData.SimilarAssets.Count));

                var oldlastScrollIndex = lastScrollIndex;
                float scrollMaxY = scrollPos.y + scrollArea.height;
                lastScrollIndex = ((int)System.Math.Ceiling((scrollMaxY / lastRect.height)) * columns);
                lastScrollIndex = Mathf.Clamp(lastScrollIndex, 0, similarAssetData.SimilarAssets.Count - 1);

                if (oldColumns != columns || oldfirstScrollIndex != firstScrollIndex || oldlastScrollIndex != lastScrollIndex)
                    Repaint();
            }
        }

        void OnSelectionChange()
        {
            ForceSelectionChange(false);
        }

        void OnGUI()
        {
            Rect headerRect = drawHeader();
            GUILayout.Space(headerRect.height + 4);
            if (this.position.width < this.position.height)
            {
                drawBody();
                GUILayout.FlexibleSpace();
                drawInfoArea();
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        //GUILayout.FlexibleSpace();
                        drawInfoArea();
                        GUILayout.FlexibleSpace();
                    }
                    using (new EditorGUILayout.VerticalScope())
                    {
                        drawBody();
                    }
                }
            }
        }

        //Since the texture preview loading is async, we forceupdate a little later to make sure we are getting the previews shown
        private void waitToRepaint()
        {
            var startTime = Time.realtimeSinceStartup;
            var repaintWorker = new System.ComponentModel.BackgroundWorker();
            repaintWorker.DoWork += (sender, args) =>
            {
                for (int i = 0; i < 20; i++) //Repaint for 2 seconds
                {
                    Thread.Sleep(100);
                    requestingRepaint = true;
                }
            };

            repaintWorker.RunWorkerCompleted += (sender, args) =>
            {
                requestingRepaint = true;
            };

            repaintWorker.RunWorkerAsync();
        }
        /// <summary>
        /// Triggers selection change
        /// </summary>
        /// <param name="forceReload">If we need to force reload (i.e. if we changed settings)</param>
        public void ForceSelectionChange(bool forceReload)
        {
            //TODO If we have multiple selected, we should only be dealing with the first item in list
            if (similarAssetData.SimilarAssets == null)
                selectionLocked = false; //Unlock so we can select new object

            if (selectionLocked)
                return;

            if (Selection.objects?.Length > 0)
                updateSelection(Selection.objects[0], forceReload);
        }

        public void SelectObject(UnityEngine.Object target, bool shouldToggleLock)
        {
            UnityEngine.Object actualTarget = (Selection.activeTransform != null) ? PrefabUtility.GetCorrespondingObjectFromOriginalSource(target) : target;

            if (shouldToggleLock)
                selectionLocked = !selectionLocked;

            this.Repaint();

            if (actualTarget == similarAssetData.Selected?.prefab)
                return;

            updateSelection(actualTarget, false);
        }

        private void updateSelection(UnityEngine.Object selectedObject, bool forceReload)
        {
            if (similarAssetData.UpdateSelection(onSelectionUpdated, selectedObject, forceReload))
            {
                addToHistory(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(similarAssetData.Selected.prefab)));

                this.Repaint();
                waitToRepaint();
            }
        }

        //Callback when we have new simmilar items
        private void onSelectionUpdated()
        {
            scrollPos = new Vector2(0, 0);
            //If we already had a selection, reset to 0 (Which should be new main selection) If we didn't have selection (-1) keep it like that
            softSelect(softSelectionIndex != -1 ? 0 : -1, false);
            this.Repaint();
            //Do some trickery to force unity to repaint for a little while to ensure we get the asset previews loaded async
            waitToRepaint();
        }

        private void drawAdditionalHeaderIcons()
        {
            GUILayout.FlexibleSpace();

            if (similarAssetData.CacheOutOfDate)
            {
                if (GUILayout.Button(SB_EditorData.Content.RefreshSimilarAssets, SB_Styles.ImageBtnStyle, GUILayout.Height(Heureka_WindowStyler.HeaderHeight - 4), GUILayout.Width(Heureka_WindowStyler.HeaderHeight - 4)))
                {
                    similarAssetData.RefreshCache();
                    updateSelection(similarAssetData.Selected.prefab, true);
                }
            }

            if (GUILayout.Button(SB_EditorData.Content.OpenFavoritesWindow, SB_Styles.ImageBtnStyle, GUILayout.Height(Heureka_WindowStyler.HeaderHeight - 4), GUILayout.Width(Heureka_WindowStyler.HeaderHeight - 4)))
            {
                SB_FavoritesWindow.Open();
            }

            if (GUILayout.Button(SB_EditorData.Content.Settings, SB_Styles.ImageBtnStyle, GUILayout.Height(Heureka_WindowStyler.HeaderHeight - 4), GUILayout.Width(Heureka_WindowStyler.HeaderHeight - 4)))
            {
                SB_SettingsWindow.Open();
            }
        }

        private Rect drawHeader()
        {
            Heureka_WindowStyler.DrawGlobalHeader(Heureka_WindowStyler.clr_middleGreen, SB_Preferences.AssetName, VERSIONNUM, drawAdditionalHeaderIcons);

            Rect tagsRect;
            using (var outerScope = new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(false)))
            {
                drawAssetStoreButton();
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (var hScope = new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(false)))
                    {
                        using (var btnScope = new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(false), GUILayout.ExpandWidth(false)))
                        {
                            drawHistoryNavigationButton(-1);
                            drawHistoryButton();
                            drawHistoryNavigationButton(1);
                            drawFavoritesButton();
                            float buttonScopeWidth = btnScope.rect.width;

                            drawLockButton();
                            float maxWidth = (EditorGUIUtility.currentViewWidth - buttonScopeWidth) - 182f;
                            if (!selectionLocked)
                            {
                                using (var check = new EditorGUI.ChangeCheckScope())
                                {
                                    var tmp = EditorGUILayout.ObjectField(similarAssetData.Selected?.prefab, typeof(GameObject), false, GUILayout.ExpandWidth(true)/* GUILayout.Width(maxWidth)*/);
                                    if (check.changed)
                                        updateSelection(tmp, false);
                                }
                            }
                            else if (similarAssetData?.Selected?.prefab != null)
                            {
                                string label = similarAssetData.Selected.prefab.name + ((selectionLocked) ? " (Locked)" : "");
                                GUIContent content = new GUIContent(label);
                                var labelWidth = GUI.skin.label.CalcSize(content).x;

                                labelWidth = Mathf.Min(labelWidth, maxWidth);
                                EditorGUILayout.LabelField(content, GUILayout.Width(labelWidth));

                            }
                            GUILayout.FlexibleSpace();
                        }
                    }
                    drawTags();
                }
                tagsRect = outerScope.rect;
            }
            return tagsRect;
        }

        private void drawTags()
        {
            //No need to draw rest, if selection is locked
            if (selectionLocked)
                return;

            GUIStyle toolbarStyle = new GUIStyle(EditorStyles.miniButton);

            using (var headerScope = new EditorGUILayout.VerticalScope())
            {
                //Dynamic valuable to hold the tag we want to toggle
                (string a, bool b) tagToModify = (string.Empty, false);

                //Set temporary tagrow holder to current value (And then potentially change it in repaint)
                List<List<string>> tmpTagRowHolder = tagRows;

                if (similarAssetData?.SelectedPrefabTags?.Count() >= 1)
                {
                    if (Event.current.type == EventType.Repaint)
                    {
                        tmpTagRowHolder = new List<List<string>>() { new List<string>() };
                        var allowedWidth = EditorGUIUtility.currentViewWidth - assetStorePopupButtonRect.width;
                        float currentX = 0;
                        int curRow = 0;

                        //Make sure the tags fit window width. If not, make new row
                        foreach (var item in similarAssetData.SelectedPrefabTags)
                        {
                            var itemWidth = toolbarStyle.CalcSize(new GUIContent(item)).x;
                            currentX += itemWidth;

                            if (currentX <= allowedWidth)
                                tmpTagRowHolder[curRow].Add(item);
                            else
                            {
                                curRow++;
                                tmpTagRowHolder.Add(new List<string>() { item });
                                currentX = itemWidth; //Reset row position X
                            }
                        }
                    }

                    foreach (var row in tagRows)
                    {
                        using (new EditorGUILayout.HorizontalScope(/*toolbarStyle*/))
                        {
                            foreach (var tag in row)
                            {
                                var isIgnored = (preferences.IgnoredTags?.Entries.Contains(tag) ?? false);

                                Color orig = GUI.backgroundColor;
                                GUI.backgroundColor = (isIgnored) ? Color.grey : orig;

                                Color contentOrig = GUI.contentColor;
                                GUI.contentColor = (isIgnored) ? Color.grey : contentOrig;

                                if (GUILayout.Button(tag))
                                {
                                    tagToModify.a = tag;
                                    tagToModify.b = !isIgnored; //Toggle
                                }
                                GUI.backgroundColor = orig;
                                GUI.contentColor = contentOrig;
                            }
                            GUILayout.FlexibleSpace();
                        }
                    }

                    //Set the tagrows now we are done with repaint
                    tagRows = tmpTagRowHolder;
                    if (tagToModify.a != string.Empty)
                    {
                        similarAssetData.IgnoreTag(tagToModify.a, tagToModify.b);
                        updateSelection(similarAssetData.Selected.prefab, true);
                    }
                }
            }
        }

        private void drawBody()
        {
            if (similarAssetData.SimilarAssets != null)
            {
                if (Event.current.type == EventType.ScrollWheel)
                {
                    if (Event.current.modifiers.HasFlag(EventModifiers.Control) && scrollArea.Contains(Event.current.mousePosition))
                    {
                        var delta = Mathf.Sign(Event.current.delta.y) * 2 * -1; //Inverting
                        preferences.PreviewImageSize.IncreaseBy((int)(delta));
                        Event.current.Use();
                    }
                }

                bool reqRepaint = false;
                using (var scrollbar = new EditorGUILayout.ScrollViewScope(scrollPos, GUILayout.ExpandHeight(true)))
                {
                    reqRepaint = (scrollPos != scrollbar.scrollPosition); //Force repaint as we do the layout logic in 'OnInspectorUpdate'
                    scrollPos = scrollbar.scrollPosition;

                    GUILayout.Space(((float)firstScrollIndex / columns) * lastRect.height); //Make empty space at top of scrollbar (We dont have to draw stuff not within view of scrollbar)

                    for (int i = firstScrollIndex; i <= lastScrollIndex; i += columns)
                    {
                        using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                        {
                            for (int j = i; j < i + columns; j++)
                            {
                                if ((similarAssetData.SimilarAssets?.Count - 1) < j) //If the index on current row doesn't exist, draw a spacer instead
                                {
                                    GUILayout.FlexibleSpace();
                                    GUILayout.Space(preferences.PreviewImageSize.Value + 3); //Again some of those weird pixels i have to add since cellsize is not 100% exact (Added 3 pixels) :(
                                    continue;
                                }

                                var isSoftSelection = softSelectionIndex == j;
                                Color orig = GUI.color;
                                if (isSoftSelection)
                                    GUI.color = preferences.SelectionColor.Value;

                                bool hasPreview;
                                var content = SB_PreviewCacheBuilder.GetAssetPreviewContent(similarAssetData.SimilarAssets[j], out hasPreview);

                                //Load the previews
                                var asset = AssetDatabase.LoadMainAssetAtPath(similarAssetData.SimilarAssets[j].path); //TODO Must be possible to cache the info we need

                                if (i != j)//Not first column
                                    GUILayout.FlexibleSpace();

                                EditorGUILayout.LabelField(content, GUILayout.Width(preferences.PreviewImageSize.Value), GUILayout.Height(preferences.PreviewImageSize.Value)); //Label to allow for drag

                                GUI.color = orig;

                                drawSuperimposedLayer(asset, isSoftSelection, !hasPreview);

                                //Get the size of the label above so we know how many columns we can draw in current scrollrect
                                if (Event.current.type == EventType.Repaint)
                                    lastRect = GUILayoutUtility.GetLastRect();

                                processMouseEvents(j);
                            }
                        }
                    }

                    if (!similarAssetData.IsDirty)
                    {
                        var spacer = Mathf.CeilToInt((((float)similarAssetData.SimilarAssets.Count() - 1) - (float)lastScrollIndex) / (float)columns) * lastRect.height;
                        GUILayout.Space(spacer);
                    }

                    //If we did something that requires us to repaint
                    if (reqRepaint)
                        Repaint();
                }
                if (Event.current.type == EventType.Repaint)
                    scrollArea = GUILayoutUtility.GetLastRect();

                if (similarAssetData.IsDirty)
                {
                    var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    EditorGUI.ProgressBar(rect, similarAssetData.WorkerCompletion, similarAssetData.WorkerStatus);
                }
            }
            else
            {
                Heureka_WindowStyler.DrawCenteredMessage(this, SB_EditorData.Icons.MainIcon, 250, 100, "Select prefab" + System.Environment.NewLine + "In scene or project");
            }
        }

        private void drawInfoArea()
        {
            if (similarAssetData?.Selected != null)
            {
                if (infoSelectionIndex != -1 && similarAssetData?.SimilarAssets?.Count() >= infoSelectionIndex)
                {
                    using (var scope = new EditorGUILayout.HorizontalScope())
                    {
                        //If vertical style
                        bool isVertical = this.position.width < this.position.height;
                        if (isVertical)
                        {
                            using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(false)))
                            {
                                drawInfoAreaButtons();
                            }
                        }

                        using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(true)))
                        {
                            if (infoSelectionIndex != -1 && similarAssetData.SimilarAssets[infoSelectionIndex].prefab != null)
                            {
                                GUIStyle bgColor = new GUIStyle();
                                bgColor.normal.background = EditorGUIUtility.whiteTexture;

                                if (gameObjectEditor == null)
                                    gameObjectEditor = Editor.CreateEditor(similarAssetData.SimilarAssets[infoSelectionIndex].prefab);

                                //Choose a preview size based on view being horizontal/vertical
                                Vector2 previewSize = (isVertical) ? new Vector2(256, 128) : new Vector2(128, (this.position.height * (2f / 3f) - 100));

                                gameObjectEditor.OnInteractivePreviewGUI(GUILayoutUtility.GetRect(previewSize.x, previewSize.y), bgColor);

                                if (!isVertical)
                                {
                                    using (new EditorGUILayout.HorizontalScope())
                                    {
                                        drawInfoAreaButtons();

                                        if (infoSelectionIndex == -1) return; //If we closed infobox
                                    }
                                }

                                using (new EditorGUI.DisabledScope(true))
                                {
                                    EditorGUILayout.ObjectField(similarAssetData?.SimilarAssets[infoSelectionIndex]?.prefab, typeof(GameObject), false);
                                }

                                GameObject go = (GameObject)similarAssetData.SimilarAssets[infoSelectionIndex].prefab;
                                Bounds bounds = SB_Utils.GetGameobjectBounds(go);

                                if (bounds != null)
                                {
                                    using (new EditorGUILayout.HorizontalScope())
                                    {
                                        using (new EditorGUI.DisabledScope(true))
                                        {
                                            float rounder = 100f; //Value used to round the floats (100 means 2 decimals, 1000 means 3 decimals etc)
                                            float size = Mathf.Round((bounds.extents.x * bounds.extents.y * bounds.extents.z) * rounder) / rounder;
                                            EditorGUILayout.LabelField($"Size: {size} m3 ", GUILayout.ExpandHeight(false));

                                        }
                                        GUILayout.FlexibleSpace();
                                    }
                                }

                            }
                        }
                        if (Event.current.type == EventType.Repaint)
                        {
                            cachedPreviewRect = scope.rect;
                            cachedPreviewRect.x += this.position.x;
                            cachedPreviewRect.y += this.position.y;
                        }
                    }
                }
            }
        }

        private void drawInfoAreaButtons()
        {
            using (new EditorGUIUtility.IconSizeScope(new Vector2(16, 16)))
            {
                int selectionCount = Selection.transforms.Count();
                if (GUILayout.Button(SB_EditorData.Content.CloseWindow, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                {
                    infoSelectionIndex = -1;
                }

                if (GUILayout.Button(SB_EditorData.Content.FindSimilar, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                {
                    btnActionSelectObject(softSelectionIndex);
                }

                if (GUILayout.Button(SB_EditorData.Content.PingAsset, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                {
                    btnActionPingObject(softSelectionIndex);
                }

                if (GUILayout.Button(SB_EditorData.Content.SelectAssetInProject, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                {
                    btnActionSelectInProject(softSelectionIndex);
                }

                using (new EditorGUI.DisabledScope(selectionCount == 0))
                {
                    if (GUILayout.Button(SB_EditorData.Content.ReplaceSelected, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                    {
                        btnActionReplaceAsset(softSelectionIndex);
                    }

                    if (GUILayout.Button(SB_EditorData.Content.ReplaceAllOfType, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                    {
                        btnActionReplaceAssetsOfType(softSelectionIndex);
                    }
                }

                var isFavorite = similarAssetData.IsFavorite(softSelectionIndex);
                if (GUILayout.Button(isFavorite ? SB_EditorData.Content.FavoriteOn : SB_EditorData.Content.FavoriteOff, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                {
                    similarAssetData.MakeSelectionFavorite(softSelectionIndex, !isFavorite);
                }

                SB_AssetInfo aInfo = similarAssetData.SimilarAssets[softSelectionIndex];
                var relevantTags = aInfo.TagsExcludedIgnored;

                if (relevantTags.Count() == 0)
                    return;

                GUIContent content = new GUIContent(SB_EditorData.Icons.Store, GetToolTipForSearchingAssetStore(relevantTags));
                if (GUILayout.Button(content, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                {
                    PopupWindow.Show(infoAreaPopupButtonRect, new SB_StoreSearchPopup(relevantTags.ToList()));
                }
                if (Event.current.type == EventType.Repaint) infoAreaPopupButtonRect = GUILayoutUtility.GetLastRect();
            }
        }
        
        private bool drawToolbarButton(GUIContent content)
        {
            return GUILayout.Button(content, EditorStyles.toolbarButton);
        }

        private void drawFavoritesButton()
        {
            var isFavorite = similarAssetData.HasFavoriteSelected();
            if (drawToolbarButton(isFavorite ? SB_EditorData.Content.FavoriteOn : SB_EditorData.Content.FavoriteOff))
            {
                similarAssetData.MakeSelectionFavorite(!isFavorite);
            }
        }

        private void drawLockButton()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(false)))
            {
                GUILayout.Space(2);
                GUIStyle toggleStyle = new GUIStyle("IN LockButton");
                toggleStyle.margin = new RectOffset(6, 0, 0, 0);

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    selectionLocked = GUILayout.Toggle(selectionLocked, GUIContent.none, toggleStyle);
                    //If we unlocked but the selection doesnt match the old "locked" selection
                    if (check.changed && !selectionLocked)
                    {
                        ForceSelectionChange(false);
                    }
                }
            }
        }

        private void drawHistoryButton()
        {
            using (new EditorGUI.DisabledGroupScope(history?.Entries.Count() < 2))
            {
                if (drawToolbarButton(SB_EditorData.Content.History))
                {
                    // create the menu and add items to it
                    GenericMenu menu = new GenericMenu();

                    for (int i = history.Entries.Count() - 1; i >= 0; i--)
                    {
                        var tmpIndex = i;
                        var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(history.Entries[i]));
                        if (asset != null)
                            menu.AddItem(new GUIContent(asset.name), i == selectionHistoryIndex, () => selectFromHistory(tmpIndex));
                    }

                    menu.ShowAsContext();
                }
            }
        }

        private void drawHistoryNavigationButton(int direction)
        {
            bool validDirection = hasHistory(direction);
            EditorGUI.BeginDisabledGroup(!validDirection);

            if (drawToolbarButton(direction == -1 ? Heureka_ResourceLoader.Content.Previous : Heureka_ResourceLoader.Content.Next))
            {
                if (direction == -1)
                    SelectPreviousFromHistory();
                else if (direction == 1)
                    SelectNextFromHistory();
                else
                    Debug.LogWarning("Wrong integer. You must select -1 or 1");
            }
            EditorGUI.EndDisabledGroup();
        }

        private void drawAssetStoreButton()
        {
            var relevantTags = similarAssetData.SelectedPrefabTags.Where(x => !preferences.IgnoredTags.Entries.Contains(x));
            if (relevantTags.Count() == 0)
                return;

            var origIconSize = EditorGUIUtility.GetIconSize();
#if UNITY_2019
            EditorGUIUtility.SetIconSize(new Vector2(46, 46));
#endif
            GUIContent content = new GUIContent(SB_EditorData.Icons.Store, GetToolTipForSearchingAssetStore(relevantTags));
            using (new EditorGUILayout.VerticalScope())
            {
                if (GUILayout.Button(content, SB_Styles.ImageBtnStyle, GUILayout.ExpandHeight(true)))
                {
                    PopupWindow.Show(assetStorePopupButtonRect, new SB_StoreSearchPopup(relevantTags.ToList()));
                }
                if (Event.current.type == EventType.Repaint) assetStorePopupButtonRect = GUILayoutUtility.GetLastRect();
            }
            EditorGUIUtility.SetIconSize(origIconSize);
        }

        private string GetToolTipForSearchingAssetStore(IEnumerable<string> tags)
        {
            return "Search assetstore for " + string.Join(", ", tags);
        }

        private void drawSuperimposedLayer(UnityEngine.Object asset, bool selected, bool noPreview)
        {
            if (asset == null)
                return;

            int btnSize = 20;
            var previewRect = GUILayoutUtility.GetLastRect();

            //Draw helper buttons
            if (selected)
            {
                string iconID = (noPreview)?"Selectable Icon":"d_Selectable Icon";
#if UNITY_2019
                iconID = "Selectable Icon";
#endif
                GUIContent selectionContent = Heureka_ResourceLoader.GetInternalContentWithTooltip(iconID, "Find similar");

                iconID = (noPreview)?"_Help@2x":"d__Help@2x";
#if UNITY_2019
                iconID = "d_UnityEditor.InspectorWindow";
#endif
                GUIContent infoContent = Heureka_ResourceLoader.GetInternalContentWithTooltip(iconID, "Inspect");

                float margin = 2f;

                var topLeftRect = new Rect(previewRect);
                topLeftRect.height = btnSize;
                topLeftRect.width = btnSize;
                topLeftRect.y += margin;

                var topRightRect = new Rect(topLeftRect);
                topRightRect.x += (((previewRect.width - btnSize) - margin));

                topLeftRect.x += margin;

                if (GUI.Button(topLeftRect, selectionContent, SB_Styles.LabelBtnStyle))
                    SelectObject(asset, false);

                if (GUI.Button(topRightRect, infoContent, SB_Styles.LabelBtnStyle))
                    infoSelectionIndex = (infoSelectionIndex == -1) ? softSelectionIndex : -1;
            }

            //Draw asset name
            if (noPreview)
            {
                //Draw dummy preview
                if (noPreview)
                {
                    var centerRect = new Rect(previewRect);
                    centerRect.y = previewRect.y + previewRect.height * .3f;
                    centerRect.x = previewRect.x + previewRect.width * .3f;
                    centerRect.height = previewRect.height * .3f;

                    var thumb = AssetPreview.GetMiniThumbnail(asset);
                    GUIContent selectionContent = new GUIContent(thumb);
                    GUI.Label(centerRect, selectionContent, SB_Styles.LabelBtnStyle);
                }

            }//Draw asset name
            if(selected||preferences.AlwaysShowFileName.Value)
            {
                GUIContent nameContent = new GUIContent(asset.name);
                var textHeight = SB_Styles.OverlayNameStyleWhite.CalcHeight(nameContent, previewRect.width);
                var numLines = textHeight / SB_Styles.OverlayNameStyleWhite.lineHeight;
                numLines = Mathf.Clamp(numLines, 1, 3);

                var bottomRect = new Rect(previewRect);
                bottomRect.height = numLines * SB_Styles.OverlayNameStyleWhite.lineHeight;
                bottomRect.y = previewRect.y + previewRect.height - bottomRect.height;

                if(selected || preferences.AlwaysShowFileName.Value)
                {
                    if(!noPreview)
                    {
                        GUI.Label(bottomRect, asset.name, SB_Styles.OverlayNameStyle);
                    }
                    else
                    {
                        GUI.Label(bottomRect, asset.name, SB_Styles.OverlayNameStyleWhite);
                    }
                }

            }
        }

        private void processMouseEvents(int currentIndex)
        {
            Event e = Event.current;
            Vector2 invertedMousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);

            //If we are inside previewrect, we dont want to drag, since that mouseevent overrides the preview window functionality
            if (infoSelectionIndex != -1 && cachedPreviewRect.Contains(invertedMousePos))
            {
                return;
            }

            bool hasMouseFocus = GUILayoutUtility.GetLastRect().Contains(e.mousePosition);

            if (hasMouseFocus && e.type == EventType.MouseDrag) //Start dragging the asset (Picked up up by DragHandler.cs)
            {
                //TODO Investigate DragAndDrop.SceneDropHandler callback
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(SB_Preferences.AssetNameShort, true);
                DragAndDrop.objectReferences = new UnityEngine.Object[] { AssetDatabase.LoadMainAssetAtPath(mouseClick.path) };
                DragAndDrop.StartDrag("Similar object drag");
                e.Use();
            }
            else if (e.type == EventType.MouseDown)
            {
                if (hasMouseFocus && Event.current.button == 1) //Right mouse
                {
                    rightClickPopupButtonRect = new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0);// GUILayoutUtility.GetLastRect();
                    drawRightClickMenu(currentIndex);
                }
                else if (hasMouseFocus && Event.current.button == 0)//Left mouse
                {
                    //Have to do this to fix issue when DragAndDrop would pick up wrong item, because of mouse movement
                    mouseClick = similarAssetData.SimilarAssets[currentIndex];
                    softSelect(currentIndex, true);
                    this.Repaint();
                }
            }
        }

        private void drawRightClickMenu(int currentIndex)
        {
            var menu = new GenericMenu();
            SB_AssetInfo aInfo = similarAssetData.SimilarAssets[currentIndex];
            string filename = aInfo.fileName;
            string path = aInfo.path;
            
            // Add a single menu item
            menu.AddItem(new GUIContent($"Select in SmartBuilder"), false,
                () =>
                {
                    btnActionSelectObject(currentIndex);
                });
            menu.AddItem(new GUIContent($"Select in project"), false,
                () =>
                {
                    btnActionSelectInProject(currentIndex);
                });
            menu.AddItem(new GUIContent($"Ping in project"), false,
                () =>
                {
                    btnActionPingObject(currentIndex);
                });
            menu.AddItem(new GUIContent(GetToolTipForSearchingAssetStore(aInfo.TagsExcludedIgnored)), false,
                () =>
                {
                    PopupWindow.Show(rightClickPopupButtonRect, new SB_StoreSearchPopup(aInfo.TagsExcludedIgnored.ToList()));
                });

            menu.AddSeparator("");

            bool isFavorite = similarAssetData.IsFavorite(currentIndex);
            menu.AddItem(new GUIContent($"{(isFavorite ? "Remove Favorite" : "Make Favorite")}"), false,
                () =>
                {
                    similarAssetData.MakeSelectionFavorite(currentIndex, !isFavorite);
                });

            menu.AddSeparator("");

            string subfolderName = "Replace Prefab/";
            int selected = Selection.transforms.Count();
            string label = $"{subfolderName}Replace scene selection {(selected == 0 ? "" : $"({selected})")}";

            if ((selected == 0))
                menu.AddDisabledItem(new GUIContent($"{subfolderName}Replace scene selection"));
            else
                menu.AddItem(new GUIContent(label), false,
                    value => replaceAsset(Selection.transforms, value as SB_AssetInfo, false, false),
                    aInfo);

            if ((selected == 0))
                menu.AddDisabledItem(new GUIContent($"{subfolderName}Replace all of selected types in scene"));
            else
            {
               var selectionPrefabs = getPrefabSelection();

                var replaceAllLabel = $"{subfolderName}Replace all of selected types in scene ({selectionPrefabs.Count})";
                if (selectionPrefabs.Count > 0)
                {
                    menu.AddItem(new GUIContent(replaceAllLabel), false,
                            value => replaceAllOfType(selectionPrefabs, value as SB_AssetInfo, false),
                            aInfo);
                }
                else
                    menu.AddDisabledItem(new GUIContent(replaceAllLabel));
            }

            menu.AddItem(new GUIContent($"{subfolderName}Advanced Replacer"), false,
                value => replaceAsset(Selection.transforms, value as SB_AssetInfo, true, false),
                aInfo);

            menu.ShowAsContext();
        }

        private List<GameObject> getPrefabSelection()
        {
            var selectionPrefabs = Selection.transforms.Select(o => PrefabUtility.GetCorrespondingObjectFromSource(o)?.gameObject).Distinct().ToList();      
            selectionPrefabs.RemoveAll(x => x == null);

            return selectionPrefabs;
        }

        private void btnActionSelectObject(int index)
        {
            SB_AssetInfo aInfo = similarAssetData.SimilarAssets[index];
            SelectObject(aInfo.prefab, false);
        }

        private void btnActionPingObject(int index)
        {
            SB_AssetInfo aInfo = similarAssetData.SimilarAssets[index];
            string path = aInfo.path;
            EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path));
        }

        private void btnActionSelectInProject(int index)
        {
            SB_AssetInfo aInfo = similarAssetData.SimilarAssets[index];
            string path = aInfo.path;
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
        }

        private void btnActionReplaceAsset(int index)
        {
            SB_AssetInfo aInfo = similarAssetData.SimilarAssets[index];
            replaceAsset(Selection.transforms, aInfo, false, false);
        }

        private void btnActionReplaceAssetsOfType(int index)
        {
            SB_AssetInfo aInfo = similarAssetData.SimilarAssets[index];
            replaceAllOfType(getPrefabSelection(), aInfo, false);
        }

        /// <summary>
        /// Set the soft selection to a given index of the similar assets
        /// </summary>
        /// <param name="newIndex">The index we want to see (-1 is none)</param>
        /// <param name="allowToggle">If true, we will set index to -1 if the new selectionindex equals old selectionindex</param>
        private void softSelect(int newIndex, bool allowToggle)
        {
            softSelectionIndex = (allowToggle && softSelectionIndex == newIndex) ? -1 : newIndex; //Toggle selected
            infoSelectionIndex = (infoSelectionIndex != -1) ? softSelectionIndex : -1;

            //if (softSelectionIndex != -1 && similarAssetData.SimilarAssets?.Count >= softSelectionIndex)
            if (similarAssetData.SimilarAssets?.ElementAtOrDefault(softSelectionIndex) != null)
            {
                //Destroy the preview editor so we have no memoryleak when previewing multiple
                DestroyImmediate(gameObjectEditor);

                SB_AssetInfo aInfo = similarAssetData.SimilarAssets[softSelectionIndex];
                string filename = aInfo.fileName;
                string path = aInfo.path;

                if (preferences.AutoPingSelection.Value)
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path));
            }
            //Else we did not have any selection in grid
        }

        private void SelectPreviousFromHistory()
        {
            selectionHistoryIndex--;
            selectFromHistory(selectionHistoryIndex);
        }

        private void SelectNextFromHistory()
        {
            selectionHistoryIndex++;
            selectFromHistory(selectionHistoryIndex);
        }

        private void selectFromHistory(int newIndex)
        {
            selectionHistoryIndex = newIndex;
            SelectObject(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(history.Entries[selectionHistoryIndex])), false);
        }

        private void addToHistory(string guid)
        {
            //The item already in history matches the item we are trying to insert
            if ((history?.Entries.Count > selectionHistoryIndex) && history?.Entries[selectionHistoryIndex] == guid)
                return;

            //Remove the part of the history branch that are no longer needed
            if (history?.Entries.Count - 1 > selectionHistoryIndex)
            {
                history.Entries.RemoveRange(selectionHistoryIndex, history.Entries.Count - selectionHistoryIndex);
            }

            if (history != null && (history?.Entries?.Count == 0 || guid != history?.Entries?.Last()))
            {
                history.Entries.Add(guid);

                var maxHistoryCount = 25;
                if (history.Entries.Count > maxHistoryCount)
                    history.Entries.RemoveRange(0, history.Entries.Count - maxHistoryCount);

                selectionHistoryIndex = history.Entries.Count - 1;
            }
        }

        private bool hasHistory(int direction)
        {
            int testIndex = selectionHistoryIndex + direction;
            bool validIndex = (testIndex >= 0 && testIndex < history?.Entries?.Count);
            //Validate that history contains that index
            return (testIndex >= 0 && testIndex < history.Entries.Count);
        }

        private void replaceAllOfType(List<GameObject> selected, SB_AssetInfo aInfo, bool showWindow)
        {
            if (!(selected.Count() > 0))
                Debug.LogWarning($"{ComponentTransferWindow.WINDOWNAME}: No prefab selected, so cant replace by type");

            var instances = selected.GetSceneInstances();
            if(instances !=null)
                replaceAsset(instances.ToArray(), aInfo, showWindow, true);
        }

        private void replaceAsset(List<GameObject> selected, SB_AssetInfo aInfo, bool showWindow, bool replaceByType)
        {
            replaceAsset(selected.Select(t => t.gameObject).ToArray(), aInfo, showWindow, replaceByType);
        }

        private void replaceAsset(Transform[] selected, SB_AssetInfo aInfo, bool showWindow, bool replaceByType)
        {
            replaceAsset(selected.Select(t => t.gameObject).ToArray(), aInfo, showWindow, replaceByType);
        }

        private void replaceAsset(GameObject[] selected, SB_AssetInfo aInfo, bool showWindow, bool replaceByType)
        {
            ComponentTransferConfig transferConfig = new ComponentTransferConfig(selected, aInfo.prefab, replaceByType);
            if (showWindow)
                ComponentTransferWindow.ShowConfig(transferConfig);
            else
                transferConfig.ReplaceAssets();
        }
    }
}