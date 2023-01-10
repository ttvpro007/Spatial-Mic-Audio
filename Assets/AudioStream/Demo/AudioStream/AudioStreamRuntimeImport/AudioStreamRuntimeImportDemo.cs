// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using AudioStreamSupport;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode()]
public class AudioStreamRuntimeImportDemo : MonoBehaviour
{
    public AudioStreamRuntimeImport asImport;
    public AudioSource userAudioSource;
    [Header("Demo textures for display")]
    public AudioTexture_OutputData audioTexture_OutputData;
    public AudioTexture_SpectrumData audioTexture_SpectrumData;

    #region UI events

    Dictionary<string, string> streamsStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, Dictionary<string, string>> tags = new Dictionary<string, Dictionary<string, string>>();

    public void OnPlaybackStarted(string goName)
    {
        // playback started means also download has been started
        this.streamsStatesFromEvents[goName] = "downloading";
    }

    public void OnPlaybackPaused(string goName, bool paused)
    {
        this.streamsStatesFromEvents[goName] = paused ? "paused" : "playing";
    }
    /// <summary>
    /// Invoked when download has finished and clip is created
    /// </summary>
    /// <param name="goName"></param>
    public void OnPlaybackStopped(string goName)
    {
        this.streamsStatesFromEvents[goName] = "downloaded & clip created";
        this.@as_isPaused = false;
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

    public void OnError(string goName, string msg)
    {
        this.streamsStatesFromEvents[goName] = msg;
    }

    public void OnAudioClipCreated(string goName, AudioClip newAudioClip)
    {
        Destroy(this.userAudioSource.clip);
        this.userAudioSource.clip = newAudioClip;

        if (this.playClipAfterDownload)
            this.userAudioSource.Play();
    }
    #endregion

    IEnumerator Start()
    {
        string filepath = "";
        yield return AudioStreamDemoSupport.GetFilenameFromStreamingAssets("electronic-senses-shibuya.mp3", (newDestination) => filepath = newDestination);

        this.asImport.url = filepath;
    }

    bool @as_isPaused = false;
    bool playClipAfterDownload = true;
    Vector2 scrollPosition = Vector2.zero;
    void OnGUI()
    {
        AudioStreamDemoSupport.OnGUI_GUIHeader(this.asImport != null ? " " + this.asImport.fmodVersion : "");
        AudioStreamSupport.UX.OnGUI_AudioTextures(this.audioTexture_OutputData, this.audioTexture_SpectrumData);

        this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUIStyle());

        // AudioStreamImportDemo

        GUILayout.Label("This component is primarily aimed at downloading files via *non realtime* speeds which are (much) higher than real time playback - this means that real time streams such as net radio streams most likely won't work", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("^ (for normal downloading see AudioStreamDemo scene which decodes (only) in realtime thus slower than this and allows audio to be played while it's downloading", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("NOTE: URL of a file placed in application's StreamingAssets is used in this demo (which kind of defeats the purpose since local file is read directly normally) - enter URL of a file on LAN or on a quick enough network to test properly", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("Press Download to start downloading entered URL\r\nURL can be pls/m3u/8 playlist, file URL, or local filesystem path (with or without the 'file://' prefix)", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("AudioStreamRuntimeImport will download/stream, decode audio and save it into cache associated w/ url instead of playing it; once done/stopped it will then construct an AudioClip and pass it to user AudioSource for playback", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("The downloaded AudioClip data is temporary, but is not deleted after the AudioClip creation (note these files can get quite large); while they're in temporary cache they can be played later offline if/when needed", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("Temporary PCM data used for AudioClip stored as .RAW file in " + RuntimeSettings.temporaryDirectoryPath, AudioStreamSupport.UX.guiStyleLabelNormal);

        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamSupport.UX.guiStyleLabelNormal);

        GUI.color = Color.white;

        FMOD.RESULT lastError;
        string lastErrorString = this.asImport.GetLastError(out lastError);

        GUILayout.Label(this.asImport.GetType() + "   ========================================", AudioStreamSupport.UX.guiStyleLabelSmall);

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label("Stream: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
            this.asImport.url = GUILayout.TextField(this.asImport.url, GUILayout.MaxWidth(Screen.width / 2));
        }

        // GUILayout.BeginHorizontal();
        this.asImport.overwriteCachedDownload = GUILayout.Toggle(this.asImport.overwriteCachedDownload, "Overwrite previously downloaded data for this url");
        this.playClipAfterDownload = GUILayout.Toggle(this.playClipAfterDownload, "Play downloaded clip immediately after the download is stopped or the whole file is downloaded");
        // GUILayout.EndHorizontal();

        GUILayout.Label(string.Format("State = {0} {1}"
            , this.asImport.isPlaying ? "Playing" + (this.asImport.isPaused ? " / Paused" : "") : "Stopped"
            , lastError + " " + lastErrorString
            )
            , AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.BeginHorizontal();

        GUILayout.Label("Volume: ", AudioStreamSupport.UX.guiStyleLabelNormal);

        this.userAudioSource.volume = GUILayout.HorizontalSlider(this.userAudioSource.volume, 0f, 1f);
        GUILayout.Label(Mathf.Round(this.userAudioSource.volume * 100f) + " %", AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label(string.Format("Decoded downloaded bytes: {0} (file size: {1})", this.asImport.decoded_bytes, this.asImport.file_size.HasValue ? this.asImport.file_size.Value + " b" : "N/A (streamed content)"));
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label(string.Format("Last AudioClip creation took: {0} ms", this.asImport.decodingToAudioClipTimeInMs));

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label("Stream format: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
            this.asImport.streamType = (AudioStreamBase.StreamAudioType)ComboBoxLayout.BeginLayout(0, System.Enum.GetNames(typeof(AudioStreamBase.StreamAudioType)), (int)this.asImport.streamType, 10, AudioStreamSupport.UX.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 2));
        }

        GUILayout.BeginHorizontal();

        var dl = this.asImport.overwriteCachedDownload ? "Download" : "Retrieve from cache";
        if (GUILayout.Button(this.asImport.isPlaying ? "Stop Download" : dl + (this.playClipAfterDownload ? " and Play" : ""), AudioStreamSupport.UX.guiStyleButtonNormal))
            if (this.asImport.isPlaying)
                this.asImport.Stop();
            else
            {
                this.asImport.Play();
            }

        if (this.asImport.isPlaying)
        {
            if (GUILayout.Button(this.asImport.isPaused ? "Resume Download" : "Pause Download", AudioStreamSupport.UX.guiStyleButtonNormal))
                if (this.asImport.isPaused)
                    this.asImport.Pause(false);
                else
                    this.asImport.Pause(true);
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.Label(string.Format("Cached file samplerate: {0}", this.asImport.streamSampleRate), AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label(string.Format("Cached file channels  : {0}", this.asImport.streamChannels), AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.Space(10);

        if (this.userAudioSource.clip != null)
        {
            // clip downloaded

            GUILayout.Label(string.Format("Downloaded AudioClip channels: {0}, length: {1} s, playback position: {2:F2} s", this.userAudioSource.clip.channels, this.userAudioSource.clip.length, this.userAudioSource.time), AudioStreamSupport.UX.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button(this.userAudioSource.isPlaying || this.@as_isPaused ? "Stop Playback" : "Play", AudioStreamSupport.UX.guiStyleButtonNormal))
            {
                if (this.userAudioSource.isPlaying || this.@as_isPaused)
                    this.userAudioSource.Stop();
                else if (!this.@as_isPaused)
                    this.userAudioSource.Play();

                this.@as_isPaused = false;
            }

            if (this.userAudioSource.isPlaying || this.@as_isPaused)
            {
                if (GUILayout.Button(this.@as_isPaused ? "Resume Playback" : "Pause Playback", AudioStreamSupport.UX.guiStyleButtonNormal))
                    if (this.@as_isPaused)
                    {
                        this.userAudioSource.UnPause();
                        this.@as_isPaused = false;
                    }
                    else
                    {
                        this.userAudioSource.Pause();
                        this.@as_isPaused = true;
                    }
            }

            GUILayout.EndHorizontal();
        }

        Dictionary<string, string> _tags;
        if (this.tags.TryGetValue(this.asImport.name, out _tags))
            foreach (var d in _tags)
                GUILayout.Label(d.Key + ": " + d.Value, AudioStreamSupport.UX.guiStyleLabelNormal);

        ComboBoxLayout.EndAllLayouts();

        GUILayout.Space(40);

        GUILayout.EndScrollView();
    }
}