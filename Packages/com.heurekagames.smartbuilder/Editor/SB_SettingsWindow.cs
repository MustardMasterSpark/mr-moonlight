using HeurekaGames.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HeurekaGames.SmartBuilder
{
    public class SB_SettingsWindow : EditorWindow
    {
        private static SB_SettingsWindow m_window;
        private Vector2 scrollPos;

        public static void Open()
        {
            if (!m_window)
                initializeWindow();
            else
                m_window.Close();
        }
        private static void initializeWindow()
        {
            m_window = EditorWindow.GetWindow<SB_SettingsWindow>(false, SB_Preferences.AssetNameShort + " Settings");
            SB_Preferences.instance.InitializeIfNeeded();
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
            Heureka_WindowStyler.DrawGlobalHeader(Heureka_WindowStyler.clr_middleGreen, SB_Preferences.AssetNameShort + " Settings");
            using (var scrollbar = new EditorGUILayout.ScrollViewScope(scrollPos))
            {
                scrollPos = scrollbar.scrollPosition;
                SB_Preferences.instance.DoGUI();
            }
        }
    }
}
