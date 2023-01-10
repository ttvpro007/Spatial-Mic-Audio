// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using AudioStreamSupport;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// [ExecuteInEditMode]
public class AudioStreamRuntimeImportStressTest : MonoBehaviour
{
    /// <summary>
    /// List of components created at the start from code
    /// </summary>
    List<AudioStreamRuntimeImport> audioStreamImports = new List<AudioStreamRuntimeImport>();

    #region UI events
    Dictionary<string, string> streamsStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, Dictionary<string, string>> tags = new Dictionary<string, Dictionary<string, string>>();

    public void OnPlaybackStarted(string goName)
    {
        // playback started means also decoding has been started
        this.streamsStatesFromEvents[goName] = "downloading";
    }

    public void OnPlaybackPaused(string goName, bool paused)
    {
        this.streamsStatesFromEvents[goName] = paused ? "paused" : "downloading";
    }
    /// <summary>
    /// Invoked when decoding has finished and clip is created
    /// </summary>
    /// <param name="goName"></param>
    public void OnPlaybackStopped(string goName, string _)
    {
        this.streamsStatesFromEvents[goName] = "downloaded & clip created";
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
        // link to scene
        // TODO: change delegates to pass UnityObject instead of just name
        var go = FindObjectsOfType<AudioStreamRuntimeImport>().FirstOrDefault(f => f.name == goName);
        if (go)
        {
            var userAudioSource = go.GetComponent<AudioSource>();

            Destroy(userAudioSource.clip);
            userAudioSource.clip = newAudioClip;

            if (playClipAfterDecoding)
                userAudioSource.Play();
        }
    }
    #endregion

    IEnumerator Start()
    {
        var testObjectsCount = 5;

        string filepath = "";
        yield return AudioStreamDemoSupport.GetFilenameFromStreamingAssets("electronic-senses-shibuya.mp3", (newDestination) => filepath = newDestination);

        for (var i = 0; i < testObjectsCount; ++i)
        {
            var go = new GameObject("AudioStreamRuntimeImport#" + i);

            var asd = go.AddComponent<AudioStreamRuntimeImport>();
            // asd.logLevel = AudioStream.LogLevel.INFO;

            while (!asd.ready)
                yield return null;

            asd.url = filepath;
            asd.uniqueCacheId = System.IO.Path.GetRandomFileName();
            asd.overwriteCachedDownload = true;
            asd.continuosStreaming = false;

            asd.OnPlaybackStarted = new AudioStreamSupport.EventWithStringParameter();
            asd.OnPlaybackStarted.AddListener(this.OnPlaybackStarted);

            asd.OnPlaybackPaused = new AudioStreamSupport.EventWithStringBoolParameter();
            asd.OnPlaybackPaused.AddListener(this.OnPlaybackPaused);

            asd.OnPlaybackStopped = new AudioStreamSupport.EventWithStringStringParameter();
            asd.OnPlaybackStopped.AddListener(this.OnPlaybackStopped);

            asd.OnTagChanged = new AudioStreamSupport.EventWithStringStringObjectParameter();
            asd.OnTagChanged.AddListener(this.OnTagChanged);

            asd.OnError = new AudioStreamSupport.EventWithStringStringParameter();
            asd.OnError.AddListener(this.OnError);

            asd.OnAudioClipCreated = new AudioStreamSupport.EventWithStringAudioClipParameter();
            asd.OnAudioClipCreated.AddListener(this.OnAudioClipCreated);

            var @as = go.AddComponent<AudioSource>();
            @as.volume = 0.05f;


            this.audioStreamImports.Add(asd);
        }

        this.allReady = true;
    }

    void OnDestroy()
    {
    }

    bool playClipAfterDecoding = true;
    bool overwriteCache = true;
    bool allReady = false;
    Vector2 scrollPosition = Vector2.zero;
    void OnGUI()
    {
        AudioStreamDemoSupport.OnGUI_GUIHeader(this.audioStreamImports.Count < 1 ? "" : this.audioStreamImports[0].fmodVersion);

        // AudioStreamImportStressTestDemo

        GUILayout.Label("Stress testing scene for the AudioStreamRuntimeImport component", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label(string.Format("{0} GameObjects with AudioStreamRuntimeImport and AudioSource components attached are created in code, all pointed to the same stream (local file is used in this demo, normally this would be network resource with high enough download speed)\r\nOnce they're done/stopped each new clip is assigned to their respective user AudioSource and played.\r\nPlease press the Start button to start them all at once." +
            "\r\n[ temporary PCM data is stored in .RAW file in {1} ]"
            , this.audioStreamImports.Count
            , RuntimeSettings.temporaryDirectoryPath)
            , AudioStreamSupport.UX.guiStyleLabelNormal
            );

        this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUIStyle());

        if (this.allReady)
        {
            var atLeastOnePlaying = false;
            foreach (var asd in this.audioStreamImports)
                if (asd.isPlaying)
                {
                    atLeastOnePlaying = true;
                    break;
                }

            if (GUILayout.Button(atLeastOnePlaying ? "Stop all downloads" : "Start all downloads", AudioStreamSupport.UX.guiStyleButtonNormal))
            {
                foreach (var asd in this.audioStreamImports)
                    if (atLeastOnePlaying)
                        asd.Stop();
                    else if (!asd.isPlaying)
                        asd.Play();
            }
        }

        this.playClipAfterDecoding = GUILayout.Toggle(this.playClipAfterDecoding, "Play downloaded clips immediately after the download is stopped or the whole file is processed");
        this.overwriteCache = GUILayout.Toggle(this.overwriteCache, "Overwrite cache with new download (otherwise no new data will be downloaded and existing cached data will be used for clip");
        foreach (var asd in this.audioStreamImports)
            asd.overwriteCachedDownload = this.overwriteCache;


        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamSupport.UX.guiStyleLabelNormal);

        GUI.color = Color.white;

        for (var i = 0; i < this.audioStreamImports.Count; ++i)
        {
            var asd = this.audioStreamImports[i];

            var userAudioSource = asd.GetComponent<AudioSource>();

            using (new GUILayout.HorizontalScope())
            {
                FMOD.RESULT lastError;
                string lastErrorString = asd.GetLastError(out lastError);

                GUILayout.Label(string.Format("State = {0} {1}"
                    , asd.isPlaying ? "Playing" + (asd.isPaused ? " / Paused" : "") : "Stopped"
                    , lastError + " " + lastErrorString
                    )
                    , AudioStreamSupport.UX.guiStyleLabelNormal);
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(asd.isPlaying ? "Stop Download" : "Download" + (this.playClipAfterDecoding ? " and Play" : ""), AudioStreamSupport.UX.guiStyleButtonNormal))
                    if (asd.isPlaying)
                        asd.Stop();
                    else
                    {
                        asd.Play();
                    }

                if (userAudioSource.clip != null)
                {
                    // clip decoded
                    if (GUILayout.Button(userAudioSource.isPlaying ? "Stop Playback" : "Play", AudioStreamSupport.UX.guiStyleButtonNormal))
                    {
                        if (userAudioSource.isPlaying)
                            userAudioSource.Stop();
                        else
                            userAudioSource.Play();
                    }
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                if (userAudioSource.clip != null)
                {
                    GUILayout.Label(string.Format("Last decoding to AudioClip took: {0} ms\r\nDecoded AudioClip channels: {1}, length: {2} s, playback position: {3:F2} s"
                        , asd.decodingToAudioClipTimeInMs
                        , userAudioSource.clip.channels
                        , userAudioSource.clip.length
                        , userAudioSource.time
                        ), AudioStreamSupport.UX.guiStyleLabelNormal);

                    GUILayout.Label("Volume: ", AudioStreamSupport.UX.guiStyleLabelNormal);

                    userAudioSource.volume = GUILayout.HorizontalSlider(userAudioSource.volume, 0f, 1f);
                    GUILayout.Label(Mathf.Round(userAudioSource.volume * 100f) + " %", AudioStreamSupport.UX.guiStyleLabelNormal);
                }
            }
        }

        GUILayout.Space(40);

        GUILayout.EndScrollView();
    }
}