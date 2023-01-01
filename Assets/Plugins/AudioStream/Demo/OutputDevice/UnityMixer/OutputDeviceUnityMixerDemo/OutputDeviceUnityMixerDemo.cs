// (c) 2016-2022 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using UnityEngine;

[ExecuteInEditMode()]
public class OutputDeviceUnityMixerDemo : MonoBehaviour
{
    void OnGUI()
    {
        GUILayout.Label("", AudioStreamDemoSupport.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamDemoSupport.guiStyleLabelSmall);
        // remove dependency on the rest of AudioStream completely to allow standalone usage for the plugin
        var versionString = "AudioStream © 2016-2022 Martin Cvengros, uses FMOD by Firelight Technologies Pty Ltd";
        GUILayout.Label(versionString, AudioStreamDemoSupport.guiStyleLabelMiddle);

        GUILayout.Label("This scene uses Windows x64 and macOS (*) Unity native audio mixer plugin for routing AudioClip signal to different system output/s directly from AudioMixer using AudioStreamOutputDevice mixer effect.", AudioStreamDemoSupport.guiStyleLabelNormal);
        GUILayout.Label("You should be hearing looping AudioClip played on output devices 1 and 2. If a device with given ID does not exist in the system, the default output (0) is used in that case instead.", AudioStreamDemoSupport.guiStyleLabelNormal);
        GUILayout.Label("Everything is set up just by configuring the mixer in the scene, no scripting is involved.", AudioStreamDemoSupport.guiStyleLabelNormal);

        GUILayout.Label("(*) see documentation for details and current limitations\r\nit also means this scene won't work on other platforms currently", AudioStreamDemoSupport.guiStyleLabelNormal);

        if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
        {
            GUILayout.Label(":!: AudioMixer effect on macOS :!: due to a bug in FMOD using the mixer plugin w/ hotplugging is not recommended - it should work for non changing outputs @ runtime.");
        }
    }
}