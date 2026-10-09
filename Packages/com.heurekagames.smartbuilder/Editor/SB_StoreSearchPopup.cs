using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace HeurekaGames.SmartBuilder
{
    public class SB_StoreSearchPopup : PopupWindowContent
    {
        private Dictionary<string, bool> relevantTagsDict;

        public SB_StoreSearchPopup(List<string> relevantTags)
        {
            relevantTagsDict = new Dictionary<string, bool>();
            foreach (var tag in relevantTags.Distinct())
            {
                relevantTagsDict.Add(tag, true);
            }
        }

        public override Vector2 GetWindowSize()
        {
            var longestTag = relevantTagsDict.Select(x => x.Key).OrderByDescending(x => x.Length).First();
            GUIContent content = new GUIContent(longestTag);

            GUIStyle style = EditorStyles.toggle;
            var elementSize = style.CalcSize(content);
            elementSize.x = Mathf.Max(elementSize.x + 20, EditorGUIUtility.labelWidth + 20);

            return new Vector2(elementSize.x, 60 + (relevantTagsDict.Count * (elementSize.y+5)));
        }

        public override void OnGUI(Rect rect)
        {
            GUILayout.Label("Search asset store for:", EditorStyles.boldLabel);

            var dictCopy = new Dictionary<string, bool>(relevantTagsDict);
            foreach (var tag in dictCopy)
            {
                relevantTagsDict[tag.Key] = EditorGUILayout.Toggle(tag.Key, tag.Value);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Search"))
            {
                var link = Utils.Heureka_Utils.GetAssetStoreSearchLink(relevantTagsDict.Where(x => x.Value == true).Select(x => x.Key));
                Application.OpenURL(link);
            }
        }
    }
}