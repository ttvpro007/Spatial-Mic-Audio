// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using AudioStreamSupport;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode]
public class AudioStreamInputDemo : MonoBehaviour
{
    public AudioStreamInput audioStreamInput;
    public AudioTexture_OutputData audioTexture_OutputData;
    public AudioTexture_SpectrumData audioTexture_SpectrumData;
    /// <summary>
    /// available audio outputs reported by FMOD
    /// </summary>
    List<FMOD_SystemW.INPUT_DEVICE> availableInputs = new List<FMOD_SystemW.INPUT_DEVICE>();

    #region UI events
    Dictionary<string, string> inputStreamsStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, string> inputNotificationStatesFromEvents = new Dictionary<string, string>();

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
    public void OnError_InputNotification(string goName, string msg)
    {
        this.inputNotificationStatesFromEvents[goName] = msg;
    }

    public void OnRecordDevicesChanged(string goName)
    {
        // update device list
        var inputs = FMOD_SystemW.AvailableInputs(this.audioStreamInput.logLevel, this.audioStreamInput.gameObject.name, this.audioStreamInput.OnError, this.includeLoopbacks);
        if (inputs.Count < 1)
            return;

        this.availableInputs = inputs;

        /*
         * do any custom reaction based on inputs change here
         */

        // for demo we select correct displayed list item of used input
        var input = this.availableInputs.FirstOrDefault(f => f.guid == this.inputDevice.guid);

        if (input.guid != System.Guid.Empty)
        {
            this.selectedListInput = this.availableInputs.IndexOf(input);
        }
        else
            // device disappeared - let's select first input in that case
            this.selectedListInput = 0;

        // update input properties
        this.inputDevice = this.availableInputs[this.selectedListInput];

        // position in the list != record driver id - need to reference driver id indirectly
        this.audioStreamInput.recordDeviceId = this.availableInputs[this.selectedListInput].id;
    }
    #endregion
    /// <summary>
    /// User selected audio input driver id
    /// </summary>
    int selectedListInput = 0;
    int previousSelectedListInput = -1;
    /// <summary>
    /// Selected input w/ all its properties
    /// </summary>
    FMOD_SystemW.INPUT_DEVICE inputDevice;
    /// <summary>
    /// DSP OnGUI
    /// </summary>
    uint dspBufferLength, dspBufferCount;
    /// <summary>
    /// Output channels based on current Unity audio settings
    /// - we need this for / this should be == to/ OAFR signal
    /// </summary>
    int outputChannels = 0;

    // signal energy per channel for UI
    float[] recBuffer = new float[512];
    List<float> signalChannelsEnergies = new List<float>();

    // RMS -> reaction cubes per channel
    // rms reaction values - being attached to audioStreamInput they compute their values from its audio filter,
    // also attached above needed AudioSourceMute component, which needs to be 'last' one - at the bottom - otherwise the signal would be supressed before it reaches rms components
    public RMSPerChannelToTransforms rmsPerChannelToTransforms;
    // audio reaction cubes
    GameObject[] cubes;
    /// <summary>
    /// Include loop back interfaces
    /// </summary>
    bool includeLoopbacks = true;

    IEnumerator Start()
    {
        while (!this.audioStreamInput.ready)
            yield return null;

        // check for available inputs
        if (Application.isPlaying)
        {
            string msg = "Available inputs:" + System.Environment.NewLine;

            this.availableInputs = FMOD_SystemW.AvailableInputs(this.audioStreamInput.logLevel, this.audioStreamInput.gameObject.name, this.audioStreamInput.OnError, this.includeLoopbacks);

            for (int i = 0; i < this.availableInputs.Count; ++i)
                msg += this.availableInputs[i].id + " : " + this.availableInputs[i].name + System.Environment.NewLine;

            Debug.Log(msg);
        }

        // get user buffer since we don't have possible initial change triggered
        this.audioStreamInput.GetDSPBufferSize(out this.dspBufferLength, out this.dspBufferCount);

        // get output channels#
        this.outputChannels = AudioStreamSupport.UnityAudio.ChannelsFromUnityDefaultSpeakerMode();

        // setup visuals

        // start RMS distribution per channel on output audio filter
        this.rmsPerChannelToTransforms.SetChannels(this.outputChannels);

        // crete cubes
        this.cubes = new GameObject[this.outputChannels];

        for (var i = 0; i < this.outputChannels; ++i)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // set some (editor) id
            cube.gameObject.name = string.Format("Channel #{0}", i.ToString());
            // set some rotation
            cube.transform.rotation = Quaternion.Euler(-45f, -35.7f, 0f);

            // place cubes evenly across the screen along x at y,z = 0
            var screenX = (Camera.main.pixelWidth / (float)(this.outputChannels + 1)) * (i + 1);
            var distanceFromCamera = Mathf.Abs(Camera.main.transform.position.z);
            var ray = Camera.main.ScreenPointToRay(new Vector3(screenX, 0, 0));

            cube.transform.position = new Vector3((ray.direction * distanceFromCamera).x, 0, 0);

            this.cubes[i] = cube;
        }

        // setup signalChannelsEnergies based on current output
        this.signalChannelsEnergies = new List<float>();
        for (var i = 0; i < this.outputChannels; ++i)
            this.signalChannelsEnergies.Add(0f);
    }

    void Update()
    {
        if (this.audioStreamInput.isRecording)
        {
            // access the recording buffer and look at some values

            var _as = this.audioStreamInput.GetComponent<AudioSource>();

            for (int ch = 0; ch < this.outputChannels; ++ch)
            {
                _as.GetOutputData(this.recBuffer, ch);

                var signalEnergy = 0f;

                for (int i = 0; i < this.recBuffer.Length; ++i)
                    signalEnergy += this.recBuffer[i] * this.recBuffer[i];

                this.signalChannelsEnergies[ch] = signalEnergy;
            }

            // distribute RMS values per channel to cubes
            for (var i = 0; i < this.outputChannels; ++i)
            {
                this.cubes[i].transform.localScale = this.rmsPerChannelToTransforms.scale[i];
                this.cubes[i].transform.rotation = this.rmsPerChannelToTransforms.rotation[i];
            }
        }
    }

    Vector2 scrollPosition1 = Vector2.zero, scrollPosition2 = Vector2.zero;
    void OnGUI()
    {
        AudioStreamDemoSupport.OnGUI_GUIHeader(this.audioStreamInput ? " " + this.audioStreamInput.fmodVersion : "");
        AudioStreamSupport.UX.OnGUI_AudioTextures(this.audioTexture_OutputData, this.audioTexture_SpectrumData);

        GUILayout.Label("Choose from available recording devices and press Record.\r\nThe cubes will react to sound - signal energy is computed from AudioSource's GetOutputData for each channel separately.\r\nNotice the latency is much higher than in AudioStreamInput2DDemo scene, but the sound is spatialized by Unity spatializer; the Rolloff is set to Linear.", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("You can move the ball (AudioSource) using [W/A/S/D], reset its position with [R].", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("For full 3D spatialization see also Resonance components in 'Spatialization' section of the main screen.", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("NOTE: you can un/plug device/s from your system during runtime - the device list will update accordingly", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("Available recording devices:", AudioStreamSupport.UX.guiStyleLabelNormal);

        // wait for startup
        if (this.availableInputs.Count < 1)
            return;

        if (!DevicesConfiguration.Instance.ASIO)
        {
            var _includeLoopbacks = GUILayout.Toggle(this.includeLoopbacks, " Include loopback interfaces [you can turn this off to filter only recording devices Unity's Microphone class can see]\r\nPlease note: recording from and playing a loopback device will immediately cause feedback loop - be sure to mute the AudioSource similarly like in this scene.");
            if (_includeLoopbacks != this.includeLoopbacks)
            {
                this.includeLoopbacks = _includeLoopbacks;
                this.availableInputs = FMOD_SystemW.AvailableInputs(this.audioStreamInput.logLevel, this.audioStreamInput.gameObject.name, this.audioStreamInput.OnError, this.includeLoopbacks);
                // small reselect if out of range..
                this.selectedListInput = 0;
            }
        }

        // selection of available audio inputs at runtime
        // list can be long w/ special devices with many ports so wrap it in scroll view
        this.scrollPosition1 = GUILayout.BeginScrollView(this.scrollPosition1, new GUIStyle());

        this.selectedListInput = GUILayout.SelectionGrid(this.selectedListInput, this.availableInputs.Select((input, index) => string.Format("{0}[Input #{1} |ID {2}|] {3} rate: {4} speaker mode: {5} channels: {6}", input.isDefault ? "(*) " : "", index, input.id, input.name, input.samplerate, input.speakermode, input.channels)).ToArray()
            , 1
            , AudioStreamSupport.UX.guiStyleButtonNormal
            , GUILayout.MaxWidth(Screen.width)
            );

        if (this.selectedListInput != this.previousSelectedListInput)
        {
            if (Application.isPlaying)
            {
                this.audioStreamInput.Stop();
            }

            this.inputDevice = this.availableInputs[this.selectedListInput];

            // position in the list != record driver id - need to reference actual driver id as retrieved by FMOD
            this.audioStreamInput.recordDeviceId = this.availableInputs[this.selectedListInput].id;

            this.previousSelectedListInput = this.selectedListInput;
        }

        GUILayout.EndScrollView();

        GUI.color = Color.yellow;

        foreach (var p in this.inputStreamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamSupport.UX.guiStyleLabelNormal);

        foreach (var p in this.inputNotificationStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamSupport.UX.guiStyleLabelNormal);

        this.scrollPosition2 = GUILayout.BeginScrollView(this.scrollPosition2, new GUIStyle());

        // wait for startup

        if (this.availableInputs.Count > 0)
        {
            GUI.color = Color.white;

            FMOD.RESULT lastError;
            string lastErrorString = this.audioStreamInput.GetLastError(out lastError);

            GUILayout.Label(this.audioStreamInput.GetType() + "   ========================================", AudioStreamSupport.UX.guiStyleLabelSmall);

            GUILayout.Label(string.Format("State = {0} {1}"
                , this.audioStreamInput.isRecording ? "Recording" + (this.audioStreamInput.isPaused ? " / Paused" : "") : "Stopped"
                , lastError + " " + lastErrorString
                )
                , AudioStreamSupport.UX.guiStyleLabelNormal);

            GUILayout.Label("Signal energy per output channel from GetOutputData: ", AudioStreamSupport.UX.guiStyleLabelNormal);
            GUILayout.BeginHorizontal();
            for (var i = 0; i < this.signalChannelsEnergies.Count; ++i)
                GUILayout.Label(string.Format("CH #{0}: {1}", i, System.Math.Round(this.signalChannelsEnergies[i], 6).ToString().PadRight(8, '0')));
            GUILayout.EndHorizontal();

            // DSP buffers info
            this.audioStreamInput.GetDSPBufferSize(out this.dspBufferLength, out this.dspBufferCount);

            GUILayout.BeginHorizontal();
            GUILayout.Label("DSP buffer length: ");
            GUILayout.Label(this.dspBufferLength.ToString());
            GUILayout.Label("DSP buffer count: ");
            GUILayout.Label(this.dspBufferCount.ToString());
            GUILayout.EndHorizontal();

            GUILayout.Label(string.Format("Input mixer latency average: {0} ms", this.audioStreamInput.latencyAverage), AudioStreamSupport.UX.guiStyleLabelNormal);

            GUILayout.Label("Recording will automatically restart if it was running after changing these.", AudioStreamSupport.UX.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();

            GUILayout.Label("Gain: ", AudioStreamSupport.UX.guiStyleLabelNormal);

            this.audioStreamInput.recordGain = GUILayout.HorizontalSlider(this.audioStreamInput.recordGain, 0f, 5f);
            GUILayout.Label(Mathf.Round(this.audioStreamInput.recordGain * 100f) + " %", AudioStreamSupport.UX.guiStyleLabelNormal);

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(this.audioStreamInput.isRecording ? "Stop" : "Record", AudioStreamSupport.UX.guiStyleButtonNormal))
                if (this.audioStreamInput.isRecording)
                    this.audioStreamInput.Stop();
                else
                    this.audioStreamInput.Record();

            if (this.audioStreamInput.isRecording)
            {
                if (GUILayout.Button(this.audioStreamInput.isPaused ? "Resume" : "Pause", AudioStreamSupport.UX.guiStyleButtonNormal))
                    if (this.audioStreamInput.isPaused)
                        this.audioStreamInput.Pause(false);
                    else
                        this.audioStreamInput.Pause(true);
            }

            GUILayout.EndHorizontal();

            this.audioStreamInput.GetComponent<AudioSourceMute>().mute = GUILayout.Toggle(this.audioStreamInput.GetComponent<AudioSourceMute>().mute, "Mute output");
        }

        GUILayout.Space(40);

        GUILayout.EndScrollView();
    }
}
