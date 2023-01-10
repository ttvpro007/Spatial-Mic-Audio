// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode()]
public class AudioSourceOutputChannelsDemo : MonoBehaviour
{
    /// <summary>
    /// Available audio outputs reported by FMOD
    /// </summary>
    List<FMOD_SystemW.OUTPUT_DEVICE> availableOutputs = new List<FMOD_SystemW.OUTPUT_DEVICE>();
    /// <summary>
    /// Always running FMOD component w output devices and their channels
    /// </summary>
    public AudioSourceOutputDevice audioSourceOutput;
    /// <summary>
    /// output gain of an output channel
    /// can be inverted
    /// </summary>
    [Range(-2f, 2f)]
    public float outputLevel = 1f;
    /// <summary>
    /// UI change
    /// </summary>
    float previousOutputLevel = 1f;

    /// <summary>
    /// Sets custom mix matrix on playing FMOD channel of selected output device based on currently selected output channel
    /// </summary>
    void SetCustomMixMatrix()
    {
        // not y ready
        if (this.availableOutputs.Count <= this.selectedOutput)
            return;
        /*
         * Based on https://www.fmod.com/docs/2.02/api/core-api-common.html#fmod_speakermode and https://www.fmod.com/docs/2.02/api/core-api-channelcontrol.html#channelcontrol_setmixmatrix
         * we set a custom mix matrix on playing FMOD channel assuming current output and MONO input clip
         * 
         * From the above: Sound channels map to speakers sequentially, so a mono sound maps to output speaker 0, stereo sound maps to output speaker 0 & 1.
         * The user assumes knowledge of the speaker order. FMOD_SPEAKER enumerations may not apply, so raw channel indices should be used.
         * Multichannel sounds map input channels to output channels 1:1.
         * 
         * Matrix is outputs(rows) x inputs(columns)
         * The gain for input channel 's' to output channel 't' is matrix[t * matrixhop + s].
         * Levels can be below 0 to invert a signal and above 1 to amplify the signal.Note that increasing the signal level too far may cause audible distortion.
         * 
         * Ex. identity would be:
         * var identity4x4 = new float[16] {
         *  1f, 0f, 0f, 0f,
         *  0f, 1f, 0f, 0f,
         *  0f, 0f, 1f, 0f,
         *  0f, 0f, 0f, 1f
         *  };
         */

        // in case of MONO input we have matrix with outpuchannels rows and 1 column:
        var outchannels = this.availableOutputs[this.selectedOutput].channels;
        var inchannels = 1; // MONO source

        var mixMatrix = new float[outchannels * inchannels];
        System.Array.Clear(mixMatrix, 0, mixMatrix.Length);

        // we'll set level just on requested output channel:
        mixMatrix[this.selectedOutputChannel] = this.outputLevel;

        this.audioSourceOutput.SetUnitySound_MixMatrix(mixMatrix, outchannels, inchannels);
    }

    #region UI events

    Dictionary<string, string> redirectionStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, string> outputNotificationStatesFromEvents = new Dictionary<string, string>();

    public void OnRedirectionStarted(string goName)
    {
        this.redirectionStatesFromEvents[goName] = "redirection started";
    }

    public void OnRedirectionStopped(string goName)
    {
        this.redirectionStatesFromEvents[goName] = "redirection stopped";
    }

    public void OnError(string goName, string msg)
    {
        this.redirectionStatesFromEvents[goName] = msg;
    }

    public void OnError_OutputNotification(string goName, string msg)
    {
        this.outputNotificationStatesFromEvents[goName] = msg;
    }

    public void OnOutputDevicesChanged(string goName)
    {
        this.outputNotificationStatesFromEvents[goName] = "Devices changed";

        // update available outputs device list
        this.availableOutputs = FMOD_SystemW.AvailableOutputs(this.audioSourceOutput.logLevel, this.audioSourceOutput.gameObject.name, this.audioSourceOutput.OnError);

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
    #endregion
    /// <summary>
    /// User selected audio output driver id
    /// </summary>
    int selectedOutput = 0; // 0 is system default
    int previousSelectedOutput = 0;
    /// <summary>
    /// user selected output channel of multichannel output device
    /// </summary>
    int selectedOutputChannel = 0;
    int previousSelectedOutputChannel = -1; // trigger channel change at start

    IEnumerator Start()
    {
        while (!this.audioSourceOutput.ready)
            yield return null;

        // check for available outputs once ready, i.e. FMOD is started up
        if (Application.isPlaying)
        {
            string msg = "Available outputs:" + System.Environment.NewLine;

            this.availableOutputs = FMOD_SystemW.AvailableOutputs(this.audioSourceOutput.logLevel, this.audioSourceOutput.gameObject.name, this.audioSourceOutput.OnError);

            for (int i = 0; i < this.availableOutputs.Count; ++i)
                msg += this.availableOutputs[i].id + " : " + this.availableOutputs[i].name + System.Environment.NewLine;

            Debug.Log(msg);
        }
    }

    Vector2 scrollPosition1 = Vector2.zero, scrollPosition2 = Vector2.zero;
    /// <summary>
    /// trigger UI change without user clicking on 1st screen showing
    /// </summary>
    bool guiStart = true;
    void OnGUI()
    {
        AudioStreamDemoSupport.OnGUI_GUIHeader(this.audioSourceOutput.fmodVersion);

        GUILayout.Label("This scene will play common Unity AudioSource with a MONO AudioClip on selected output device on selected channel of that output\r\n" +
            "The output mix matrix can be set only for *all* audio played on chosen output device using this component, so this has drawback that it's not possible to play multiple AudioClips on different output channels simultaneously (or you would have to prepare all your multichannel clips beforehand and set your custom - single - mix matrix)\r\n" +
            "Please see 'MediaSourceOutputDeviceDemo' on how to play multiple audio files simultaneously on different output channels (only using FMOD, not using Unity audio)\r\n" +
            "(a MONO clip is used to more easily map it to a single output channel)", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("Select output device and press Play (default output device is preselected).\r\nYou can switch between outputs while playing.", AudioStreamSupport.UX.guiStyleLabelNormal);

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

                this.audioSourceOutput.SetOutput(this.selectedOutput);

                this.selectedOutputChannel = 0;
                this.previousSelectedOutputChannel = -1; // trigger channel change
            }

            this.previousSelectedOutput = this.selectedOutput;
        }

        GUILayout.EndScrollView();

        GUILayout.Space(10);

        GUILayout.Label("Select output channel of the selected output device to play the MONO clip on.\r\nYou can switch between outputs while playing.", AudioStreamSupport.UX.guiStyleLabelNormal);

        this.scrollPosition2 = GUILayout.BeginScrollView(this.scrollPosition2, new GUIStyle());

        // Display button for each channel of currently selected output device once everything is available
        if (this.availableOutputs.Count > this.selectedOutput)
        {
            var channels = Enumerable.Range(0, this.availableOutputs[this.selectedOutput].channels)
                .Select(s => string.Format("CH #{0}", s));

            this.selectedOutputChannel = GUILayout.SelectionGrid(this.selectedOutputChannel, channels.Select((input, index) => string.Format("{0}: {1}", index, input)).ToArray()
                , 4, AudioStreamSupport.UX.guiStyleButtonNormal);

            if (this.selectedOutputChannel != this.previousSelectedOutputChannel)
            {
                if (Application.isPlaying)
                {
                    this.SetCustomMixMatrix();
                }

                this.previousSelectedOutputChannel = this.selectedOutputChannel;
            }
        }

        GUILayout.Label("You can adjust gain of the output channel (note that negative values will invert the signal): ");
        GUILayout.BeginHorizontal();
        this.outputLevel = (float)System.Math.Round(
            GUILayout.HorizontalSlider(this.outputLevel, -1.2f, 1.2f, GUILayout.MaxWidth(Screen.width / 2))
            , 2
            );
        GUILayout.Label(this.outputLevel.ToString(), GUILayout.MaxWidth(Screen.width / 2));
        GUILayout.EndHorizontal();

        if (this.outputLevel != this.previousOutputLevel)
        {
            this.SetCustomMixMatrix();
            this.previousOutputLevel = this.outputLevel;
        }

        GUI.color = Color.yellow;

        foreach (var p in this.redirectionStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamSupport.UX.guiStyleLabelNormal);

        foreach (var p in this.outputNotificationStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamSupport.UX.guiStyleLabelNormal);
        
        GUI.color = Color.white;

        FMOD.RESULT lastError;
        string lastErrorString;

        // standalone AudioSource:

        var _as = this.audioSourceOutput.GetComponent<AudioSource>();

        lastErrorString = this.audioSourceOutput.GetLastError(out lastError);

        GUILayout.Label(_as.GetType() + "   ========================================", AudioStreamSupport.UX.guiStyleLabelSmall);

        GUILayout.Label("Common Unity MONO AudioClip", AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.Label("Clip: " + _as.clip.name, AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label(string.Format("State = {0} {1}"
            , _as.isPlaying ? "Playing" : "Stopped"
            , lastError + " " + lastErrorString
            )
            , AudioStreamSupport.UX.guiStyleLabelNormal);

        var pcmb = this.audioSourceOutput.PCMCallbackBuffer();
        var underflow = (pcmb != null && pcmb.underflow) ? ", underflow" : string.Empty;

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
            }
            else
            {
                _as.Play();
            }

        GUILayout.EndHorizontal();

        GUILayout.Space(40);

        GUILayout.EndScrollView();
    }
}
