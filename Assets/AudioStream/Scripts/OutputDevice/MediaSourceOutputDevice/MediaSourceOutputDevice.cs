// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Output device + specific channel selection via mix matrix for media played directly by FMOD only
    /// There is no Editor/Inspector functionality for user sounds (except Unity events) - only API is exposed via this component currently -
    /// Please see how it's used in MediaSourceOutputDeviceDemo scene
    /// </summary>
    public class MediaSourceOutputDevice : MonoBehaviour
	{
        // ========================================================================================================================================
        #region Editor
        [Header("[Setup]")]
        [Tooltip("user requested output driver on which audio will be played by default.\r\nMost of functionality is accessible via API only currently. Please see 'MediaSourceOutputDeviceDemo' for more.")]
        /// <summary>
        /// This' FMOD system current output device
        /// </summary>
        [SerializeField]
        int outputDriverID = 0;
        /// <summary>
        /// public facing property
        /// </summary>
        public int OutputDriverID { get { return this.outputDriverID; } protected set { this.outputDriverID = value; } }
        /// <summary>
        /// Output w/ all properties - stored to automatically find driver id at runtime
        /// </summary>
        public FMOD_SystemW.OUTPUT_DEVICE outputDevice { get; protected set; }
        [Tooltip("If output #outputDriverID can't be used this will indicate on which output is this component actually playing")]
        [SerializeField]
        [ReadOnly]
        int runtimeOutputDriverID = 0;
        /// <summary>
        /// public facing property
        /// </summary>
        public int RuntimeOutputDriverID { get { return this.runtimeOutputDriverID; } }
        /// <summary>
        /// Output w/ all properties - runtime info
        /// </summary>
        public FMOD_SystemW.OUTPUT_DEVICE runtimeOutputDevice { get; protected set; }
        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;

        #region Unity events
        [Header("[Events]")]
        public EventWithStringParameter OnPlaybackStarted;
        public EventWithStringParameter OnPlaybackStopped;
        public EventWithStringParameter OnPlaybackPaused;
        public EventWithStringStringParameter OnError;
        #endregion

        [Header("[Output device latency (ms) (info only)]")]
        [Tooltip("TODO: PD readonly Computed for current output device at runtime")]
        public float latencyBlock;
        [Tooltip("TODO: PD readonly Computed for current output device at runtime")]
        public float latencyTotal;
        [Tooltip("TODO: PD readonly Computed for current output device at runtime")]
        public float latencyAverage;
        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        string gameObjectName = string.Empty;
        #endregion
        // ========================================================================================================================================
        #region FMOD start/release/play sound
        /// <summary>
        /// Component can play multiple user's sounds via API - manage all of them separately + their volumes
        /// </summary>
        List<FMOD.Channel> channels = new List<FMOD.Channel>();
        /// <summary>
        /// Component startup sync
        /// </summary>
        [HideInInspector]
        public bool ready = false;
        [HideInInspector]
        public string fmodVersion;
        /// <summary>
        /// The system which plays the sound on selected output - one per output, released when sound (redirection) is stopped,
        /// </summary>
        FMOD_SystemW.FMOD_System outputdevice_system = null;
        protected FMOD.RESULT result = FMOD.RESULT.OK;
        FMOD.RESULT lastError = FMOD.RESULT.OK;
        /// <summary>
        /// FMOD system for output
        /// </summary>
        /// <param name="rememberOutput">Remembers set output to reacquire it later on devices changes</param>
        void StartFMODSystem(int onOutputDriverID, bool rememberOutput)
        {
            /*
             * before creating system for target output, check if it wouldn't fail with it first :: device list can be changed @ runtime now
             * if it would, fallback to default ( 0 ) which should be hopefully always available - otherwise we would have failed miserably some time before already
             */
            var availableOutputs = FMOD_SystemW.AvailableOutputs(this.logLevel, this.gameObjectName, this.OnError);
            if (!availableOutputs.Select(s => s.id).Contains(this.outputDriverID))
            {
                LOG(LogLevel.WARNING, "Output device {0} is not available, using default output (0) as fallback", this.outputDriverID);
                this.outputDriverID = 0;
                // play on 0 if not present ?
            }

            uint dspBufferLength, dspBufferCount;
            this.outputdevice_system = FMOD_SystemW.FMOD_System_Create(onOutputDriverID, true, this.logLevel, this.gameObjectName, this.OnError, out dspBufferLength, out dspBufferCount);
            this.fmodVersion = this.outputdevice_system.VersionString;

            this.runtimeOutputDriverID = onOutputDriverID;
            this.runtimeOutputDevice = availableOutputs[this.runtimeOutputDriverID];

            // store output info if called by user to find it later if/when needed
            if (this.runtimeOutputDriverID == this.outputDriverID
                || rememberOutput
                )
                if (this.outputDriverID < availableOutputs.Count)
                    this.outputDevice = availableOutputs[this.outputDriverID];

            // compute latency as last step
            // TODO: move latency to system creation

            int samplerate;
            FMOD.SPEAKERMODE sm;
            int speakers;
            result = this.outputdevice_system.system.getSoftwareFormat(out samplerate, out sm, out speakers);
            ERRCHECK(result, "outputdevice_system.System.getSoftwareFormat");

            float ms = (float)dspBufferLength * 1000.0f / (float)samplerate;

            this.latencyBlock = ms;
            this.latencyTotal = ms * dspBufferCount;
            this.latencyAverage = ms * ((float)dspBufferCount - 1.5f);
        }

        /*
         * System is released automatically when the last sound being played via it is stopped (sounds are tracked by 'sounds' member list)
         * There was not a good place to release it otherwise since it has to be released after all sounds are released and:
         * - OnApplicationQuit is called *before* OnDestroy so it couldn't be used (sound can be released when switching scenes)
         * - when released in class destructor (exit/ domain reload) it led to crashes / deadlocks in FMOD - *IF* a sound was created/played on that system before -
         */

        /// <summary>
        /// Stops sound created by this component on shared FMOD system and removes reference to it
        /// If the system is playing 0 sounds afterwards, it is released too
        /// </summary>
        void StopFMODSystem()
        {
            if (this.outputdevice_system != null)
            {
                foreach (var channel in this.channels)
                {
                    this.outputdevice_system.ReleaseUserSound(channel, this.logLevel, this.gameObjectName, this.OnError);
                }

                this.channels.Clear();

                FMOD_SystemW.FMOD_System_Release(ref this.outputdevice_system, this.logLevel, this.gameObjectName, this.OnError);
            }
        }
        #endregion
        // ========================================================================================================================================
        #region Unity lifecycle
        void Start()
        {
            // FMODSystemsManager.InitializeFMODDiagnostics(FMOD.DEBUG_FLAGS.LOG);
            this.gameObjectName = this.gameObject.name;

            this.StartFMODSystem(this.outputDriverID, true);

            this.ready = true;
        }
        void Update()
        {
            // update output system
            if (
                this.outputdevice_system != null
                && this.outputdevice_system.SystemHandle != IntPtr.Zero
                )
            {
                this.outputdevice_system.Update();
            }
        }

        void OnDestroy()
        {
            this.StopFMODSystem();
        }
        #endregion
        // ========================================================================================================================================
        #region Support
        void ERRCHECK(FMOD.RESULT result, string customMessage, bool throwOnError = true)
        {
            this.lastError = result;

            FMODHelpers.ERRCHECK(result, this.logLevel, this.gameObjectName, this.OnError, customMessage, throwOnError);
        }

        void LOG(LogLevel requestedLogLevel, string format, params object[] args)
        {
            Log.LOG(requestedLogLevel, this.logLevel, this.gameObjectName, format, args);
        }

        public string GetLastError(out FMOD.RESULT errorCode)
        {
            if (!this.ready)
                errorCode = FMOD.RESULT.ERR_NOTREADY;
            else
                errorCode = this.lastError;

            return FMOD.Error.String(errorCode);
        }
        #endregion
        // ========================================================================================================================================
        #region Output device
        /// <summary>
        /// Use different output
        /// </summary>
        /// <param name="_outputDriverID"></param>
        /// <param name="rememberOutput">Remember the output to reacquire later on devices change</param>
        public void SetOutput(int _outputDriverID)
        {
            if (!this.ready)
            {
                LOG(LogLevel.ERROR, "Please make sure to wait for 'ready' flag before calling this method");
                return;
            }
            
            this.outputDriverID = _outputDriverID;

            // redirection is always running so restart it with new output
            this.StopFMODSystem();

            this.StartFMODSystem(this.outputDriverID, true);
        }
        /// <summary>
        /// Stop this' sound (and release system) in the first phase of reflecting updated outputs on device/s change
        /// </summary>
        public void ReflectOutput_Start()
        {
            if (!this.ready)
            {
                this.LOG(LogLevel.ERROR, "Please make sure to wait for 'ready' flag before calling this method");
                return;
            }

            // redirection is always running so restart it with new output
            this.StopFMODSystem();
        }
        /// <summary>
        /// Updates this' runtimeOutputDriverID and FMOD sound/system based on new/updated system devices list
        /// Tries to find output in new list if it was played already, or sets output to be user output id, if possible
        /// </summary>
        /// <param name="updatedOutputDevices"></param>
        public void ReflectOutput_Finish(List<FMOD_SystemW.OUTPUT_DEVICE> updatedOutputDevices)
        {
            // sounds end up on default (0) if device is not present
            var outputID = 0;

            // if output wasn't running it was requested on initially non present output
            if (this.outputDevice.guid != Guid.Empty)
            {
                var output = updatedOutputDevices.FirstOrDefault(f => f.guid == this.outputDevice.guid);

                // Guid.Empty indicates OUTPUT_DEVICE default (not found) item
                if (output.guid != Guid.Empty)
                {
                    outputID = updatedOutputDevices.IndexOf(output);
                }
            }
            else
            {
                if (this.outputDriverID < updatedOutputDevices.Count)
                {
                    outputID = this.outputDriverID;
                }
            }

            this.StartFMODSystem(outputID, false);
        }
        #endregion
        // ========================================================================================================================================
        #region User sound/channel management / playback
        /// <summary>
        /// Creates an user sound and optionally plays it immediately; returns created channel so it can be played/unpaused later
        /// </summary>
        /// <param name="audioUri"></param>
        /// <param name="volume"></param>
        /// <param name="loop"></param>
        /// <param name="playImmediately"></param>
        /// <param name="mixMatrix"></param>
        /// <param name="outchannels"></param>
        /// <param name="inchannels"></param>
        /// <param name="channel"></param>
        public void StartUserSound(string audioUri
            , float volume
            , bool loop
            , bool playImmediately
            , float[,] mixMatrix
            , int outchannels
            , int inchannels
            , out FMOD.Channel channel
            )
        {
            result = this.outputdevice_system.CreateUserSound(
                audioUri
                , loop
                , volume
                , playImmediately
                , mixMatrix
                , outchannels
                , inchannels
                , this.logLevel
                , this.gameObjectName
                , this.OnError
                , out channel
                );

            if (result == FMOD.RESULT.OK)
            {
                this.channels.Add(channel);

                if (this.OnPlaybackStarted != null && playImmediately)
                    this.OnPlaybackStarted.Invoke(this.gameObjectName);
            }
            else
            {
                var msg = string.Format("Can't create sound: {0}", FMOD.Error.String(result));

                LOG(LogLevel.ERROR, msg);
                
                if (this.OnError != null)
                    this.OnError.Invoke(this.gameObjectName, msg);
            }
        }
        /// <summary>
        /// Effectively just unpauses created channel
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public FMOD.RESULT PlayUserSound(FMOD.Channel channel
            , float volume
            , bool loop
            , float[,] mixMatrix
            , int outchannels
            , int inchannels
            , out FMOD.Channel newChannel
            )
        {
            result = channel.setPaused(false);
            // ERRCHECK(result, "channel.setPaused", false);

            // channel was released already, most likely due to finished playback - start a new one and return it
            // usually with FMOD.RESULT.ERR_INVALID_HANDLE / FMOD.RESULT.ERR_CHANNEL_STOLEN but we'll try in any case..
            if (result != FMOD.RESULT.OK)
            {
                LOG(LogLevel.WARNING, "Channel finished/stolen, creating new one.. ");

                result = this.outputdevice_system.PlayUserChannel(channel, volume, loop, mixMatrix, outchannels, inchannels, this.logLevel, this.gameObjectName, this.OnError, out newChannel);
                if (result == FMOD.RESULT.OK)
                {
                    this.channels.Remove(channel);
                    this.channels.Add(newChannel);
                }
            }
            else
            {
                // just update parameters
                result = channel.setLoopCount(loop ? -1 : 0);
                ERRCHECK(result, "channel.setLoopCount", false);

                result = channel.setVolume(volume);
                ERRCHECK(result, "channel.setVolume", false);

                // keep the behaviour consistent 
                newChannel = channel;

                result = FMOD.RESULT.OK;
            }

            if (result == FMOD.RESULT.OK)
                if (this.OnPlaybackStarted != null)
                    this.OnPlaybackStarted.Invoke(this.gameObjectName);

            return result;
        }
        /// <summary>
        /// Effectively just pauses the channel, and sets playback position to 0
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public FMOD.RESULT StopUserSound(FMOD.Channel channel)
        {
            result = channel.setPosition(0, FMOD.TIMEUNIT.MS);
            ERRCHECK(result, "channel.setPosition", false);

            if (result != FMOD.RESULT.OK)
                return result;

            result = channel.setPaused(true);
            ERRCHECK(result, "channel.setPaused", false);

            if (result != FMOD.RESULT.OK)
                return result;

            if (this.OnPlaybackStopped != null)
                this.OnPlaybackStopped.Invoke(this.gameObjectName);

            return result;
        }

        public FMOD.RESULT PauseUserSound(FMOD.Channel channel, bool paused)
        {
            result = channel.setPaused(paused);
            ERRCHECK(result, "channel.setPaused", false);

            if (result == FMOD.RESULT.OK)
                if (this.OnPlaybackPaused != null)
                    this.OnPlaybackPaused.Invoke(this.gameObjectName);

            return result;
        }
        /// <summary>
        /// Stops and releases the sound associated with channel
        /// </summary>
        /// <param name="audioUri"></param>
        public void ReleaseUserSound(FMOD.Channel channel)
        {
            // prevent editor reloading related event/s
            if (!channel.hasHandle())
                return;

            if (this.outputdevice_system != null)
            {
                // this _will_ get called with valid handles despite the fact that system released running sounds in OnDestroy on the component and also e.g. in OnDestroy in the demo/test scene..
                result = this.outputdevice_system.ReleaseUserSound(channel, this.logLevel, this.gameObjectName, this.OnError);
                // ERRCHECK(result, "outputdevice_system.ReleaseUserSound", false);
            }
            else
            {
                result = FMOD.RESULT.OK;
            }

            if (result == FMOD.RESULT.OK)
            {
                this.channels.Remove(channel);

                if (this.OnPlaybackStopped != null)
                    this.OnPlaybackStopped.Invoke(this.gameObjectName);
            }
        }

        public bool IsSoundPaused(FMOD.Channel channel)
        {
            bool paused;
            result = channel.getPaused(out paused);
            // ERRCHECK(result, "channel.getPaused", false);

            if (result == FMOD.RESULT.OK)
                return paused;
            else
                return false;
        }

        public bool IsSoundPlaying(FMOD.Channel channel)
        {
            bool isPlaying;
            result = channel.isPlaying(out isPlaying);
            // ERRCHECK(result, "channel.isPlaying", false);

            bool paused = this.IsSoundPaused(channel);

            if (result == FMOD.RESULT.OK && !paused)
                return isPlaying;
            else
                return false;
        }
        public float GetVolume(FMOD.Channel channel)
        {
            float volume;
            result = channel.getVolume(out volume);
            // ERRCHECK(result, "channel.getVolume", false);

            if (result == FMOD.RESULT.OK)
                return volume;
            else
                return 0f;
        }
        public void SetVolume(FMOD.Channel channel, float volume)
        {
            result = channel.setVolume(volume);
            // ERRCHECK(result, "channel.setVolume", false);
        }

        public void SetPitch(FMOD.Channel channel, float pitch)
        {
            result = channel.setPitch(pitch);
            // ERRCHECK(result, "channel.setPitch", false);
        }
        public float GetPitch(FMOD.Channel channel)
        {
            float pitch;
            result = channel.getPitch(out pitch);
            // ERRCHECK(result, "channel.getPitch", false);

            if (result == FMOD.RESULT.OK)
                return pitch;
            else
                return 1f;
        }
        public void SetTimeSamples(FMOD.Channel channel, int timeSamples)
        {
            result = channel.setPosition((uint)timeSamples, FMOD.TIMEUNIT.PCM);
            // ERRCHECK(result, "channel.setPosition", false);
        }
        public int GetTimeSamples(FMOD.Channel channel)
        {
            uint timeSamples;
            result = channel.getPosition(out timeSamples, FMOD.TIMEUNIT.PCM);
            if (result == FMOD.RESULT.OK)
                return (int)timeSamples;
            else
                return -1;
        }

        public int GetLengthSamples(FMOD.Channel channel)
        {
            uint lengthSamples;
            FMOD.Sound sound;
            result = channel.getCurrentSound(out sound);
            if (result == FMOD.RESULT.OK)
            {
                result &= sound.getLength(out lengthSamples, FMOD.TIMEUNIT.PCM);
                if (result == FMOD.RESULT.OK)
                    return (int)lengthSamples;
                else return -1;
            }
            return -1;
        }
        #endregion
        // ========================================================================================================================================
        #region Channel mix matrix
        public FMOD.RESULT SetMixMatrix(FMOD.Channel channel
            , float[,] mixMatrix
            , int outchannels
            , int inchannels
            )
        {
            return this.outputdevice_system.SetMixMatrix(channel, mixMatrix, outchannels, inchannels);
        }

        public FMOD.RESULT GetMixMatrix(FMOD.Channel channel, out float[,] mixMatrix, out int outchannels, out int inchannels)
        {
            return this.outputdevice_system.GetMixMatrix(channel, out mixMatrix, out outchannels, out inchannels);
        }
        #endregion
    }
}
