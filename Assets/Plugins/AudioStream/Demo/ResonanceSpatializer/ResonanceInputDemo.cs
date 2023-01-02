﻿// (c) 2016-2022 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode]
public class ResonanceInputDemo : MonoBehaviour
{
    public ResonanceInput resonanceInput;
    public ResonanceMicrophoneSelector microphoneSelector;
    /// <summary>
    /// available audio outputs reported by FMOD
    /// </summary>
    List<FMOD_SystemW.INPUT_DEVICE> availableInputs = new List<FMOD_SystemW.INPUT_DEVICE>();

    #region UI events
    Dictionary<string, string> inputStreamsStatesFromEvents = new Dictionary<string, string>();

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
            this.availableInputs = FMOD_SystemW.AvailableInputs(this.resonanceInput.logLevel, this.resonanceInput.gameObject.name, this.resonanceInput.OnError, this.includeLoopbacks);
    }
    #endregion
    /// <summary>
    /// User selected audio output driver id
    /// </summary>
    int selectedInput = 0; // 0 is system default
    int previousSelectedInput = 0;
    /// <summary>
    /// DSP OnGUI
    /// </summary>
    uint dspBufferLength, dspBufferCount;
    /// <summary>
    /// Include loop back interfaces
    /// </summary>
    bool includeLoopbacks = true;

    IEnumerator Start()
    {
        while (!this.resonanceInput.ready)
            yield return null;

        // check for available inputs
        if (Application.isPlaying)
        {
            string msg = "Available inputs:" + System.Environment.NewLine;

            this.availableInputs = FMOD_SystemW.AvailableInputs(this.resonanceInput.logLevel, this.resonanceInput.gameObject.name, this.resonanceInput.OnError, this.includeLoopbacks);

            for (int i = 0; i < this.availableInputs.Count; ++i)
                msg += this.availableInputs[i].id + " : " + this.availableInputs[i].name + System.Environment.NewLine;

            Debug.Log(msg);
        }
    }

    Vector2 scrollPosition1 = Vector2.zero, scrollPosition2 = Vector2.zero;
    void OnGUI()
    {
        AudioStreamDemoSupport.GUIHeader(this.resonanceInput ? " " + this.resonanceInput.fmodVersion : "");

        GUILayout.Label("Input audio is being played via FMOD's provided Google Resonance plugin.");
        GUILayout.Label(">> W/S/A/D/Arrows to move || Left Shift/Ctrl to move up/down || Mouse to look || 'R' to reset listener position <<");
        GUILayout.Label("");

        GUILayout.Label("Choose from available recording devices and press Record.\r\nThe input singal will be processed by Resonance and played from the cube's 3D position.", AudioStreamDemoSupport.guiStyleLabelNormal);

        GUILayout.Label("Available recording devices:", AudioStreamDemoSupport.guiStyleLabelNormal);

        if (!DevicesConfiguration.Instance.ASIO)
        {
            var _includeLoopbacks = GUILayout.Toggle(this.includeLoopbacks, " Include loopback interfaces [you can turn this off to filter only recording devices Unity's Microphone class can see]");
            if (_includeLoopbacks != this.includeLoopbacks)
            {
                this.includeLoopbacks = _includeLoopbacks;
                this.availableInputs = FMOD_SystemW.AvailableInputs(this.resonanceInput.logLevel, this.resonanceInput.gameObject.name, this.resonanceInput.OnError, this.includeLoopbacks);
                // small reselect if out of range..
                this.selectedInput = 0;
            }
            
            if (_includeLoopbacks)
                this.selectedInput = microphoneSelector.Id;
        }

        // selection of available audio inputs at runtime
        // list can be long w/ special devices with many ports so wrap it in scroll view
        this.scrollPosition1 = GUILayout.BeginScrollView(this.scrollPosition1, new GUIStyle());

        this.selectedInput = GUILayout.SelectionGrid(this.selectedInput, this.availableInputs.Select((input, index) => string.Format("[Input #: {0}] {1} rate: {2} speaker mode: {3} channels: {4}", index, input.name, input.samplerate, input.speakermode, input.channels)).ToArray()
            , 1
            , AudioStreamDemoSupport.guiStyleButtonNormal
            , GUILayout.MaxWidth(Screen.width)
            );

        if (this.selectedInput != this.previousSelectedInput)
        {
            if (Application.isPlaying)
            {
                this.resonanceInput.Stop();
                this.resonanceInput.recordDeviceId = this.selectedInput;
            }

            this.previousSelectedInput = this.selectedInput;
        }

        GUILayout.EndScrollView();

        GUI.color = Color.yellow;

        foreach (var p in this.inputStreamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamDemoSupport.guiStyleLabelNormal);

        this.scrollPosition2 = GUILayout.BeginScrollView(this.scrollPosition2, new GUIStyle());

        // wait for startup

        if (this.availableInputs.Count > 0)
        {
            GUI.color = Color.white;

            FMOD.RESULT lastError;
            string lastErrorString = this.resonanceInput.GetLastError(out lastError);

            GUILayout.Label(this.resonanceInput.GetType() + "   ========================================", AudioStreamDemoSupport.guiStyleLabelSmall);

            GUILayout.Label(string.Format("State = {0} {1}"
                , this.resonanceInput.isRecording ? "Recording" + (this.resonanceInput.isPaused ? " / Paused" : "") : "Stopped"
                , lastError + " " + lastErrorString
                )
                , AudioStreamDemoSupport.guiStyleLabelNormal);

            // DSP buffers info
            this.resonanceInput.GetDSPBufferSize(out this.dspBufferLength, out this.dspBufferCount);

            GUILayout.BeginHorizontal();
            GUILayout.Label("DSP buffer length: ");
            GUILayout.Label(this.dspBufferLength.ToString());
            GUILayout.Label("DSP buffer count: ");
            GUILayout.Label(this.dspBufferCount.ToString());
            GUILayout.EndHorizontal();

            GUILayout.Label(string.Format("Input mixer latency average: {0} ms", this.resonanceInput.latencyAverage), AudioStreamDemoSupport.guiStyleLabelNormal);

            GUILayout.Label("Recording will automatically restart if it was running after changing these.", AudioStreamDemoSupport.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();

            GUILayout.Label("Gain: ", AudioStreamDemoSupport.guiStyleLabelNormal);

            this.resonanceInput.gain = GUILayout.HorizontalSlider(this.resonanceInput.gain, 0f, 10f);
            GUILayout.Label(Mathf.Round(this.resonanceInput.gain * 100f) + " %", AudioStreamDemoSupport.guiStyleLabelNormal);

            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();

            if (GUILayout.Button(this.resonanceInput.isRecording ? "Stop" : "Record", AudioStreamDemoSupport.guiStyleButtonNormal))
                if (this.resonanceInput.isRecording)
                    this.resonanceInput.Stop();
                else
                    this.resonanceInput.Record();

            if (this.resonanceInput.isRecording)
            {
                if (GUILayout.Button(this.resonanceInput.isPaused ? "Resume" : "Pause", AudioStreamDemoSupport.guiStyleButtonNormal))
                    if (this.resonanceInput.isPaused)
                        this.resonanceInput.Pause(false);
                    else
                        this.resonanceInput.Pause(true);
            }

            GUILayout.EndHorizontal();

            // TODO: enable once AudioSource/DSP interop works
            // this.resonanceInput.GetComponent<AudioSourceMute>().mute = GUILayout.Toggle(this.resonanceInput.GetComponent<AudioSourceMute>().mute, "Mute output");
        }

        GUILayout.Space(40);

        GUILayout.EndScrollView();
    }
}
