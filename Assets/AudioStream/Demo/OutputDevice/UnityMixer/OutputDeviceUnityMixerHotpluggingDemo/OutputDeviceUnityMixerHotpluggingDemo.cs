// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

public class OutputDeviceUnityMixerHotpluggingDemo : MonoBehaviour
{
    /// <summary>
    /// available audio outputs reported by FMOD
    /// </summary>
    List<FMOD_SystemW.OUTPUT_DEVICE> availableOutputs = new List<FMOD_SystemW.OUTPUT_DEVICE>();
    /// <summary>
    /// Mixer of AudioSource
    /// </summary>
    public AudioMixer audioMixer;
    /// <summary>
    /// 
    /// </summary>
    public AudioSourceOutputDevice audioSourceOutputDevice;

    #region UI events

    Dictionary<string, string> outputNotificationStatesFromEvents = new Dictionary<string, string>();

    public void OnError_OutputNotification(string goName, string msg)
    {
        this.outputNotificationStatesFromEvents[goName] = msg;
    }

    public void OnOutputDevicesChanged(string goName)
    {
        this.UpdateOutputDevicesList();
    }
    #endregion
    /// <summary>
    /// Audio devices change notification 
    /// </summary>
    void UpdateOutputDevicesList()
    {
        // update available outputs device list
        // use e.g. this.audioStream for log level and error logging
        this.availableOutputs = FMOD_SystemW.AvailableOutputs(this.audioSourceOutputDevice.logLevel, this.gameObject.name, this.audioSourceOutputDevice.OnError);

        //string msg = "Available outputs:" + System.Environment.NewLine;
        //for (int i = 0; i < this.availableOutputs.Count; ++i)
        //    msg += this.availableOutputs[i].id + " : " + this.availableOutputs[i].name + System.Environment.NewLine;
        //Debug.Log(msg);

        /*
         * do any custom reaction based on outputs change here
         */

        // for demo the mixer effect will always play on selectedOutput regardless on device list, so nothing to do here
    }
    /// <summary>
    /// User selected audio output driver id
    /// </summary>
    int selectedOutput = 0; // 0 is system default
    int previousSelectedOutput = -1;

    IEnumerator Start()
    {
        // example usage - do something only after components are .ready
        while (!this.audioSourceOutputDevice.ready)
            yield return null;

        // check for available outputs
        if (Application.isPlaying)
        {
            this.UpdateOutputDevicesList();
        }
    }

    Vector2 scrollPosition1 = Vector2.zero, scrollPosition2 = Vector2.zero;
    /// <summary>
    /// trigger UI change without user clicking on 1st screen showing
    /// </summary>
    bool guiStart = true;
    void OnGUI()
    {
        GUILayout.Label("", AudioStreamSupport.UX.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamSupport.UX.guiStyleLabelSmall);
        // remove dependency on the rest of AudioStream completely to allow standalone usage for the plugin
        var versionString = "AudioStream © 2016-2023 Martin Cvengros, uses FMOD by Firelight Technologies Pty Ltd";
        GUILayout.Label(versionString, AudioStreamSupport.UX.guiStyleLabelMiddle);

        GUILayout.Label("This scene will play AudioSource in scene via Unity AudioMixer with AudioStreamOutputDevice effect which OutputID is set based on user choice from all displayed devices currently available.", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("You can un/plug / change default device/s at runtime, the list will update, and internally the mixer effect will be in sync with new device list.", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("The mixer effect will always play on last selected device ID (even in new list). - you can customize this behaviour in OnDevicesChanged event of AudioStreamDevicesChangedNotify component in the scene.", AudioStreamSupport.UX.guiStyleLabelNormal);

        if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
        {
            GUILayout.Label(":!: AudioMixer effect on macOS :!: due to a bug in FMOD using the mixer plugin w/ hotplugging is not recommended, this scene will not work properly with devices changed @ runtime.");
        }

        // selection of available audio outputs at runtime
        // list can be long w/ special devices with many ports so wrap it in scroll view
        this.scrollPosition1 = GUILayout.BeginScrollView(this.scrollPosition1, new GUIStyle());

        GUILayout.Label("Available output devices:", AudioStreamSupport.UX.guiStyleLabelNormal);

        this.selectedOutput = GUILayout.SelectionGrid(this.selectedOutput, this.availableOutputs.Select((output, index) => string.Format("[Output #{0}]: {1}", index, output.name)).ToArray()
            , 1, AudioStreamSupport.UX.guiStyleButtonNormal);

        if (this.selectedOutput != this.previousSelectedOutput)
        {
            if ((Application.isPlaying
                // Indicate correct device in the list, but don't call output update if it was not due user changing / clicking it
                && Event.current.type == EventType.Used
                )
                || this.guiStart
                )
            {
                this.guiStart = false;

                this.audioMixer.SetFloat("OutputDeviceID", this.selectedOutput);
            }

            this.previousSelectedOutput = this.selectedOutput;
        }

        GUILayout.EndScrollView();


        GUI.color = Color.yellow;

        foreach (var p in this.outputNotificationStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamSupport.UX.guiStyleLabelNormal);

        GUI.color = Color.white;

        this.scrollPosition2 = GUILayout.BeginScrollView(this.scrollPosition2, new GUIStyle());

        GUILayout.Space(40);

        GUILayout.EndScrollView();
    }
}
