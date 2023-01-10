// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// [ExecuteInEditMode()]
public class AudioSourceOutputDeviceDemo : MonoBehaviour
{
    /// <summary>
    /// available audio outputs reported by FMOD
    /// </summary>
    List<FMOD_SystemW.OUTPUT_DEVICE> availableOutputs = new List<FMOD_SystemW.OUTPUT_DEVICE>();
    /// <summary>
    /// AudioStream with redirect attached
    /// </summary>
    public AudioStream.AudioStream audioStream;
    /// <summary>
    /// AudioStreamMinimal allows to change output directly
    /// </summary>
    public AudioStreamMinimal audioStreamMinimal;
    /// <summary>
    /// Unity AudioSource
    /// </summary>
    public AudioSourceOutputDevice audioSourceOutput;

    #region UI events

    Dictionary<string, string> streamsStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, string> redirectionStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, string> outputNotificationStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, Dictionary<string, string>> tags = new Dictionary<string, Dictionary<string, string>>();

    public void OnPlaybackStarted(string goName)
    {
        this.streamsStatesFromEvents[goName] = "playing";
    }

    public void OnPlaybackPaused(string goName, bool paused)
    {
        this.streamsStatesFromEvents[goName] = paused ? "paused" : "playing";
    }

    public void OnPlaybackStopped(string goName)
    {
        this.streamsStatesFromEvents[goName] = "stopped";
    }

    public void OnTagChanged(string goName, string _key, object _value)
    {
        // care only about 'meaningful' tags
        var key = _key.ToLowerInvariant();

        if (key == "artist" || key == "title")
        {
            // little juggling around dictionaries..

            if (this.tags.ContainsKey(goName))
                this.tags[goName][_key] = _value as string;
            else
                this.tags[goName] = new Dictionary<string, string>() { { _key, _value as string } };
        }
    }

    public void OnError_Audio(string goName, string msg)
    {
        this.streamsStatesFromEvents[goName] = msg;
    }

    public void OnRedirectionStarted(string goName)
    {
        this.redirectionStatesFromEvents[goName] = "redirection started";
    }

    public void OnRedirectionStopped(string goName)
    {
        this.redirectionStatesFromEvents[goName] = "redirection stopped";
    }

    public void OnError_Redirection(string goName, string msg)
    {
        this.redirectionStatesFromEvents[goName] = msg;
    }

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
        this.availableOutputs = FMOD_SystemW.AvailableOutputs(this.audioStream.logLevel, this.gameObject.name, this.audioStream.OnError);

        //string msg = "Available outputs:" + System.Environment.NewLine;
        //for (int i = 0; i < this.availableOutputs.Count; ++i)
        //    msg += this.availableOutputs[i].id + " : " + this.availableOutputs[i].name + System.Environment.NewLine;
        //Debug.Log(msg);

        /*
         * do any custom reaction based on outputs change here
         */

        // for demo we select correct displayed list item of playing output
        // since ASOD components update their output driver id automatically after devices change, just sync list with the id
        this.selectedOutput = this.audioSourceOutput.RuntimeOutputDriverID;
    }
    /// <summary>
    /// User selected audio output driver id
    /// </summary>
    int selectedOutput = 0; // 0 is system default
    int previousSelectedOutput = -1;

    IEnumerator Start()
    {
        // example usage - do something only after components are .ready
        while (!this.audioStream.ready || !this.audioStreamMinimal.ready || !this.audioSourceOutput.ready)
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
        AudioStreamDemoSupport.OnGUI_GUIHeader(this.audioStream ? " " + this.audioStream.fmodVersion : "");

        GUILayout.Label("This scene will play AudioStream playback components and regular Unity AudioSource on selected system output. Mixing is done by FMOD automatically.", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("Select output device and press Play (default output device is preselected). You can switch between outputs while playing.", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("NOTE: you can un/plug device/s in your system during runtime - the device list should update accordingly", AudioStreamSupport.UX.guiStyleLabelNormal);

        // selection of available audio outputs at runtime
        // list can be long w/ special devices with many ports so wrap it in scroll view
        this.scrollPosition1 = GUILayout.BeginScrollView(this.scrollPosition1, new GUIStyle());

        GUILayout.Label("Available output devices:", AudioStreamSupport.UX.guiStyleLabelNormal);

        this.selectedOutput = GUILayout.SelectionGrid(this.selectedOutput, this.availableOutputs.Select((output, index) => string.Format("[Output #{0}]: {1}", index, output.name)).ToArray()
            , 1, AudioStreamSupport.UX.guiStyleButtonNormal);

        GUILayout.Label(string.Format("-- user requested {0}, running on {1}", this.audioSourceOutput.outputDevice.name, this.audioSourceOutput.RuntimeOutputDriverID), AudioStreamSupport.UX.guiStyleLabelNormal);

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

                this.audioStream.SetOutput(this.selectedOutput);

                this.audioStreamMinimal.SetOutput(this.selectedOutput);

                this.audioSourceOutput.SetOutput(this.selectedOutput);
            }

            this.previousSelectedOutput = this.selectedOutput;
        }

        GUILayout.EndScrollView();


        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamSupport.UX.guiStyleLabelNormal);

        foreach (var p in this.redirectionStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamSupport.UX.guiStyleLabelNormal);

        foreach (var p in this.outputNotificationStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamSupport.UX.guiStyleLabelNormal);

        GUI.color = Color.white;

        this.scrollPosition2 = GUILayout.BeginScrollView(this.scrollPosition2, new GUIStyle());

        // AudioStream:

        FMOD.RESULT lastError;
        string lastErrorString = this.audioStream.GetLastError(out lastError);

        GUILayout.Label(this.audioStream.GetType() + "   ========================================", AudioStreamSupport.UX.guiStyleLabelSmall);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Stream: ", AudioStreamSupport.UX.guiStyleLabelNormal);
        this.audioStream.url = GUILayout.TextField(this.audioStream.url);
        GUILayout.EndHorizontal();

        GUILayout.Label(string.Format("State = {0} {1}"
            , this.audioStream.isPlaying ? "Playing" + (this.audioStream.isPaused ? " / Paused" : "") : "Stopped"
            , lastError + " " + lastErrorString
            )
            , AudioStreamSupport.UX.guiStyleLabelNormal);

        // AudioStream+AudioSourceOutpuDevice
        AudioSourceOutputDevice asod = this.audioStream.GetComponent<AudioSourceOutputDevice>();

        var pcmb = asod.PCMCallbackBuffer();
        var underflow = (pcmb != null && pcmb.underflow) ? ", underflow" : string.Empty;

        GUILayout.Label(string.Format("Input mix latency: {0} ms", asod.inputLatency), AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label(string.Format("Output device latency average: {0} ms{1}", asod.latencyAverage, underflow), AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.BeginHorizontal();

        GUILayout.Label("Volume: ", AudioStreamSupport.UX.guiStyleLabelNormal);

        var _as = this.audioStream.GetComponent<AudioSource>();
        _as.volume = GUILayout.HorizontalSlider(_as.volume, 0f, 1f);
        GUILayout.Label(Mathf.Round(_as.volume * 100f) + " %", AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(this.audioStream.isPlaying ? "Stop" : "Play", AudioStreamSupport.UX.guiStyleButtonNormal))
            if (this.audioStream.isPlaying)
                this.audioStream.Stop();
            else
                this.audioStream.Play();

        if (this.audioStream.isPlaying)
        {
            if (GUILayout.Button(this.audioStream.isPaused ? "Resume" : "Pause", AudioStreamSupport.UX.guiStyleButtonNormal))
                if (this.audioStream.isPaused)
                    this.audioStream.Pause(false);
                else
                    this.audioStream.Pause(true);
        }

        GUILayout.EndHorizontal();

        /*
         * took too much screen estate on demo scene when there are e.g. multiple output devices
        Dictionary<string, string> _tags;
        if (this.tags.TryGetValue(this.audioStream.name, out _tags))
            foreach (var d in _tags)
                GUILayout.Label(d.Key + ": " + d.Value, AudioStreamSupport.UX.guiStyleLabelNormal);
        */

        // AudioStreamMinimal:
        // uses default DSP buffers
        // TODO: implement FMODSourceOutpuDevice to cover both components in similar way..

        lastErrorString = this.audioStreamMinimal.GetLastError(out lastError);

        GUILayout.Label(this.audioStreamMinimal.GetType() + "   ========================================", AudioStreamSupport.UX.guiStyleLabelSmall);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Stream: ", AudioStreamSupport.UX.guiStyleLabelNormal);
        audioStreamMinimal.url = GUILayout.TextField(audioStreamMinimal.url);
        GUILayout.EndHorizontal();

        GUILayout.Label(string.Format("State = {0} {1}"
            , this.audioStreamMinimal.isPlaying ? "Playing" + (this.audioStreamMinimal.isPaused ? " / Paused" : "") : "Stopped"
            , lastError + " " + lastErrorString
            )
            , AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.BeginHorizontal();

        GUILayout.Label("Volume: ", AudioStreamSupport.UX.guiStyleLabelNormal);

        this.audioStreamMinimal.volume = GUILayout.HorizontalSlider(this.audioStreamMinimal.volume, 0f, 1f);
        GUILayout.Label(Mathf.Round(this.audioStreamMinimal.volume * 100f) + " %", AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(this.audioStreamMinimal.isPlaying ? "Stop" : "Play", AudioStreamSupport.UX.guiStyleButtonNormal))
            if (this.audioStreamMinimal.isPlaying)
                this.audioStreamMinimal.Stop();
            else
                this.audioStreamMinimal.Play();

        if (this.audioStreamMinimal.isPlaying)
        {
            if (GUILayout.Button(this.audioStreamMinimal.isPaused ? "Resume" : "Pause", AudioStreamSupport.UX.guiStyleButtonNormal))
                if (this.audioStreamMinimal.isPaused)
                    this.audioStreamMinimal.Pause(false);
                else
                    this.audioStreamMinimal.Pause(true);
        }

        GUILayout.EndHorizontal();

        /*
         * took too much screen estate on demo scene when there are e.g. multiple output devices
        if (this.tags.TryGetValue(this.audioStreamMinimal.name, out _tags))
            foreach (var d in _tags)
                GUILayout.Label(d.Key + ": " + d.Value, AudioStreamSupport.UX.guiStyleLabelNormal);
        
        */

        // standalone AudioSource:

        _as = this.audioSourceOutput.GetComponent<AudioSource>();

        lastErrorString = this.audioSourceOutput.GetLastError(out lastError);

        GUILayout.Label(_as.GetType() + "   ========================================", AudioStreamSupport.UX.guiStyleLabelSmall);

        GUILayout.Label("Common Unity AudioClip", AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.Label("Clip: " + _as.clip.name, AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label(string.Format("State = {0} {1}"
            , _as.isPlaying ? "Playing" : "Stopped"
            , lastError + " " + lastErrorString
            )
            , AudioStreamSupport.UX.guiStyleLabelNormal);

        // AudioSoutce+AudioSourceOutpuDevice

        pcmb = this.audioSourceOutput.PCMCallbackBuffer();
        underflow = (pcmb != null && pcmb.underflow) ? ", underflow" : string.Empty;

        GUILayout.Label(string.Format("Input mix latency: {0} ms", this.audioSourceOutput.inputLatency), AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label(string.Format("Output device latency average: {0} ms{1}", this.audioSourceOutput.latencyAverage, underflow), AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.BeginHorizontal();

        GUILayout.Label("Volume: ", AudioStreamSupport.UX.guiStyleLabelNormal);

        _as.volume = GUILayout.HorizontalSlider(_as.volume, 0f, 1f);
        GUILayout.Label(Mathf.Round(_as.volume * 100f) + " %", AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(_as.isPlaying ? "Stop" : "Play", AudioStreamSupport.UX.guiStyleButtonNormal))
            if (_as.isPlaying)
            {
                _as.Stop();

                this.OnPlaybackStopped(_as.gameObject.name);
            }
            else
            {
                _as.Play();

                this.OnPlaybackStarted(_as.gameObject.name);
            }

        GUILayout.EndHorizontal();

        GUILayout.Space(40);

        GUILayout.EndScrollView();
    }
}
