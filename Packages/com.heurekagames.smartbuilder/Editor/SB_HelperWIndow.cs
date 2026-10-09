using UnityEngine;
using UnityEditor;
using System;

namespace HeurekaGames.SmartBuilder
{
    public class SB_HelperWindow : PopupWindowContent
    {
        private Func<Vector2> drawBespokeHelperGUI;
        private Vector2 scrollpos;
        private Vector2 windowSize = new Vector2(10,10);

        public SB_HelperWindow(Func<Vector2> bespokeGUI)
        {
            drawBespokeHelperGUI = bespokeGUI;
        }

        public override Vector2 GetWindowSize()
        {
            return windowSize;
        }

        public override void OnGUI(Rect rect)
        {
            using (var scrollbar = new EditorGUILayout.ScrollViewScope(scrollpos))
            {
                scrollpos = scrollbar.scrollPosition;
                windowSize = drawBespokeHelperGUI();
            }
        }
    }
}