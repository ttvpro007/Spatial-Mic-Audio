// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using UnityEngine;
/// <summary>
/// Menu definition for demo app; populated in Editor via 'Editor\AudioStreamDemoMenuDef'
/// </summary>
// [CreateAssetMenu(fileName = "AudioStreamDemoMenu", menuName = "AudioStream asset AudioStreamDemoMenu", order = 1201)]
public class AudioStreamDemoMenu : ScriptableObject
{
    public string mainSceneName;

    [System.Serializable]
    public struct MENU_SECTION
    {
        public string name;
        public string description;
        public string[] sceneNames;
    }

    public MENU_SECTION[] menuSections;
}