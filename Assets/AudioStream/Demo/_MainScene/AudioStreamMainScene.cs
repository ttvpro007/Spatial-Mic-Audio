// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
[ExecuteInEditMode]
/// <summary>
/// Displays all demo scenes list and provides their un/loading
/// If scene with this script is opened in the Editor, try to populate Build Settings with AudioStream scenes (since that is what the user most likely wants)
/// </summary>
public class AudioStreamMainScene : MonoBehaviour
{
    List<string> sceneNames = new List<string>();

    // (very) basic single GO instance handling
    static AudioStreamMainScene instance;
    /// <summary>
    /// Demo menu definition SO reference
    /// </summary>
    public AudioStreamDemoMenu audioStreamDemoMenu;

    void Awake()
    {
        if (AudioStreamMainScene.instance == null)
        {
            AudioStreamMainScene.instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            if (Application.isPlaying)
                // we are being loaded again - destroy ourselves since the other instance is already alive
                Destroy(this.gameObject);
            return;
        }
    }

    int selectedSceneGroup = 0;
    Vector2 scrollPosition = Vector2.zero;
    void OnGUI()
    {
        // display scene list on main scene
        if (SceneManager.GetActiveScene().name == this.audioStreamDemoMenu.mainSceneName)
        {
            AudioStreamDemoSupport.OnGUI_GUIHeader("");

            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
                DevicesConfiguration.Instance.ASIO = GUILayout.Toggle(DevicesConfiguration.Instance.ASIO, "Enable ASIO");

            if (AudioStream.DevicesConfiguration.Instance.ASIO)
            {
                GUILayout.Label("ASIO enabled", AudioStreamSupport.UX.guiStyleLabelNormal);

                var sBufferSize = AudioStream.DevicesConfiguration.Instance.ASIO_bufferSize.ToString();

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("ASIO buffer size (important: this should match ASIO config): ", AudioStreamSupport.UX.guiStyleLabelNormal);
                    sBufferSize = GUILayout.TextArea(sBufferSize, 5);
                }

                GUILayout.Label(string.Format("ASIO buffer count: {0} - FMOD default", AudioStream.DevicesConfiguration.Instance.ASIO_bufferCount), AudioStreamSupport.UX.guiStyleLabelNormal);

                if (uint.TryParse(sBufferSize, out var uBufferSize))
                    AudioStream.DevicesConfiguration.Instance.ASIO_bufferSize = uBufferSize;
            }

            GUILayout.Label("----------------------------------------------------------------------------------------");
            GUILayout.Label("Pick demo scenes group", AudioStreamSupport.UX.guiStyleLabelNormal);

            this.selectedSceneGroup = GUILayout.SelectionGrid(this.selectedSceneGroup
                , this.audioStreamDemoMenu.menuSections.Select(s => string.Format("{0}\r\n{1}", s.name, s.description)).ToArray()
                , 2
                , AudioStreamSupport.UX.guiStyleButtonNormal
                , GUILayout.MaxWidth(Screen.width));
            this.sceneNames = this.audioStreamDemoMenu.menuSections[this.selectedSceneGroup].sceneNames.ToList();

            GUILayout.Label("Press button to run a demo scene", AudioStreamSupport.UX.guiStyleLabelNormal);

            this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUIStyle());

            foreach (var sceneName in this.sceneNames)
            {
                if (GUILayout.Button(sceneName, AudioStreamSupport.UX.guiStyleButtonNormal))
                {
                    SceneManager.LoadScene(sceneName);
                    Resources.UnloadUnusedAssets();
                }
            }

            GUILayout.Label("----------------------------------------------------------------------------------------");

            // caches
            var fsz = AudioStreamSupport.FileSystem.DirectorySize(AudioStreamSupport.RuntimeSettings.downloadCachePath);
            GUILayout.Label(string.Format("[Clear/view download cache directory at {0}; current size: {1} b]", AudioStreamSupport.RuntimeSettings.downloadCachePath, fsz), AudioStreamSupport.UX.guiStyleLabelNormal);

            using (new GUILayout.HorizontalScope())
            {
                if (!Application.isMobilePlatform && !Application.isConsolePlatform)
                    if (GUILayout.Button("Open download cache folder", AudioStreamSupport.UX.guiStyleButtonNormal))
                    {
                        Application.OpenURL(AudioStreamSupport.RuntimeSettings.downloadCachePath);
                    }

                if (GUILayout.Button("Clear download cache folder", AudioStreamSupport.UX.guiStyleButtonNormal))
                {
                    foreach (var fp in System.IO.Directory.GetFiles(AudioStreamSupport.RuntimeSettings.downloadCachePath))
                    {
                        System.IO.File.Delete(fp);
                    }
                }
            }

            fsz = AudioStreamSupport.FileSystem.DirectorySize(AudioStreamSupport.RuntimeSettings.temporaryDirectoryPath);
            GUILayout.Label(string.Format("[Clear/view temporary/decoded samples directory at {0}; current size: {1} b]", AudioStreamSupport.RuntimeSettings.temporaryDirectoryPath, fsz), AudioStreamSupport.UX.guiStyleLabelNormal);

            using (new GUILayout.HorizontalScope())
            {
                if (!Application.isMobilePlatform && !Application.isConsolePlatform)
                    if (GUILayout.Button("Open temp dir. folder", AudioStreamSupport.UX.guiStyleButtonNormal))
                    {
                        Application.OpenURL(AudioStreamSupport.RuntimeSettings.temporaryDirectoryPath);
                    }

                if (GUILayout.Button("Clear temp dir. folder", AudioStreamSupport.UX.guiStyleButtonNormal))
                {
                    foreach (var fp in System.IO.Directory.GetFiles(AudioStreamSupport.RuntimeSettings.temporaryDirectoryPath))
                    {
                        System.IO.File.Delete(fp);
                    }
                }
            }

            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    // open player log button
                    if (GUILayout.Button("Open player log", AudioStreamSupport.UX.guiStyleButtonNormal))
                    {
                        if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
                        {
                            var log_path = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "..", "LocalLow", Application.companyName, Application.productName, "Player.log");
                            Application.OpenURL(log_path);
                        }
                    }
                    break;
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    GUILayout.Label(@"For log see Editor.log/Player.log in Console.app");
                    break;
            }

            // font size
            {
                GUILayout.Label("Font size / zoom: ", AudioStreamSupport.UX.guiStyleLabelNormal);

                using (new GUILayout.HorizontalScope())
                {
                    var sz = AudioStreamSupport.UX.fontSizeBase;

                    using (new GUILayout.HorizontalScope())
                    {
                        sz = (int)GUILayout.HorizontalSlider(sz, -1, 10);
                        GUILayout.Label(string.Format("{0}", sz));
                    }

                    if (sz != AudioStreamSupport.UX.fontSizeBase)
                    {
                        AudioStreamSupport.UX.ResetStyles();
                        AudioStreamSupport.UX.fontSizeBase = sz;
                    }
                }
            }

            GUILayout.Space(40);

            GUILayout.EndScrollView();
        }
        else
        {
            // display navigation bottom bar on a single scene

            // bottom bar line height
            var bHeight = Screen.height / 16;

            using (new GUILayout.AreaScope(new Rect(0, Screen.height - bHeight, Screen.width, bHeight)))
            {
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(" < ", GUILayout.MaxWidth(Screen.width / 3), GUILayout.MaxHeight(bHeight)))
                    {
                        var i = this.sceneNames.IndexOf(SceneManager.GetActiveScene().name) - 1;
                        if (i < 0)
                            i = this.sceneNames.Count - 1;

                        // find the scene in full scene list and load it
                        i = System.Array.IndexOf(this.audioStreamDemoMenu.menuSections[this.selectedSceneGroup].sceneNames, this.sceneNames[i]);
                        SceneManager.LoadScene(this.audioStreamDemoMenu.menuSections[this.selectedSceneGroup].sceneNames[i]);
                        Resources.UnloadUnusedAssets();
                    }

                    GUILayout.Label(SceneManager.GetActiveScene().name, AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 3), GUILayout.MaxHeight(bHeight));

                    if (GUILayout.Button("Return to main", GUILayout.MaxWidth(Screen.width / 3), GUILayout.MaxHeight(bHeight)))
                    {
                        SceneManager.LoadScene(this.audioStreamDemoMenu.mainSceneName);
                        Resources.UnloadUnusedAssets();
                    }
                    if (GUILayout.Button(" > ", GUILayout.MaxWidth(Screen.width / 3), GUILayout.MaxHeight(bHeight)))
                    {
                        var i = this.sceneNames.IndexOf(SceneManager.GetActiveScene().name) + 1;
                        if (i > this.sceneNames.Count - 1)
                            i = 0;

                        // find the scene in full scene list and load it
                        i = System.Array.IndexOf(this.audioStreamDemoMenu.menuSections[this.selectedSceneGroup].sceneNames,this.sceneNames[i]);
                        SceneManager.LoadScene(this.audioStreamDemoMenu.menuSections[this.selectedSceneGroup].sceneNames[i]);
                        Resources.UnloadUnusedAssets();
                    }
                }
            }
        }
    }
}