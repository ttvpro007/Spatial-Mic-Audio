using System.Collections;
using System.Collections.Generic;
using AudioStream;
using UnityEngine;

public class SpatialMicDemo : MonoBehaviour
{
    public ResonanceInput resonanceInput;
    List<FMOD_SystemW.INPUT_DEVICE> availableInputs = new List<FMOD_SystemW.INPUT_DEVICE>();
    
    public void OnRecordingStarted(string goName)
    {
        this.inputStreamsStatesFromEvents[goName] = "recording";
    }

    public void OnRecordingPaused(string goName, bool paused)
    {
        this.inputStreamsStatesFromEvents[goName] = paused ? "paused" : "recording";
    }

    public void OnRecordingStopped(string goName)
    {
        this.inputStreamsStatesFromEvents[goName] = "stopped";
    }

    public void OnError(string goName, string msg)
    {
        this.inputStreamsStatesFromEvents[goName] = msg;
    }

    public void OnRecordDevicesChanged(string goName)
    {
        // update device list
        if (this.resonanceInput.ready)
            this.availableInputs = FMOD_SystemW.AvailableInputs(this.resonanceInput.logLevel, this.resonanceInput.gameObject.name, this.resonanceInput.OnError, false);
    }

    Dictionary<string, string> inputStreamsStatesFromEvents = new Dictionary<string, string>();

    void OnGUI()
    {
        foreach (var p in this.inputStreamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamDemoSupport.guiStyleLabelNormal);
    }
}