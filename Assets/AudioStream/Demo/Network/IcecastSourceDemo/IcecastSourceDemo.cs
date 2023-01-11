// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using AudioStreamSupport;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode()]
public class IcecastSourceDemo : MonoBehaviour
{
    public AudioStream.AudioStream audioStream;
    public AudioStream.AudioStreamInput2D audioStreamInput2D;

    public IcecastSource icecastSource;
    /// <summary>
    /// available audio outputs reported by FMOD
    /// </summary>
    List<FMOD_SystemW.INPUT_DEVICE> availableInputs = new List<FMOD_SystemW.INPUT_DEVICE>();
    /// <summary>
    /// User selected audio output driver id
    /// </summary>
    int selectedInput = 0; // 0 is system default
    int previousSelectedInput = 0;

    #region UI events
    Dictionary<string, string> streamsStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, string> serverStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, Dictionary<string, object>> tags = new Dictionary<string, Dictionary<string, object>>();

    #region AudioStream events
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
        //
        // for ID2v2 tags present in mp3 files:
        // album art is under 'APIC_##' tag name
        // where '##' is e.g. '0' ( Other ), or '3' Cover(front), see all types in 'Picture type' in AudioStreamBase comments
        //
        // TIT1, TIT2, TIT3: related to title
        // TPE1, TPE2, TPE3, TPE4: related to performer / artist
        // TALB is album/show
        // for more see http://id3.org/id3v2.3.0
        //
        if (
            // shoutcast/icecast radio tags
            key == "artist"
            || key == "album"
            || key == "title"
            // ID3v2 tags
            || key.StartsWith("apic")
            || key.StartsWith("tit")
            || key.StartsWith("tpe")
            || key.StartsWith("talb")
            )
        {
            if (key.StartsWith("apic"))
                Debug.LogFormat("ID3v2 album artwork type {0} present", key.Split('_')[1]);

            // little juggling around dictionaries..

            if (this.tags.ContainsKey(goName))
                this.tags[goName][_key] = _value;
            else
                this.tags[goName] = new Dictionary<string, object>() { { _key, _value } };
        }
    }

    public void OnError_Audio(string goName, string msg)
    {
        this.streamsStatesFromEvents[goName] = msg;
    }
    #endregion

    #region AudioStreamInput2D events
    public void OnRecordingStarted(string goName)
    {
        this.streamsStatesFromEvents[goName] = "recording";
    }

    public void OnRecordingPaused(string goName, bool paused)
    {
        this.streamsStatesFromEvents[goName] = paused ? "paused" : "recording";
    }

    public void OnRecordingStopped(string goName)
    {
        this.streamsStatesFromEvents[goName] = "stopped";
    }

    public void OnError_Input(string goName, string msg)
    {
        this.streamsStatesFromEvents[goName] = msg;
    }
    public void OnRecordDevicesChanged(string goName)
    {
        // update device list
        if (this.audioStreamInput2D.ready)
            this.availableInputs = FMOD_SystemW.AvailableInputs(this.audioStreamInput2D.logLevel, this.audioStreamInput2D.gameObject.name, this.audioStreamInput2D.OnError, this.includeLoopbacks);
    }

    #endregion

    #region IcecastSource events
    public void OnServerConnected(string goName)
    {
        this.serverStatesFromEvents[goName] = "server connected";
    }

    public void OnServerDisconnected(string goName)
    {
        this.serverStatesFromEvents[goName] = "server disconnected";
    }

    public void OnError_Network(string goName, string msg)
    {
        this.serverStatesFromEvents[goName] = msg;
    }
    #endregion
    #endregion
    /// <summary>
    /// Include loop back interfaces
    /// </summary>
    bool includeLoopbacks = true;

    IEnumerator Start()
    {
        while (!this.audioStreamInput2D.ready)
            yield return null;

        // check for available inputs
        if (Application.isPlaying)
        {
            string msg = "Available inputs:" + System.Environment.NewLine;

            this.availableInputs = FMOD_SystemW.AvailableInputs(this.audioStreamInput2D.logLevel, this.audioStreamInput2D.gameObject.name, this.audioStreamInput2D.OnError, this.includeLoopbacks);

            for (int i = 0; i < this.availableInputs.Count; ++i)
                msg += this.availableInputs[i].id + " : " + this.availableInputs[i].name + System.Environment.NewLine;

            Debug.Log(msg);
        }

        // we might have set recid outside of current system - just sync with default
        this.audioStreamInput2D.recordDeviceId = this.selectedInput;
    }

    Vector2 scrollPosition1 = Vector2.zero, scrollPosition2 = Vector2.zero;
    void OnGUI()
    {
        AudioStreamDemoSupport.OnGUI_GUIHeader("");

        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamSupport.UX.guiStyleLabelNormal);

        foreach (var p in this.serverStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamSupport.UX.guiStyleLabelNormal);

        GUI.color = Color.white;

        GUILayout.Label("IcecastSource component is used to push audio output of a GameObject to an Icecast mountpoint\r\nPress Play to start playback of the AudioSource (AudioStream is used as an example) and/or select a recording device and press Record.\r\nIcecastSource is attached to the main listener, so it pushes combined audio to the Icecast mountpoint, once connected.");

        // GUILayout.HorizontalScope appeared first in 5.0.0f2 so should be safe to use !
        using (new GUILayout.HorizontalScope())
        {
            using (new GUILayout.VerticalScope(GUILayout.MaxWidth(Screen.width / 2)))
            {
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

                GUILayout.Label(string.Format("Time: {0}", Log.TimeStringFromSeconds(this.audioStream.PositionInSeconds)), AudioStreamSupport.UX.guiStyleLabelNormal);

                GUILayout.BeginHorizontal();

                GUILayout.Label("Volume: ", AudioStreamSupport.UX.guiStyleLabelNormal);

                var @as = this.audioStream as AudioStream.AudioStream;

                var asource = @as.GetComponent<AudioSource>();
                asource.volume = GUILayout.HorizontalSlider(asource.volume, 0f, 1f);
                GUILayout.Label(Mathf.Round(asource.volume * 100f) + " %", AudioStreamSupport.UX.guiStyleLabelNormal);

                GUILayout.EndHorizontal();

                /*
                GUILayout.BeginHorizontal();
                GUILayout.Label("Stream buffer size: ", AudioStreamSupport.UX.guiStyleLabelNormal);
                uint bs = this.audioStream.streamBufferSize;
                if (uint.TryParse(
                    GUILayout.TextField(this.audioStream.streamBufferSize.ToString())
                    , out bs)
                    )
                    this.audioStream.streamBufferSize = bs;
                GUILayout.Label(" bytes", AudioStreamSupport.UX.guiStyleLabelNormal);
                GUILayout.EndHorizontal();
                */

                GUILayout.BeginHorizontal();

                if (GUILayout.Button(audioStream.isPlaying ? "Stop" : "Play", AudioStreamSupport.UX.guiStyleButtonNormal))
                {
                    if (audioStream.isPlaying)
                        audioStream.Stop();
                    else
                    {
                        if (audioStream.ready)
                        {
                            // clear any previous tags when starting
                            this.tags.Remove(audioStream.gameObject.name);

                            audioStream.Play();
                        }
                    }
                }

                if (audioStream.isPlaying)
                {
                    if (GUILayout.Button(audioStream.isPaused ? "Resume" : "Pause", AudioStreamSupport.UX.guiStyleButtonNormal))
                        if (audioStream.isPaused)
                            audioStream.Pause(false);
                        else
                            audioStream.Pause(true);
                }

                GUILayout.EndHorizontal();

                // print out any and all tags received for given GO
                // print texts in the column and any album artwork next to it
                Dictionary<string, object> _tags;
                if (this.tags.TryGetValue(audioStream.gameObject.name, out _tags))
                {
                    GUILayout.BeginHorizontal();

                    GUILayout.BeginVertical(GUILayout.MaxWidth(Screen.width / 2));
                    foreach (var d in _tags)
                        if (d.Value is string)
                            GUILayout.Label(d.Key + ": " + d.Value, AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
                    GUILayout.EndVertical();

                    foreach (var d in _tags)
                        if (d.Value is Texture2D)
                            GUILayout.Label(d.Value as Texture2D, AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.Width(Screen.width / 10), GUILayout.Height(Screen.height / 10));

                    GUILayout.EndHorizontal();
                }
            }

            using (new GUILayout.VerticalScope(GUILayout.MaxWidth(Screen.width / 2)))
            {
                GUILayout.Label(this.audioStreamInput2D.GetType() + "   ========================================", AudioStreamSupport.UX.guiStyleLabelSmall);

                GUILayout.Label("Available recording devices:", AudioStreamSupport.UX.guiStyleLabelNormal);

                if (!DevicesConfiguration.Instance.ASIO)
                {
                    var _includeLoopbacks = GUILayout.Toggle(this.includeLoopbacks, "Include loopback interfaces\r\nPlease note: recording from and playing a loopback device will immediately cause feedback loop - be sure to mute the AudioSource similarly like in this scene.");
                    if (_includeLoopbacks != this.includeLoopbacks)
                    {
                        this.includeLoopbacks = _includeLoopbacks;
                        this.availableInputs = FMOD_SystemW.AvailableInputs(this.audioStreamInput2D.logLevel, this.audioStreamInput2D.gameObject.name, this.audioStreamInput2D.OnError, this.includeLoopbacks);
                        // small reselect if out of range..
                        this.selectedInput = 0;
                    }
                }

                // selection of available audio inputs at runtime
                // list can be long w/ special devices with many ports so wrap it in scroll view
                this.scrollPosition1 = GUILayout.BeginScrollView(this.scrollPosition1, new GUIStyle());

                this.selectedInput = GUILayout.SelectionGrid(this.selectedInput, this.availableInputs.Select((input, index) => string.Format("{0}[Input #{1} |ID {2}|] {3} rate: {4} speaker mode: {5} channels: {6}", input.isDefault ? "(*) " : "", index, input.id, input.name, input.samplerate, input.speakermode, input.channels)).ToArray()
                    , 1
                    , AudioStreamSupport.UX.guiStyleButtonNormal
                    , GUILayout.MaxWidth(Screen.width / 2)
                    );

                if (this.selectedInput != this.previousSelectedInput)
                {
                    if (Application.isPlaying)
                    {
                        this.audioStreamInput2D.Stop();
                        this.audioStreamInput2D.recordDeviceId = this.selectedInput;
                    }

                    this.previousSelectedInput = this.selectedInput;
                }

                GUILayout.EndScrollView();

                // wait for startup
                if (this.availableInputs.Count > 0)
                {
                    FMOD.RESULT lastError;
                    string lastErrorString = this.audioStreamInput2D.GetLastError(out lastError);

                    GUILayout.Label(string.Format("State = {0} {1}"
                        , this.audioStreamInput2D.isRecording ? "Recording" + (this.audioStreamInput2D.isPaused ? " / Paused" : "") : "Stopped"
                        , lastError + " " + lastErrorString
                        )
                        , AudioStreamSupport.UX.guiStyleLabelNormal);
                }

                GUILayout.BeginHorizontal();

                GUILayout.Label("Gain: ", AudioStreamSupport.UX.guiStyleLabelNormal);

                this.audioStreamInput2D.recordGain = GUILayout.HorizontalSlider(this.audioStreamInput2D.recordGain, 0f, 5f);
                GUILayout.Label(Mathf.Round(this.audioStreamInput2D.recordGain * 100f) + " %", AudioStreamSupport.UX.guiStyleLabelNormal);

                GUILayout.EndHorizontal();


                GUILayout.BeginHorizontal();

                if (GUILayout.Button(this.audioStreamInput2D.isRecording ? "Stop" : "Record", AudioStreamSupport.UX.guiStyleButtonNormal))
                    if (this.audioStreamInput2D.isRecording)
                        this.audioStreamInput2D.Stop();
                    else if (this.audioStreamInput2D.ready)
                        this.audioStreamInput2D.Record();

                if (this.audioStreamInput2D.isRecording)
                {
                    if (GUILayout.Button(this.audioStreamInput2D.isPaused ? "Resume" : "Pause", AudioStreamSupport.UX.guiStyleButtonNormal))
                        if (this.audioStreamInput2D.isPaused)
                            this.audioStreamInput2D.Pause(false);
                        else
                            this.audioStreamInput2D.Pause(true);
                }

                GUILayout.EndHorizontal();
            }
        }

        this.scrollPosition2 = GUILayout.BeginScrollView(this.scrollPosition2, new GUIStyle());

        GUILayout.Label(this.icecastSource.GetType() + "   ========================================", AudioStreamSupport.UX.guiStyleLabelSmall);

        GUILayout.Label("Press Connect to connect and start pushing to an Icecast mount point after entering all parameters.", AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Host: ", AudioStreamSupport.UX.guiStyleLabelNormal);
        this.icecastSource.hostname = GUILayout.TextField(this.icecastSource.hostname, GUILayout.MaxWidth(Screen.width / 2));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Port: ", AudioStreamSupport.UX.guiStyleLabelNormal);
        ushort.TryParse(GUILayout.TextField(this.icecastSource.port.ToString(), GUILayout.MaxWidth(Screen.width / 2)), out this.icecastSource.port);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Mountpoint: ", AudioStreamSupport.UX.guiStyleLabelNormal);
        this.icecastSource.mountPoint = GUILayout.TextField(this.icecastSource.mountPoint, GUILayout.MaxWidth(Screen.width / 2));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Password: ", AudioStreamSupport.UX.guiStyleLabelNormal);
        this.icecastSource.password = GUILayout.TextField(this.icecastSource.password, GUILayout.MaxWidth(Screen.width / 2));
        GUILayout.EndHorizontal();

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label("Codec: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
            this.icecastSource.codec = (IcecastSource.IcecastSourceCodec)GUILayout.SelectionGrid((int)this.icecastSource.codec, System.Enum.GetNames(typeof(IcecastSource.IcecastSourceCodec)), 3, AudioStreamSupport.UX.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 4 * 3));
        }


        var connected = this.icecastSource.Connected;

        GUILayout.Label("Host: " + this.icecastSource.hostname, AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("Port: " + this.icecastSource.port, AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label(string.Format("State = {0}"
            , connected ? "Connected" : "Disconnected"
            )
            , AudioStreamSupport.UX.guiStyleLabelNormal);

        this.icecastSource.listen = GUILayout.Toggle(this.icecastSource.listen, "Listen to source output here");

        if (GUILayout.Button(connected ? "Disconnect" : "Connect", AudioStreamSupport.UX.guiStyleButtonNormal))
        {
            if (connected)
                this.icecastSource.Disconnect();
            else
                this.icecastSource.Connect();
        }

        GUILayout.Space(40);

        GUILayout.EndScrollView();
    }
}
