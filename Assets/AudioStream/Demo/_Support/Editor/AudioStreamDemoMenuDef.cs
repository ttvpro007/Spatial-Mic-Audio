// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace AudioStreamDemoEditorSupport
{
    /// <summary>
    /// simple singleton SO asset for defining demo menu structure, populates EditorBuildSettings with scenes if needed and runtime object with scene names
    /// </summary>
    public class AudioStreamDemoMenuDef : ScriptableObject
    {
        public AudioStreamDemoMenu runtimeMenu;

        public SceneAsset mainScene;

        [System.Serializable]
        public struct MENU_SECTION
        {
            public string name;
            public string description;
            public SceneAsset[] scenes;
        }

        public MENU_SECTION[] menuSections;

        [Tooltip("Uncheck this to prevent populating the Build Settings Scene List with AudioStream demo scenes\r\n(useful when keeping all demos in the project and making builds which don't run them)")]
        public bool copyDemoScenesToBuildSettings = true;

        #region Editor SO Singleton
        static AudioStreamDemoMenuDef instance;
        public static AudioStreamDemoMenuDef Instance
        {
            get
            {
                if (AudioStreamDemoMenuDef.instance == null)
                    AudioStreamDemoMenuDef.Load();

                return AudioStreamDemoMenuDef.instance;
            }
        }

        protected AudioStreamDemoMenuDef()
        {
            if (AudioStreamDemoMenuDef.instance != null)
                Debug.LogErrorFormat(@"Won't create anbother instance");
            else
                AudioStreamDemoMenuDef.instance = this;
        }
        static void Load()
        {
            var filePath = GetFilePath();
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                AudioStreamDemoMenuDef.instance = (AudioStreamDemoMenuDef)InternalEditorUtility.LoadSerializedFileAndForget(filePath)[0];
            }

            /*
             * the isntance is setup - won't create it
            if (s_Instance == null)
            {
                var inst = CreateInstance<T>() as ScriptableObjectSingleton<T>;
                Assert.IsFalse(inst == null);
                inst.hideFlags = HideFlags.HideAndDontSave;
                inst.Save();
            }
            */
        }
        static string GetFilePath()
        {
            var guid = AssetDatabase.FindAssets("t:AudioStreamDemoMenuDef", new string[] { "Assets" })[0];
            return AssetDatabase.GUIDToAssetPath(guid);
        }
        #endregion

        [InitializeOnEnterPlayMode]
        public static void UpdateRuntimeMenu()
        {
            // update Resources/runtime object

            // copy scene names
            var instance = AudioStreamDemoMenuDef.Instance;
            var menudef = AudioStreamDemoMenuDef.Instance.menuSections;

            instance.runtimeMenu.menuSections = new AudioStreamDemoMenu.MENU_SECTION[menudef.Length];

            for (var i = 0; i < menudef.Length; ++i)
            {
                instance.runtimeMenu.menuSections[i].name = menudef[i].name;
                instance.runtimeMenu.menuSections[i].description = menudef[i].description;
                instance.runtimeMenu.menuSections[i].sceneNames = new string[menudef[i].scenes.Length];

                for (var s = 0; s < menudef[i].scenes.Length; ++s)
                    instance.runtimeMenu.menuSections[i].sceneNames[s] = menudef[i].scenes[s].name;
            }
            // + main scene
            instance.runtimeMenu.mainSceneName = AudioStreamDemoMenuDef.Instance.mainScene.name;

            // update EditorBuildSettings, if user doesn't want to skip this
            if (!instance.copyDemoScenesToBuildSettings)
                return;

            // current all scenes in build settings
            var originalScenesInBuildSettings = UnityEditor.EditorBuildSettings.scenes;

            // find all audiostream demo scenes in the project
            List<string> audioStreamDemoScenePaths = new List<string>();

            // get all scene paths

            // + main scene
            audioStreamDemoScenePaths.Add(AssetDatabase.GetAssetPath(instance.mainScene));
            for (var i = 0; i < menudef.Length; ++i)
            {
                for (var s = 0; s < menudef[i].scenes.Length; ++s)
                    audioStreamDemoScenePaths.Add(AssetDatabase.GetAssetPath(menudef[i].scenes[s]));
            }

            // find which demo scenes are currently not in the build settings
            // scene paths match:
            // Scene in project:        Assets/AudioStream/Demo/_MainScene/AudioStreamMainScene.unity
            // Scene in Build Settings: Assets/AudioStream/Demo/_MainScene/AudioStreamMainScene.unity

            List<string> audioStreamDemoScenePathsToAdd = new List<string>();
            var originalScenesInBuildSettingsPaths = UnityEditor.EditorBuildSettings.scenes.Select(s => s.path);

            foreach (var scenePath in audioStreamDemoScenePaths)
            {
                if (!originalScenesInBuildSettingsPaths.Contains(scenePath))
                    audioStreamDemoScenePathsToAdd.Add(scenePath);
            }

            // new scenes for build settings
            List<UnityEditor.EditorBuildSettingsScene> newScenesInBuildSettings = new List<UnityEditor.EditorBuildSettingsScene>();

            // add all original scenes and make sure all demo scenes are enabled
            var updatedCount = 0;
            for (int i = 0; i < originalScenesInBuildSettings.Length; ++i)
            {
                if (audioStreamDemoScenePaths.Contains(originalScenesInBuildSettings[i].path))
                {
                    updatedCount += originalScenesInBuildSettings[i].enabled ? 0 : 1;
                    newScenesInBuildSettings.Add(new UnityEditor.EditorBuildSettingsScene(originalScenesInBuildSettings[i].path, true));
                }
                else
                    newScenesInBuildSettings.Add(originalScenesInBuildSettings[i]);
            }

            // add new scenes
            foreach (var scenePath in audioStreamDemoScenePathsToAdd)
                newScenesInBuildSettings.Add(new UnityEditor.EditorBuildSettingsScene(scenePath, true));

            if (updatedCount > 0 || audioStreamDemoScenePathsToAdd.Count > 0)
            {
                Debug.LogWarningFormat("Automatically enabled {0} and added {1} AudioStream demo scene/s to Build Settings scene list", updatedCount, audioStreamDemoScenePathsToAdd.Count);
                Debug.LogWarningFormat("Please restart the scene to reload Build Settings if a demo scene won't load");
            }

            // update editor build settings
            UnityEditor.EditorBuildSettings.scenes = newScenesInBuildSettings.ToArray();
        }
    }
}