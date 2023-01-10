// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using UnityEngine;

namespace AudioStreamSupport
{
    /// <summary>
    /// Build and environment info for UI
    /// Simple SO singleton based on Resources asset
    /// transfers info from Editor to runtime
    /// </summary>
    // [CreateAssetMenu(fileName = "AudioStreamRuntimeBuildInformation", menuName = "AudioStream asset RuntimeBuildInformation", order = 1201)]
    public class RuntimeBuildInformation : ScriptableObject
    {
        public string scriptingBackend = string.Empty;
        public string apiLevel = string.Empty;
        public string buildTime = string.Empty;                // GetType().Assembly.Location is empty on IL2CPP so can be used only at Editor time
        [Tooltip("For Unity 2022 and up this is retrieved from Player Settings ['Allow downloads over HTTP' field] to indicate whether insecure downloads are allowed to display info/warning message.")]
        public bool playerCanUseHTTP = true;
        public string buildString { get; protected set; }
        public string defaultOutputProperties { get; protected set; }


        protected static RuntimeBuildInformation instance = null;
        public static RuntimeBuildInformation Instance
        {
            get
            {
                if (instance == null)
                {
                    var assets = // Resources.FindObjectsOfTypeAll(typeof(T)) // - not consistent results when opening editor/project...
                        Resources.LoadAll("AudioStreamRuntimeBuildInformation")
                        ;

                    if (assets.Length != 1)
                    {
                        Debug.LogWarningFormat(@"Found {0} SO assets of {1} in Resources named {2}, expected 1. Please press Play to enter play mode to fix it.", assets.Length, typeof(RuntimeBuildInformation), "AudioStreamRuntimeBuildInformation");
                        Debug.LogFormat(@"Package ships by default with one named {0} created in 'Support\Resources' for all messaging in demo scene to work", "AudioStreamRuntimeBuildInformation");

                        return null;
                    }

                    RuntimeBuildInformation.instance = (RuntimeBuildInformation)assets[0];
                    RuntimeBuildInformation.instance.hideFlags = HideFlags.DontUnloadUnusedAsset;
                }

                return instance;
            }
        }


        void OnEnable()
        {
            this.buildString = string.Format("Built {0}, Unity version: {1}{2}{3}, {4} bit, {5}"
                    , this.buildTime
                    , Application.unityVersion
                    , !string.IsNullOrEmpty(this.scriptingBackend) ? (", " + this.scriptingBackend + " scripting backend") : ""
                    , !string.IsNullOrEmpty(this.apiLevel) ? (", " + this.apiLevel) : ""
                    , System.Runtime.InteropServices.Marshal.SizeOf(System.IntPtr.Zero) * 8
                    , UnityAudio.UnityAudioLatencyDescription()
                    );
            this.defaultOutputProperties = string.Format("System default output samplerate: {0}, application speaker mode: {1} [HW: {2}]", AudioSettings.outputSampleRate, AudioSettings.speakerMode, AudioSettings.driverCapabilities);
        }

        public string downloadsOverHTTPMessage
        {
            get
            {
                if (RuntimeBuildInformation.Instance
                    && !RuntimeBuildInformation.Instance.playerCanUseHTTP)
                    return "Please enable downloads over HTTP in PlayerSettings in order to use HTTP links in the demo. Go to Edit -> Project Settings -> Player -> Other Settings > Configuration and set 'Allow downloads over HTTP' to 'Always allowed'. Alternatively, use secure (HTTPS) links only.";
                else
                    return null;
            }
        }
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        [UnityEditor.InitializeOnEnterPlayMode]
        static void UpdateRuntimeInformation()
        {
            // don't NRE on not found Resource
            if (!RuntimeBuildInformation.Instance)
                return;

            var buildTargetGroup = UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup;
            RuntimeBuildInformation.Instance.scriptingBackend = UnityEditor.PlayerSettings.GetScriptingBackend(buildTargetGroup).ToString();
            RuntimeBuildInformation.Instance.apiLevel = UnityEditor.PlayerSettings.GetApiCompatibilityLevel(buildTargetGroup).ToString();
            RuntimeBuildInformation.Instance.buildTime = System.IO.File.GetLastWriteTimeUtc(typeof(RuntimeBuildInformation).Assembly.Location).ToString("MMMM dd yyyy");

            RuntimeBuildInformation.Instance.playerCanUseHTTP
#if UNITY_2022_1_OR_NEWER
                = RuntimeBuildInformation.Instance.playerCanUseHTTP = UnityEditor.PlayerSettings.insecureHttpOption == UnityEditor.InsecureHttpOption.AlwaysAllowed;
#else
                = true;
#endif
        }
#endif
    }
}