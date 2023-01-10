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
    /// Unity sound (AudioSource/Listener) capture
    /// Main part tying together Unity AudioSource/Listener and FMOD output via a FMOD user sound
    /// </summary>
    public class AudioSourceOutputDevice : MonoBehaviour
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[Setup]")]
        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;
        [Tooltip("Specify any available audio output device present in the system, pass an interger number between 0 and (# of audio output devices) - 1 (0 is default output).\r\nThis ID will serve as user requested ID which might not correspond to actually used output ID at runtime - this' RuntimeOutputDriverID will inidcate output at runtime.\r\nRuntimeOutputDriverID reflects addition/removal of devices at runtime when a game object with AudioStreamDevicesChangedNotify component is in the scene. See FMOD_SystemW.AvailableOutputs / notification event from AudioStreamDevicesChangedNotify in the demo scene.")]
        [SerializeField]
        int outputDriverID = 0;
        /// <summary>
        /// public facing property
        /// </summary>
        public int OutputDriverID { get { return this.outputDriverID; } }
        /// <summary>
        /// Output w/ all properties - stored to automatically find driver id at runtime
        /// </summary>
        public FMOD_SystemW.OUTPUT_DEVICE outputDevice { get; protected set; }
        [Tooltip("If output #outputDriverID can't be used at runtime this will indicate on which output is this component actually playing its sound")]
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
        [Tooltip("Mute the signal after being routed.\r\n - otherwise the signal will be audible twice - on FMOD (redirected) and on system (Unity) default output simultaneously\r\nAlso useful when having more than one AudioSourceOutputDevice on one AudioSource/Listener for multiple devices at the same time.\r\n- only the last one in chain should be muted in that case.")]
        public bool muteAfterRouting = true;

        [Header("[Input mix latency (ms)]")]
        [Range(25, 400)]
        [Tooltip("User adjustable latency for the incoming Unity signal (note: that runs under its own separate latency) - \r\nchange according to the actual conditions (i.e. until 'things still work')\r\nThis is FMOD's created sound latency to sample Unity's audio. By default FMOD sets this to 400 ms, which is way too high to be useable. Note that setting this to too low value might cause the output audio to be silenced altogether, even after a while.\r\n- (just) manually restricted in the Inspector to 25-30 ms which should be achievable of common consumer desktops, depends on concrete HW/drivers. You'd have to edit the range (or set it manually) for lower values.")]
        public int inputLatency = 25;

        [Header("[Output device latency (ms) (info only)]")]
        [Tooltip("Computed for current output device at runtime")]
        public float latencyBlock;
        [Tooltip("Computed for current output device at runtime")]
        public float latencyTotal;
        [Tooltip("Computed for current output device at runtime")]
        public float latencyAverage;

        #region Unity events
        [Header("[Events]")]
        public EventWithStringParameter OnRoutingStarted;
        public EventWithStringParameter OnRoutingStopped;
        public EventWithStringStringParameter OnError;
        #endregion

        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        string gameObjectName = string.Empty;
        #endregion

        // ========================================================================================================================================
        #region FMOD && Unity audio callback
        /// <summary>
        /// Component startup sync
        /// </summary>
        [HideInInspector]
        public bool ready = false;
        [HideInInspector]
        public string fmodVersion;
        /// <summary>
        /// The system which plays the captured sound on selected output - one per output, released when sound (routing) is stopped
        /// </summary>
        protected FMOD_SystemW.FMOD_System outputdevice_system = null;
        /// <summary>
        /// FMOD sound ptr passed to static callback
        /// needed to identify this instance's audio buffers
        /// </summary>
        protected FMOD.Sound sound;
        protected FMOD.Channel channel;
        protected FMOD.RESULT result = FMOD.RESULT.OK;
        FMOD.RESULT lastError = FMOD.RESULT.OK;

        #endregion

        // ========================================================================================================================================
        #region Unity lifecycle
        /// <summary>
        /// Retrieve shared FMOD system and create this' sound
        /// </summary>
        protected virtual void Start()
        {
            // FMODSystemsManager.InitializeFMODDiagnostics(FMOD.DEBUG_FLAGS.LOG);
            this.gameObjectName = this.gameObject.name;
            /*
             * start system & sound
             * will be stopped only OnDestroy, or when outptut device changes
             */
            this.StartFMODSoundAndSystem(this.outputDriverID, true);

            this.ready = true;
        }

        protected virtual void Update()
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
        /// <summary>
        /// Forward audio buffer if fmod component is running
        /// (don't touch fmod system update - audio thread interferes even with GC cleanup)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="channels"></param>
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (this.outputdevice_system != null && this.outputdevice_system.SystemHandle != IntPtr.Zero)
            {
                if (this.sound.hasHandle())
                {
                    // direct 'bytewise' copy is enough since we know the format of the output

                    byte[] bArr = new byte[data.Length * sizeof(float)];

                    Buffer.BlockCopy(data, 0, bArr, 0, bArr.Length);

                    // this can happen - is not fast enough when e.g. switching outputs
                    if (this.outputdevice_system != null)
                        this.outputdevice_system.Feed(this.sound, bArr);
                }
            }

            if (this.muteAfterRouting)
                // clear the output buffer if needed
                // e.g. when this component is the last one in audio chain (at the bottom in the inspector)
                Array.Clear(data, 0, data.Length);
        }
        /// <summary>
        /// OnDestroy should be proper place to release the sound - it's called on scene unload / application quit and since the sound should be running for the lifetime of this GO this is should be last place to properly release it
        /// </summary>
        protected virtual void OnDestroy()
        {
            this.StopFMODSoundAndReleaseSystem();
        }
        #endregion

        // ========================================================================================================================================
        #region internal FMOD Start / Stop
        /// <summary>
        /// Creates a sound on shared FMOD system
        /// </summary>
        /// <param name="rememberOutput">Remembers set output to reacquire it later on devices changes</param>
        protected virtual void StartFMODSoundAndSystem(int onOutputDriverID, bool rememberOutput)
        {
            /*
             * before creating system for target output, check if it wouldn't fail with it first :: device list can be changed @ runtime now
             * if it would, fallback to default ( 0 ) which should be hopefully always available - otherwise we would have failed miserably some time before already
             */
            var availableOutputs = FMOD_SystemW.AvailableOutputs(this.logLevel, this.gameObjectName, this.OnError);
            if (!availableOutputs.Select(s => s.id).Contains(onOutputDriverID))
            {
                LOG(LogLevel.WARNING, "Output device {0} is not available, using default output (0) as fallback", onOutputDriverID);

                onOutputDriverID = 0;
                // play on 0 if not present ?
            }

            uint dspBufferLength, dspBufferCount;
            this.outputdevice_system = FMOD_SystemW.FMOD_System_Create(onOutputDriverID, true, this.logLevel, this.gameObjectName, this.OnError, out dspBufferLength, out dspBufferCount);
            this.fmodVersion = this.outputdevice_system.VersionString;

            var channels = UnityAudio.ChannelsFromUnityDefaultSpeakerMode();

            // store sound to associate this instance with static callback
            // TODO: reenable dspBufferLength if DSP buffer settings are exposed again per system
            (this.sound, this.channel) = this.outputdevice_system.CreateAndPlaySound(channels, this.inputLatency, this.logLevel, this.gameObjectName, this.OnError);

            // Debug.LogFormat(@"Updating {0} to: {1}", this.outputDriverID, newOutputDriverID);

            this.runtimeOutputDriverID = onOutputDriverID;
            this.runtimeOutputDevice = availableOutputs[this.runtimeOutputDriverID];

            // store output info if called by user to find it later if/when needed
            if (rememberOutput)
                this.outputDevice = availableOutputs[this.outputDriverID];

            LOG(LogLevel.INFO, string.Format("Started routing to device {0}", this.outputDriverID));

            if (this.OnRoutingStarted != null)
                this.OnRoutingStarted.Invoke(this.gameObjectName);

            int samplerate;
            FMOD.SPEAKERMODE sm;
            int speakers;
            result = this.outputdevice_system.system.getSoftwareFormat(out samplerate, out sm, out speakers);
            ERRCHECK(result, "outputdevice_system.system.getSoftwareFormat");

            float ms = (float)dspBufferLength* 1000.0f / (float)samplerate;

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
        protected virtual void StopFMODSoundAndReleaseSystem()
        {
            if (this.outputdevice_system != null)
            {
                this.outputdevice_system.StopSound(this.sound, this.logLevel, this.gameObjectName, this.OnError);

                FMOD_SystemW.FMOD_System_Release(ref this.outputdevice_system, this.logLevel, this.gameObjectName, this.OnError);

                LOG(LogLevel.INFO, string.Format("Stopped current routing"));

                if (this.OnRoutingStopped != null)
                    this.OnRoutingStopped.Invoke(this.gameObjectName);
            }
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

            // routing is always running so restart it with new output
            this.StopFMODSoundAndReleaseSystem();
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

            this.StartFMODSoundAndSystem(outputID, false);
        }
        #endregion

        // ========================================================================================================================================
        #region Support
        protected void ERRCHECK(FMOD.RESULT result, string customMessage, bool throwOnError = true)
        {
            this.lastError = result;

            FMODHelpers.ERRCHECK(result, this.logLevel, this.gameObjectName, this.OnError, customMessage, throwOnError);
        }

        protected void LOG(LogLevel requestedLogLevel, string format, params object[] args)
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
        #region User support
        /// <summary>
        /// Changing output device means we have to set sound format, which is allowed only before system init -> we have to restart
        /// Invoked only by user i.e. only after output drivers list is settled and not because of a devices change notification 
        /// </summary>
        /// <param name="_outputDriverID"></param>
        public void SetOutput(int _outputDriverID)
        {
            if (!this.ready)
            {
                LOG(LogLevel.ERROR, "Please make sure to wait for 'ready' flag before calling this method");
                return;
            }

            this.outputDriverID = _outputDriverID;

            // routing is always running so restart it with new output
            this.StopFMODSoundAndReleaseSystem();

            this.StartFMODSoundAndSystem(_outputDriverID, true);
        }
        /// <summary>
        /// this' PCM callback buffer
        /// mainly for displaying some stats
        /// </summary>
        /// <returns></returns>
        public PCMCallbackBuffer PCMCallbackBuffer()
        {
            if (!this.sound.hasHandle())
                return null;

            return FMOD_SystemW.FMOD_System.PCMCallbackBuffer(this.sound);
        }
        /// <summary>
        /// Wrapper call around FMOD's setMixMatrix for current FMOD sound master channel - this means all Unity audio using this output will use this (last set) mix matrix
        /// Useful only in rather specific cases when *all* audio being played on this output will go to selected output channels
        /// </summary>
        /// <param name="matrix"></param>
        /// <param name="outchannels"></param>
        /// <param name="inchannels"></param>
        public void SetUnitySound_MixMatrix(float[] matrix, int outchannels, int inchannels)
        {
            if (!this.ready)
            {
                Debug.LogErrorFormat("Please make sure to wait for this component's 'ready' flag before calling this method");
                return;
            }

            if (outchannels * inchannels != matrix.Length)
            {
                Debug.LogErrorFormat("Make sure to provide correct mix matrix dimensions");
                return;
            }

            FMOD.ChannelGroup channel;
            result = this.outputdevice_system.system.getMasterChannelGroup(out channel);
            ERRCHECK(result, "outputdevice_system.system.getMasterChannelGroup");

            if (!channel.hasHandle())
            {
                LOG(LogLevel.ERROR, "AudioSourceOutputDevice not yet initialized before usage.");
                return;
            }

            result = channel.setMixMatrix(matrix, outchannels, inchannels, inchannels);
            ERRCHECK(result, "channel.setMixMatrix");

            var matrixAsString = string.Empty;

            for (var row = 0; row < outchannels; ++row)
            {
                for (var column = 0; column < inchannels; ++column)
                    matrixAsString += matrix[row * inchannels + column];

                matrixAsString += "\r\n";
            }

            LOG(LogLevel.INFO, "Set custom mix matrix to:\r\n{0}", matrixAsString);
        }
        #endregion
    }
}
