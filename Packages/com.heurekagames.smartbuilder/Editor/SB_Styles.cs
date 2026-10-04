using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HeurekaGames.SmartBuilder
{
    public static class SB_Styles
    {
        static GUIStyle headerBtnStyle;
        static GUIStyle overlayNameStyle;
        static GUIStyle overlayNameStyleBlack;   
        static GUIStyle imageBtnStyle;
        static GUIStyle toolbarBtnStyle;
        static GUIStyle miniButtonReduced;

        public static GUIStyle MiniButtonReduced
        {
            get
            {
                if (miniButtonReduced == null)
                { 
                    miniButtonReduced = new GUIStyle(EditorStyles.miniButton);
                    miniButtonReduced.padding = new RectOffset(2, 2, 0, 0);
                    miniButtonReduced.margin = new RectOffset(2, 2, 2, 2);
                }

                return miniButtonReduced;
            }
            set => miniButtonReduced = value;
        }

        public static GUIStyle LabelBtnStyle
        {
            get
            {
                if (headerBtnStyle == null)
                    headerBtnStyle = new GUIStyle(EditorStyles.label);

                return headerBtnStyle;
            }
            set => headerBtnStyle = value;
        }

        public static GUIStyle OverlayNameStyleWhite
        {
            get
            {
                if (overlayNameStyle == null)
                {
                    overlayNameStyle = new GUIStyle(GUI.skin.label);
                    overlayNameStyle.normal.textColor = Color.white;
                    overlayNameStyle.wordWrap = true;
                    overlayNameStyle.alignment = TextAnchor.UpperLeft;
                }
                if (overlayNameStyle.fontSize != SB_Preferences.instance.SimilarItemFontSize.Value)
                {
                    overlayNameStyle.fontSize = SB_Preferences.instance.SimilarItemFontSize.Value;
                }

                return overlayNameStyle;
            }
            set => overlayNameStyle = value;
        }

        public static GUIStyle OverlayNameStyle
        {
            get
            {
                if (overlayNameStyleBlack == null)
                {
                    overlayNameStyleBlack = new GUIStyle(overlayNameStyle);
                    overlayNameStyle.normal.textColor = GUI.skin.label.normal.textColor;
                }
                if (overlayNameStyleBlack.fontSize != SB_Preferences.instance.SimilarItemFontSize.Value)
                {
                    overlayNameStyleBlack.fontSize = SB_Preferences.instance.SimilarItemFontSize.Value;
                }

                return overlayNameStyleBlack;
            }
            set => overlayNameStyleBlack = value;
        }

        public static GUIStyle ImageBtnStyle
        {
            get
            {
                if (imageBtnStyle == null)
                {
                    imageBtnStyle = new GUIStyle(GUI.skin.button);
                    imageBtnStyle.padding = new RectOffset(0, 0, 0, 0);
                }
                return imageBtnStyle;
            }
            set => imageBtnStyle = value;
        }
    }
}