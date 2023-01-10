// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioStreamMinimalStressTest : MonoBehaviour
{
    /// <summary>
    /// List of components created at the start from code
    /// </summary>
    readonly List<AudioStream.AudioStreamMinimal> audioStreams = new List<AudioStream.AudioStreamMinimal>();

    #region UI events

    readonly Dictionary<string, string> streamsStatesFromEvents = new Dictionary<string, string>();
    readonly Dictionary<string, Dictionary<string, string>> tags = new Dictionary<string, Dictionary<string, string>>();

    public void OnPlaybackStarted(string goName)
    {
        this.streamsStatesFromEvents[goName] = "playing";
    }

    public void OnPlaybackPaused(string goName, bool paused)
    {
        this.streamsStatesFromEvents[goName] = paused ? "paused" : "playing";
    }

    public void OnPlaybackStopped(string goName, string _)
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

    public void OnError(string goName, string msg)
    {
        this.streamsStatesFromEvents[goName] = msg;
    }
    #endregion

    IEnumerator Start()
    {
        var testObjectsCount = 5; // max. individual systems are limited - 1.10.15 could create up to 8, which counts to overall system, so limit this for everything else to be still useable 

        for (var i = 0; i < testObjectsCount; ++i)
        {
            var go = new GameObject("AudioStreamMinimal#" + i);

            var @as = go.AddComponent<AudioStream.AudioStreamMinimal>();
            // @as.logLevel = AudioStream.LogLevel.INFO;

            while (!@as.ready)
                yield return null;

            @as.url = "http://somafm.com/spacestation.pls";

            @as.OnPlaybackStarted = new AudioStreamSupport.EventWithStringParameter();
            @as.OnPlaybackStarted.AddListener(this.OnPlaybackStarted);

            @as.OnPlaybackPaused = new AudioStreamSupport.EventWithStringBoolParameter();
            @as.OnPlaybackPaused.AddListener(this.OnPlaybackPaused);

            @as.OnPlaybackStopped = new AudioStreamSupport.EventWithStringStringParameter();
            @as.OnPlaybackStopped.AddListener(this.OnPlaybackStopped);

            @as.OnTagChanged = new AudioStreamSupport.EventWithStringStringObjectParameter();
            @as.OnTagChanged.AddListener(this.OnTagChanged);

            @as.OnError = new AudioStreamSupport.EventWithStringStringParameter();
            @as.OnError.AddListener(this.OnError);

            @as.volume = 0.1f;

            this.audioStreams.Add(@as);
        }

        this.allReady = true;
    }

    bool allReady = false;
    Vector2 scrollPosition = Vector2.zero;
    void OnGUI()
    {
        AudioStreamDemoSupport.OnGUI_GUIHeader(this.audioStreams != null && this.audioStreams.Count > 0 ? " " + this.audioStreams[0].fmodVersion : "");

        GUILayout.Label("Stress testing scene for the AudioStreamMinimal component", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label(string.Format("{0} AudioStreamMinimal game objects are created at Start and are set to start streaming simultaneously the same url", this.audioStreams.Count), AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("[Depending on the network connection several might not be able to actually connect]", AudioStreamSupport.UX.guiStyleLabelNormal);

        this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUIStyle());

        if (this.allReady)
        {
            var atLeastOnePlaying = false;
            foreach (var @as in this.audioStreams)
                if (@as.isPlaying)
                {
                    atLeastOnePlaying = true;
                    break;
                }

            if (GUILayout.Button(atLeastOnePlaying ? "Stop all" : "Start all", AudioStreamSupport.UX.guiStyleButtonNormal))
            {
                foreach (var @as in this.audioStreams)
                    if (atLeastOnePlaying)
                        @as.Stop();
                    else if (!@as.isPlaying)
                        @as.Play();
            }
        }

        GUI.color = Color.yellow;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var p in this.streamsStatesFromEvents)
            sb.Append(" | " + p.Key + " : " + p.Value);
        GUILayout.Label(sb.ToString(), AudioStreamSupport.UX.guiStyleLabelNormal);

        GUI.color = Color.white;

        foreach (var audioStream in this.audioStreams)
        {
            FMOD.RESULT lastError;
            string lastErrorString = audioStream.GetLastError(out lastError);

            GUILayout.BeginHorizontal();
            GUILayout.Label(audioStream.gameObject.name, AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
            // GUILayout.Label(audioStream.url, AudioStreamSupport.UX.guiStyleLabelNormal);
            GUILayout.Label(string.Format("State = {0} {1}"
                , audioStream.isPlaying ? "Playing" + (audioStream.isPaused ? " / Paused" : "") : "Stopped"
                , lastError + " " + lastErrorString
                )
                , AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

            if (GUILayout.Button(audioStream.isPlaying ? "Stop" : "Play", AudioStreamSupport.UX.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 2)))
                if (audioStream.isPlaying)
                    audioStream.Stop();
                else
                {
                    if (audioStream.ready)
                        audioStream.Play();
                }

            if (audioStream.isPlaying)
            {
                if (GUILayout.Button(audioStream.isPaused ? "Resume" : "Pause", AudioStreamSupport.UX.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 2)))
                    if (audioStream.isPaused)
                        audioStream.Pause(false);
                    else
                        audioStream.Pause(true);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(40);

        GUILayout.EndScrollView();
    }
}