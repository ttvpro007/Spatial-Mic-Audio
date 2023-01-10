// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

// [ExecuteInEditMode]
public class AudioStreamMemoryStressTest : MonoBehaviour
{
    /// <summary>
    /// List of components created at the start from code
    /// </summary>
    List<AudioStreamMemory> audioStreamMemories = new List<AudioStreamMemory>();

    #region UI events
    Dictionary<string, string> streamsStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, Dictionary<string, string>> tags = new Dictionary<string, Dictionary<string, string>>();

    public void OnPlaybackStarted(string goName)
    {
        // playback started means also decoding has been started
        this.streamsStatesFromEvents[goName] = "decoding";
    }

    public void OnPlaybackPaused(string goName, bool paused)
    {
        this.streamsStatesFromEvents[goName] = paused ? "paused" : "playing";
    }
    /// <summary>
    /// Invoked when decoding has finished and clip is created
    /// </summary>
    /// <param name="goName"></param>
    public void OnPlaybackStopped(string goName, string _)
    {
        this.streamsStatesFromEvents[goName] = "decoded & clip created";
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
                this.tags[goName] = new Dictionary<string, string>() { { _key, _value as string} };
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
        var go = FindObjectsOfType<AudioStreamMemory>().FirstOrDefault(f => f.name == goName);
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

    byte[] bytes = null;
    GCHandle memoryHandle;
    IEnumerator Start()
    {
        var testObjectsCount = 5;

        // load compressed audio to memory
        // (Android has special needs - copy the file out of archive from StreamingAsset to some accessible location)

        string filepath = "";
        yield return AudioStreamDemoSupport.GetFilenameFromStreamingAssets("electronic-senses-shibuya.mp3", (newDestination) => filepath = newDestination);

        this.bytes = File.ReadAllBytes(filepath);

        this.memoryHandle = GCHandle.Alloc(this.bytes, GCHandleType.Pinned);

        for (var i = 0; i < testObjectsCount; ++i)
        {
            var go = new GameObject("AudioStreamMemory#" + i);

            var asm = go.AddComponent<AudioStreamMemory>();
            // asm.logLevel = AudioStream.LogLevel.INFO;

            while (!asm.ready)
                yield return null;

            asm.memoryLocation = this.memoryHandle.AddrOfPinnedObject();
            asm.memoryLength = (uint)this.bytes.Length;
            asm.cacheIdentifier = Path.GetRandomFileName();

            asm.OnPlaybackStarted = new AudioStreamSupport.EventWithStringParameter();
            asm.OnPlaybackStarted.AddListener(this.OnPlaybackStarted);

            asm.OnPlaybackPaused = new AudioStreamSupport.EventWithStringBoolParameter();
            asm.OnPlaybackPaused.AddListener(this.OnPlaybackPaused);

            asm.OnPlaybackStopped = new AudioStreamSupport.EventWithStringStringParameter();
            asm.OnPlaybackStopped.AddListener(this.OnPlaybackStopped);

            asm.OnTagChanged = new AudioStreamSupport.EventWithStringStringObjectParameter();
            asm.OnTagChanged.AddListener(this.OnTagChanged);

            asm.OnError = new AudioStreamSupport.EventWithStringStringParameter();
            asm.OnError.AddListener(this.OnError);

            asm.OnAudioClipCreated = new AudioStreamSupport.EventWithStringAudioClipParameter();
            asm.OnAudioClipCreated.AddListener(this.OnAudioClipCreated);

            var @as = go.AddComponent<AudioSource>();
            @as.volume = 0.05f;


            this.audioStreamMemories.Add(asm);
        }

        this.allReady = true;
    }

    void OnDestroy()
    {
        // cleanup
        if (this.memoryHandle.IsAllocated)
            this.memoryHandle.Free();
    }
    /// <summary>
    /// Plays all components potentially from cache - there's a frame yield in order not to stall clips creation
    /// </summary>
    /// <returns></returns>
    IEnumerator PlayAllClips()
    {
        foreach (var asm in this.audioStreamMemories)
            if (!asm.isPlaying)
            {
                asm.Play();
                // yield a frame in order to not overload application
                yield return null;
            }
    }

    IEnumerator StopAllClips()
    {
        foreach (var asm in this.audioStreamMemories)
        {
            asm.Stop();
            // yield a frame in order to not overload application
            yield return null;
        }
    }

    bool playClipAfterDecoding = true;
    bool allReady = false;
    Vector2 scrollPosition = Vector2.zero;
    void OnGUI()
    {
        AudioStreamDemoSupport.OnGUI_GUIHeader(this.audioStreamMemories.Count < 1 ? "" : this.audioStreamMemories[0].fmodVersion);

        GUILayout.Label("Stress testing scene for the AudioStreamMemory component", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label(string.Format("{0} GameObjects with AudioStreamMemory and AudioSource components attached are created in code, all pointed to the same memory location with mp3 file.\r\nOnce they're done decoding each new clip is assigned to their respective user AudioSource.\r\nEach is assigned a unique cache id when scene starts, so decoded results are cached and subsequent runs in this session (until leaving the scene) will retrieve cached audio immediately.\r\nPlease press the Start button to start them all at once.", this.audioStreamMemories.Count), AudioStreamSupport.UX.guiStyleLabelNormal);

        this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUIStyle());

        if (this.allReady)
        {
            var atLeastOnePlaying = false;
            foreach (var asm in this.audioStreamMemories)
                if (asm.isPlaying)
                {
                    atLeastOnePlaying = true;
                    break;
                }

            if (GUILayout.Button(atLeastOnePlaying ? "Stop all decoders" : "Start all decoders", AudioStreamSupport.UX.guiStyleButtonNormal))
            {
                if (atLeastOnePlaying)
                    this.StartCoroutine(this.StopAllClips());
                else
                    this.StartCoroutine(this.PlayAllClips());
            }
        }

        this.playClipAfterDecoding = GUILayout.Toggle(this.playClipAfterDecoding, "Play decoded clip immediately after the decoding is stopped or the whole file is processed");

        GUI.color = Color.yellow;

        foreach (var p in this.streamsStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamSupport.UX.guiStyleLabelNormal);

        GUI.color = Color.white;

        for (var i = 0; i < this.audioStreamMemories.Count; ++i)
        {
            var asm = this.audioStreamMemories[i];

            var userAudioSource = asm.GetComponent<AudioSource>();

            using (new GUILayout.HorizontalScope())
            {
                FMOD.RESULT lastError;
                string lastErrorString = asm.GetLastError(out lastError);

                GUILayout.Label(string.Format("State = {0} {1}"
                    , asm.isPlaying ? "Playing" + (asm.isPaused ? " / Paused" : "") : "Stopped"
                    , lastError + " " + lastErrorString
                    )
                    , AudioStreamSupport.UX.guiStyleLabelNormal);

                GUILayout.Label(string.Format("Decoded bytes: {0} (encoded memory size: {1})", asm.decoded_bytes, asm.memoryLength + " b"), AudioStreamSupport.UX.guiStyleLabelNormal);
            }

            using (new GUILayout.HorizontalScope())
            {
                if (this.bytes == null)
                {
                    GUILayout.Label("Waiting for file to load into memory....", AudioStreamSupport.UX.guiStyleLabelNormal);
                }
                else
                {
                    if (GUILayout.Button(asm.isPlaying ? "Stop Decoding" : "Decode" + (this.playClipAfterDecoding ? " and Play" : ""), AudioStreamSupport.UX.guiStyleButtonNormal))
                        if (asm.isPlaying)
                            asm.Stop();
                        else
                        {
                            asm.Play();
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
            }

            using (new GUILayout.HorizontalScope())
            {
                if (userAudioSource.clip != null)
                {
                    GUILayout.Label(string.Format("Last decoding to AudioClip took: {0} ms\r\nDecoded AudioClip channels: {1}, length: {2} s, playback position: {3:F2} s"
                        , asm.decodingToAudioClipTimeInMs
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

        GUILayout.Space(10);

        GUILayout.Label("Audio track used in this demo:", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("Shibuya by Electronic Senses | https://soundcloud.com/electronicsenses", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("Music promoted by https://www.free-stock-music.com", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("Creative Commons Attribution - ShareAlike 3.0 Unported", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("https://creativecommons.org/licenses/by-sa/3.0/deed.en_US", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("audio file is read from StreamingAssets", AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.Space(40);

        GUILayout.EndScrollView();
    }
}