using HeurekaGames.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HeurekaGames.SmartBuilder
{
    public static class SB_GUIElementTags
    {
        static List<List<string>> tagRows = new List<List<string>>();
        private static EditorGUILayout.VerticalScope curScope;
        private static float maxWidth = 100f;

        public static void Draw()
        {
            //var tmpIgnores = new List<List<string>>();
            var tmpTagRowHolder = new List<List<string>>(tagRows);

            if (Event.current.type == EventType.Layout && curScope != null)
            {
                //Sometimes when you draw a window, the scope rect width is 0, so we counter that here
                if (curScope.rect.width != 0)
                    maxWidth = curScope.rect.width;
            }

            if (SB_Preferences.instance.IgnoredTags.Entries.Count > 0)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    string tagToClear = String.Empty;
                    GUIStyle toolbarStyle = new GUIStyle(EditorStyles.miniButton);

                    if (Event.current.type == EventType.Repaint)
                    {
                        tmpTagRowHolder = new List<List<string>>() { new List<string>() };
                        //var allowedWidth = maxWidth;
                        float currentX = 0;
                        int curRow = 0;

                        //Make sure the tags fit window width. If not, make new row
                        foreach (var item in SB_Preferences.instance.IgnoredTags.Entries)
                        {
                            var itemWidth = toolbarStyle.CalcSize(new GUIContent(item)).x;
                            currentX += itemWidth;

                            if (currentX <= maxWidth)
                                tmpTagRowHolder[curRow].Add(item);
                            else
                            {
                                curRow++;
                                tmpTagRowHolder.Add(new List<string>() { item });
                                currentX = (itemWidth); //Reset row position X
                            }
                        }
                    }

                    EditorGUILayout.LabelField(SB_Preferences.IgnoredTagsContent, EditorStyles.boldLabel);
                    using (curScope = new EditorGUILayout.VerticalScope("box"))
                    {
                        foreach (var row in tagRows)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                foreach (var tag in row)
                                {
                                    if (GUILayout.Button(tag))
                                    {
                                        tagToClear = tag;
                                    }
                                }
                                GUILayout.FlexibleSpace();
                            }
                        }
                        var style = new GUIStyle(GUI.skin.button);
                        style.normal.textColor = Heureka_WindowStyler.clr_Red;

                        if (GUILayout.Button("Clear ignored tags", style))
                        {
                            tagRows = new List<List<string>>();
                            SB_Preferences.instance.IgnoredTags.Clear();
                        }
                    }

                    if (!string.IsNullOrEmpty(tagToClear))
                    {
                        tagRows.Remove(tagRows.Find(x => x.Contains(tagToClear)));
                        SB_Preferences.instance.IgnoredTags.Remove(tagToClear);
                    }

                    tagRows = tmpTagRowHolder;
                }
            }
        }
    }
}
