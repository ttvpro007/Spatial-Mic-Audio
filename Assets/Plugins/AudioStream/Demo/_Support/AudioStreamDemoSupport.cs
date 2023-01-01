// (c) 2016-2022 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System.Collections;
using UnityEngine;

public static class AudioStreamDemoSupport
{
    #region styles
    static GUIStyle _guiStyleLabelSmall = GUIStyle.none;
    public static int fontSizeBase = 0;
    public static GUIStyle guiStyleLabelSmall
    {
        get
        {
            if (AudioStreamDemoSupport._guiStyleLabelSmall == GUIStyle.none)
            {
                AudioStreamDemoSupport._guiStyleLabelSmall = new GUIStyle(GUI.skin.GetStyle("Label"));
                AudioStreamDemoSupport._guiStyleLabelSmall.fontSize = 8 + AudioStreamDemoSupport.fontSizeBase;
                AudioStreamDemoSupport._guiStyleLabelSmall.margin = new RectOffset(0, 0, 0, 0);
            }
            return AudioStreamDemoSupport._guiStyleLabelSmall;
        }
        private set { AudioStreamDemoSupport._guiStyleLabelSmall = value; }
    }
    static GUIStyle _guiStyleLabelMiddle = GUIStyle.none;
    public static GUIStyle guiStyleLabelMiddle
    {
        get
        {
            if (AudioStreamDemoSupport._guiStyleLabelMiddle == GUIStyle.none)
            {
                AudioStreamDemoSupport._guiStyleLabelMiddle = new GUIStyle(GUI.skin.GetStyle("Label"));
                AudioStreamDemoSupport._guiStyleLabelMiddle.fontSize = 10 + AudioStreamDemoSupport.fontSizeBase;
                AudioStreamDemoSupport._guiStyleLabelMiddle.margin = new RectOffset(0, 0, 0, 0);
            }
            return AudioStreamDemoSupport._guiStyleLabelMiddle;
        }
        private set { AudioStreamDemoSupport._guiStyleLabelMiddle = value; }
    }
    static GUIStyle _guiStyleLabelNormal = GUIStyle.none;
    public static GUIStyle guiStyleLabelNormal
    {
        get
        {
            if (AudioStreamDemoSupport._guiStyleLabelNormal == GUIStyle.none)
            {
                AudioStreamDemoSupport._guiStyleLabelNormal = new GUIStyle(GUI.skin.GetStyle("Label"));
                AudioStreamDemoSupport._guiStyleLabelNormal.fontSize = 11 + AudioStreamDemoSupport.fontSizeBase;
                AudioStreamDemoSupport._guiStyleLabelNormal.margin = new RectOffset(0, 0, 0, 0);
            }
            return AudioStreamDemoSupport._guiStyleLabelNormal;
        }
        private set { AudioStreamDemoSupport._guiStyleLabelNormal = value; }
    }
    static GUIStyle _guiStyleButtonNormal = GUIStyle.none;
    public static GUIStyle guiStyleButtonNormal
    {
        get
        {
            if (AudioStreamDemoSupport._guiStyleButtonNormal == GUIStyle.none)
            {
                AudioStreamDemoSupport._guiStyleButtonNormal = new GUIStyle(GUI.skin.GetStyle("Button"));
                AudioStreamDemoSupport._guiStyleButtonNormal.fontSize = 14 + AudioStreamDemoSupport.fontSizeBase;
                AudioStreamDemoSupport._guiStyleButtonNormal.margin = new RectOffset(5, 5, 5, 5);
            }
            return AudioStreamDemoSupport._guiStyleButtonNormal;
        }
        private set { AudioStreamDemoSupport._guiStyleButtonNormal = value; }
    }
    public static void ResetStyles()
    {
        AudioStreamDemoSupport.guiStyleButtonNormal =
            AudioStreamDemoSupport.guiStyleLabelMiddle =
            AudioStreamDemoSupport.guiStyleLabelNormal =
            AudioStreamDemoSupport.guiStyleLabelSmall =
            GUIStyle.none;
    }
    #endregion

    #region filesystem demo assets
    /// <summary>
    /// On Android copies a file out of application archive StreamingAssets into external storage directory and returns its new file path
    /// On all other platforms just returns StreamingAssets location directly
    /// </summary>
    /// <param name="filename">file name in StreamingAssets</param>
    /// <param name="newDestination">called with new file path destination once file is copied out</param>
    /// <returns></returns>
    public static IEnumerator GetFilenameFromStreamingAssets(string filename, System.Action<string> newDestination)
    {
        var sourceFilepath = System.IO.Path.Combine(System.IO.Path.Combine(Application.streamingAssetsPath, "AudioStream") , filename);

        if (Application.platform == RuntimePlatform.Android)
        {
            using (AndroidJavaClass jcEnvironment = new AndroidJavaClass("android.os.Environment"))
            {
                using (AndroidJavaObject joExDir = jcEnvironment.CallStatic<AndroidJavaObject>("getExternalStorageDirectory"))
                {
                    var destinationDirectory = joExDir.Call<string>("toString");
                    var destinationPath = System.IO.Path.Combine(destinationDirectory, filename);

                    // 2018_3 has first deprecation warning
#if UNITY_2018_3_OR_NEWER
                    using (var www = UnityEngine.Networking.UnityWebRequest.Get(sourceFilepath))
                    {
                        yield return www.SendWebRequest();

                        if (!string.IsNullOrEmpty(www.error)
#if UNITY_2020_2_OR_NEWER
                            || www.result != UnityEngine.Networking.UnityWebRequest.Result.Success
#else
                            || www.isNetworkError
                            || www.isHttpError
#endif
                            )
                        {
                            Debug.LogErrorFormat("Can't find {0} in StreamingAssets ({1}): {2}", filename, sourceFilepath, www.error);

                            yield break;
                        }

                        while (!www.downloadHandler.isDone)
                            yield return null;

                        Debug.LogFormat("Copying streaming asset, {0}b", www.downloadHandler.data.Length);

                        System.IO.File.WriteAllBytes(destinationPath, www.downloadHandler.data);
                    }
#else
                    using (WWW www = new WWW(sourceFilepath))
                    {
                        yield return www;

                        if (!string.IsNullOrEmpty(www.error))
                        {
                            Debug.LogErrorFormat("Can't find {0} in StreamingAssets ({1}): {2}", filename, sourceFilepath, www.error);

                            yield break;
                        }

                        System.IO.File.WriteAllBytes(destinationPath, www.bytes);
                    }
#endif
                    sourceFilepath = destinationPath;
                }
            }
        }

        newDestination.Invoke(sourceFilepath);
    }
    #endregion

    #region common UI
    public static void GUIHeader(string fmodVersion)
    {
        GUILayout.Label("", AudioStreamDemoSupport.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamDemoSupport.guiStyleLabelSmall);
        GUILayout.Label(AudioStream.About.versionString + AudioStream.About.fmodNotice + (!string.IsNullOrEmpty(fmodVersion) ? " " + fmodVersion : ""), AudioStreamDemoSupport.guiStyleLabelMiddle);
        GUILayout.Label(RuntimeBuildInformation.Instance.buildString, AudioStreamDemoSupport.guiStyleLabelMiddle);
        GUILayout.Label(RuntimeBuildInformation.Instance.defaultOutputProperties, AudioStreamDemoSupport.guiStyleLabelMiddle);

        if (AudioStream.DevicesConfiguration.Instance.ASIO
            && !string.IsNullOrEmpty(fmodVersion)
            )
            GUILayout.Label(string.Format("ASIO enabled, buffer size: {0}", AudioStream.DevicesConfiguration.Instance.ASIO_bufferSize), AudioStreamDemoSupport.guiStyleLabelMiddle);

        if (!string.IsNullOrEmpty(RuntimeBuildInformation.Instance.downloadsOverHTTPMessage)) { GUI.color = Color.magenta; GUILayout.Label(RuntimeBuildInformation.Instance.downloadsOverHTTPMessage); GUI.color = Color.white; }
    }
    public static void GUIAudioTextures(AudioTexture_OutputData audioTexture_OutputData, AudioTexture_SpectrumData audioTexture_SpectrumData)
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
