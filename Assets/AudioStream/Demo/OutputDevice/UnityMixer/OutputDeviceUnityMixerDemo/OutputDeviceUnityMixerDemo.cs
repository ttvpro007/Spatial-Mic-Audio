// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using UnityEngine;

[ExecuteInEditMode()]
public class OutputDeviceUnityMixerDemo : MonoBehaviour
{
    void OnGUI()
    {
        GUILayout.Label("", AudioStreamSupport.UX.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamSupport.UX.guiStyleLabelSmall);
        // remove dependency on the rest of AudioStream completely to allow standalone usage for the plugin
        var versionString = "AudioStream © 2016-2023 Martin Cvengros, uses FMOD by Firelight Technologies Pty Ltd";
        GUILayout.Label(versionString, AudioStreamSupport.UX.guiStyleLabelMiddle);

        GUILayout.Label("This scene uses Windows x64 and macOS (*) Unity native audio mixer plugin for routing AudioClip signal to different system output/s directly from AudioMixer using AudioStreamOutputDevice mixer effect.", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("You should be hearing looping AudioClip played on output devices 1 and 2. If a device with given ID does not exist in the system, the default output (0) is used in that case instead.", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("Everything is set up just by configuring the mixer in the scene, no scripting is involved.", AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.Label("(*) see documentation for details and current limitations\r\nit also means this scene won't work on other platforms currently", AudioStreamSupport.UX.guiStyleLabelNormal);

        if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
        {
            GUILayout.Label(":!: AudioMixer effect on macOS :!: due to a bug in FMOD using the mixer plugin w/ hotplugging is not recommended - it should work for non changing outputs @ runtime.");
        }
    }
}