// (c) 2022-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using UnityEngine;

namespace AudioStreamSupport
{
    /// <summary>
    /// common (mainly demo) Gui/UX helpers
    /// </summary>
    public static class UX
    {
        // ========================================================================================================================================
        #region OnGUI styles
        static GUIStyle _guiStyleLabelSmall = GUIStyle.none;
        public static int fontSizeBase = 0;
        public static GUIStyle guiStyleLabelSmall
        {
            get
            {
                if (UX._guiStyleLabelSmall == GUIStyle.none)
                {
                    UX._guiStyleLabelSmall = new GUIStyle(GUI.skin.GetStyle("Label"));
                    UX._guiStyleLabelSmall.fontSize = 8 + UX.fontSizeBase;
                    UX._guiStyleLabelSmall.margin = new RectOffset(0, 0, 0, 0);
                }
                return UX._guiStyleLabelSmall;
            }
            private set { UX._guiStyleLabelSmall = value; }
        }
        static GUIStyle _guiStyleLabelMiddle = GUIStyle.none;
        public static GUIStyle guiStyleLabelMiddle
        {
            get
            {
                if (UX._guiStyleLabelMiddle == GUIStyle.none)
                {
                    UX._guiStyleLabelMiddle = new GUIStyle(GUI.skin.GetStyle("Label"));
                    UX._guiStyleLabelMiddle.fontSize = 10 + UX.fontSizeBase;
                    UX._guiStyleLabelMiddle.margin = new RectOffset(0, 0, 0, 0);
                }
                return UX._guiStyleLabelMiddle;
            }
            private set { UX._guiStyleLabelMiddle = value; }
        }
        static GUIStyle _guiStyleLabelNormal = GUIStyle.none;
        public static GUIStyle guiStyleLabelNormal
        {
            get
            {
                if (UX._guiStyleLabelNormal == GUIStyle.none)
                {
                    UX._guiStyleLabelNormal = new GUIStyle(GUI.skin.GetStyle("Label"));
                    UX._guiStyleLabelNormal.fontSize = 11 + UX.fontSizeBase;
                    UX._guiStyleLabelNormal.margin = new RectOffset(0, 0, 0, 0);
                }
                return UX._guiStyleLabelNormal;
            }
            private set { UX._guiStyleLabelNormal = value; }
        }
        static GUIStyle _guiStyleButtonNormal = GUIStyle.none;
        public static GUIStyle guiStyleButtonNormal
        {
            get
            {
                if (UX._guiStyleButtonNormal == GUIStyle.none)
                {
                    UX._guiStyleButtonNormal = new GUIStyle(GUI.skin.GetStyle("Button"));
                    UX._guiStyleButtonNormal.fontSize = 14 + UX.fontSizeBase;
                    UX._guiStyleButtonNormal.margin = new RectOffset(5, 5, 5, 5);
                }
                return UX._guiStyleButtonNormal;
            }
            private set { UX._guiStyleButtonNormal = value; }
        }
        public static void ResetStyles()
        {
            UX.guiStyleButtonNormal =
                UX.guiStyleLabelMiddle =
                UX.guiStyleLabelNormal =
                UX.guiStyleLabelSmall =
                GUIStyle.none;
        }
        #endregion
        // ========================================================================================================================================
        #region OnGUI
        public static void OnGUI_Header(string fullVersion)
        {
            GUILayout.Label("", UX.guiStyleLabelSmall); // statusbar on mobile overlay
            GUILayout.Label("", UX.guiStyleLabelSmall);
            GUILayout.Label(fullVersion, UX.guiStyleLabelMiddle);
            GUILayout.Label(RuntimeBuildInformation.Instance.buildString, UX.guiStyleLabelMiddle);
            GUILayout.Label(RuntimeBuildInformation.Instance.defaultOutputProperties, UX.guiStyleLabelMiddle);
        }
        // OnGUI audio textures
        public static void OnGUI_AudioTextures(AudioTexture_OutputData audioTexture_OutputData, AudioTexture_SpectrumData audioTexture_SpectrumData)
        {
            if (audioTexture_OutputData && audioTexture_OutputData.outputTexture)
                GUI.DrawTexture(new Rect(0
                                    , (Screen.height / 2)
                                    , Screen.width
                                    , audioTexture_OutputData.outputTexture.height
                                    )
                , audioTexture_OutputData.outputTexture
                , ScaleMode.StretchToFill
                );

            if (audioTexture_SpectrumData && audioTexture_SpectrumData.outputTexture)
                GUI.DrawTexture(new Rect(0
                                    , (Screen.height / 2) + (audioTexture_OutputData && audioTexture_OutputData.outputTexture ? audioTexture_OutputData.outputTexture.height : 0)
                                    , Screen.width
                                    , audioTexture_SpectrumData.outputTexture.height
                                    )
                , audioTexture_SpectrumData.outputTexture
                , ScaleMode.StretchToFill
                );
        }
        #endregion
    }
}