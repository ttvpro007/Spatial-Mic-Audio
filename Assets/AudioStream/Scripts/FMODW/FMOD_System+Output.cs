// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// FMOD systems manager/wrapper
    /// Interface for FMOD system objects + their related functionality
    /// </summary>
    public static partial class FMOD_SystemW
    {
        // ========================================================================================================================================
        #region FMOD system object
        public partial class FMOD_System
        {
            // ========================================================================================================================================
            #region fmod pcmread callbacks
            /// <summary>
            /// An FMOD sound specific audio buffers for static PCM read callbacks
            /// </summary>
            public static Dictionary<System.IntPtr, PCMCallbackBuffer> pcmCallbackBuffers = new Dictionary<System.IntPtr, PCMCallbackBuffer>();
            /// <summary>
            /// Make instance cb buffer size some large(r) number in order not to shuffle around much
            /// </summary>
            const int cbuffer_capacity = 100000;
            static object pcm_callback_lock = new object();
            // Callback has to be a static method for IL2CPP/AOT to be able to make the delegate call
            [AOT.MonoPInvokeCallback(typeof(FMOD.SOUND_PCMREAD_CALLBACK))]
            static FMOD.RESULT PCMReadCallback(System.IntPtr soundraw, System.IntPtr data, uint datalen)
            {
                lock (FMOD_System.pcm_callback_lock)
                {
                    // clear the array first - fix for non running AudioSource
                    var zeroArr = new byte[datalen];
                    global::System.Runtime.InteropServices.Marshal.Copy(zeroArr, 0, data, (int)datalen);

                    // retrieve instance specific buffer
                    PCMCallbackBuffer pCMCallbackBuffer = null;
                    if (!FMOD_System.pcmCallbackBuffers.TryGetValue(soundraw, out pCMCallbackBuffer))
                    {
                        // create pcm buffer with capacity with requested length of the callback
                        pCMCallbackBuffer = new PCMCallbackBuffer(cbuffer_capacity);
                        FMOD_System.pcmCallbackBuffers.Add(soundraw, pCMCallbackBuffer);
                    }

                    // store few useful statistics
                    if (datalen > pCMCallbackBuffer.maxdatalen)
                        pCMCallbackBuffer.maxdatalen = datalen;

                    if (datalen < pCMCallbackBuffer.mindatalen && datalen > 0)
                        pCMCallbackBuffer.mindatalen = datalen;

                    pCMCallbackBuffer.datalen = datalen;

                    // resize instance buffer if needed - FMOD can adjust length of the requested buffer dynamically - in grow direction only
                    if (datalen > pCMCallbackBuffer.Capacity)
                    {
                        // spam this as warning only when resizing is taking place when the buffer is already somehow large
                        if (pCMCallbackBuffer.Capacity > 1000000)
                            Debug.LogWarningFormat("PCMReadCallback {0} increase change, requested: {1} / capacity: {2}", soundraw, datalen, pCMCallbackBuffer.Capacity);

                        // copy out existing data
                        var copy = pCMCallbackBuffer.Dequeue(pCMCallbackBuffer.Available);

                        // replace instance buffer preserving existing data
                        FMOD_System.pcmCallbackBuffers[soundraw] = null;

                        pCMCallbackBuffer = new PCMCallbackBuffer((int)datalen * 2);

                        // restore existing data
                        pCMCallbackBuffer.Enqueue(copy);

                        FMOD_System.pcmCallbackBuffers[soundraw] = pCMCallbackBuffer;
                    }

                    // copy out available bytes
                    var count_available = pCMCallbackBuffer.Available;
                    var count_provide = (int)Mathf.Min(count_available, (int)datalen);

                    pCMCallbackBuffer.underflow = count_available < datalen;
                    // in case of input buffer underflow there's unfortunately little we can do automatically - OAFR simply can't provide data fast enough (or is stopped) - 
                    // usually the best course of action is to match # of input and output channels and/or
                    // improve Unity audio bandwidth (change DSP Buffer Size in AudioManager) and/or on Windows install low latency drivers

                    // return all available (if skipped entirely when pCMCallbackBuffer.underflow FMOD will duck volume which is noticeable
                    var audioArr = pCMCallbackBuffer.Dequeue(count_provide);
                    global::System.Runtime.InteropServices.Marshal.Copy(audioArr, 0, data, audioArr.Length);

                    return FMOD.RESULT.OK;
                }
            }

            [AOT.MonoPInvokeCallback(typeof(FMOD.SOUND_PCMSETPOS_CALLBACK))]
            static FMOD.RESULT PCMSetPosCallback(System.IntPtr soundraw, int subsound, uint position, FMOD.TIMEUNIT postype)
            {
                /*
                Debug.LogFormat("PCMSetPosCallback sound {0}, subsound {1} requesting position {2}, postype {3}, time: {4}"
                    , soundraw
                    , subsound
                    , position
                    , postype
                    , AudioSettings.dspTime
                    );
                    */
                return FMOD.RESULT.OK;
            }
            /// <summary>
            /// Mainly for displaying some stats
            /// </summary>
            /// <returns></returns>
            public static PCMCallbackBuffer PCMCallbackBuffer(FMOD.Sound sound)
            {
                if (!FMOD_System.pcmCallbackBuffers.ContainsKey(sound.handle))
                    return null;

                return FMOD_System.pcmCallbackBuffers[sound.handle];
            }
            #endregion
            // ========================================================================================================================================
            #region The output sound
            FMOD.SOUND_PCMREAD_CALLBACK pcmreadcallback;
            FMOD.SOUND_PCMSETPOS_CALLBACK pcmsetposcallback;
            /// <summary>
            /// Sounds automatically created for buffer redirection (more than one component might be using the same system)
            /// And user sounds played by this system for direct user playback
            /// Contains pairs of sound/channel for now for simplicity to track finished channels in order to re/start them for user sounds
            /// - channels finished playing are/can be released from the system so any operation on them afterwards results in INVALID_CHANNEL
            /// Used for tracking playing sounds in StopSound for automatic system release
            /// </summary>
            readonly Dictionary<FMOD.Sound, FMOD.Channel> sounds = new Dictionary<FMOD.Sound, FMOD.Channel>();
            /// <summary>
            /// Creates this system's sound and sets up pcm callbacks
            /// The sounds has Unity's samplerate and output channels to match 'input' audio
            /// </summary>
            /// <param name="soundChannels"></param>
            /// <param name="logLevel"></param>
            /// <param name="gameObjectName"></param>
            /// <param name="onError"></param>
            /// <returns>Created FMOD sound</returns>
            public (FMOD.Sound, FMOD.Channel) CreateAndPlaySound(int soundChannels
                , int latency
                , LogLevel logLevel
                , string gameObjectName
                , EventWithStringStringParameter onError
                )
            {
                /*
                 * setup FMOD callback:
                 * 
                 * the created sound should match current audio -
                 * samplerate is current Unity output samplerate, channels are from Unity's speaker mode
                 * Resampling and channel distribution is handled by FMOD (uses output device's default settings, unless changed by user)
                 * 
                 * - pcmcallback uses a 16k block and calls the callback multiple times if it needs more data.
                 * - extinfo.decodebuffersize determines the size of the double buffer (in PCM samples) that a stream uses.  Use this for user created streams if you want to determine the size of the callback buffer passed to you.  Specify 0 to use FMOD's default size which is currently equivalent to 400ms of the sound format created/loaded.
                 * 
                 * extinfo.decodebuffersize:
                 * - FMOD does not seem to work with anything lower than 1024 properly at all
                 * - directly affects latency of the output (e.g. volume changes..)
                 * - if it's too high, the latency is very high, defaul value (0) gives 400 ms latency, which is not very useable
                 * so the goal is to minize it as much as possible -
                 * - if it's too low, and in the scene are 'many' AudioSources with e.g. 7.1 output (high bandwidth), the sound sometimes just stops playing (although all callbacks are still firing... )
                 * (was using 1024 as 'default' where this happened)
                 * We'll try to reach 50 ms
                 * 
                 * - createSound calls back immediately once after it is created
                 */

                // PCMFLOAT format maps directly to Unity audio buffer so we don't have to do any conversions
                var elementSize = global::System.Runtime.InteropServices.Marshal.SizeOf(typeof(float));

                // all incoming bandwidth
                var nAvgBytesPerSec = soundChannels * AudioSettings.outputSampleRate * elementSize;
                Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Default channels from Unity speaker mode: {0} (samplerate: {1}, elment size: {2}) -> nAvgBytesPerSec: {3}", soundChannels, AudioSettings.outputSampleRate, elementSize, nAvgBytesPerSec);

                var msPerSample = latency / (float)soundChannels / 1000f;
                Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Requested latency: {0}, default channels: {1} -> msPerSample: {2}", latency, soundChannels, msPerSample);

                var decodebuffersize = (uint)Mathf.Max(nAvgBytesPerSec * msPerSample, 1024);

                if ((nAvgBytesPerSec * msPerSample) < 1024)
                    Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Not setting decodebuffersize below 1024 minimum");

                Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Avg bytes per sec: {0}, msPerSample: {1} -> exinfo.decodebuffersize: {2}", nAvgBytesPerSec, msPerSample, decodebuffersize);

                // Explicitly create the delegate object and assign it to a member so it doesn't get freed by the garbage collector while it's not being used
                this.pcmreadcallback = new FMOD.SOUND_PCMREAD_CALLBACK(PCMReadCallback);
                this.pcmsetposcallback = new FMOD.SOUND_PCMSETPOS_CALLBACK(PCMSetPosCallback);

                FMOD.CREATESOUNDEXINFO exinfo = new FMOD.CREATESOUNDEXINFO();
                // exinfo.cbsize = sizeof(FMOD.CREATESOUNDEXINFO);
                exinfo.numchannels = soundChannels;                                                         /* Number of channels in the sound. */
                exinfo.defaultfrequency = AudioSettings.outputSampleRate;                                   /* Default playback rate of sound. */
                exinfo.decodebuffersize = decodebuffersize;                                                 /* Chunk size of stream update in samples. This will be the amount of data passed to the user callback. */
                exinfo.length = (uint)(exinfo.defaultfrequency * exinfo.numchannels * elementSize);         /* Length of PCM data in bytes of whole song (for Sound::getLength) - this is 1 s here - (does not affect latency) */
                exinfo.format = FMOD.SOUND_FORMAT.PCMFLOAT;                                                 /* Data format of sound. */
                exinfo.pcmreadcallback = this.pcmreadcallback;                                              /* User callback for reading. */
                exinfo.pcmsetposcallback = this.pcmsetposcallback;                                          /* User callback for seeking. */
                exinfo.cbsize = global::System.Runtime.InteropServices.Marshal.SizeOf(exinfo);


                FMOD.Sound sound = default(FMOD.Sound);
                FMOD.Channel channel;

                result = this.system.createSound(gameObjectName + "_FMOD_sound"
                        , FMOD.MODE.OPENUSER
                        | FMOD.MODE.CREATESTREAM
                        | FMOD.MODE.LOOP_NORMAL
                        , ref exinfo
                        , out sound);
                FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "this.system.createSound");

                FMOD.ChannelGroup master;
                result = this.system.getMasterChannelGroup(out master);
                FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "this.system.getMasterChannelGroup");

                result = this.system.playSound(sound, master, false, out channel);
                FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "this.system.playSound");

                this.sounds.Add(sound, default(FMOD.Channel));

                Log.LOG(LogLevel.DEBUG, logLevel, gameObjectName, "Created sound {0} on system {1} / output {2}", sound.handle, this.system.handle, this.initialOutpuDevice.id);

                return (sound, channel);
            }
            /// <summary>
            /// Stops the sound, removes its reference from callbacks and removes it from internal playing list
            /// </summary>
            /// <param name="sound"></param>
            /// <param name="logLevel"></param>
            /// <param name="gameObjectName"></param>
            /// <param name="onError"></param>
            public FMOD.RESULT StopSound(FMOD.Sound sound
                , LogLevel logLevel
                , string gameObjectName
                , EventWithStringStringParameter onError
                )
            {
                // pause was in original sample; however, it causes noticeable pop on default device when stopping the sound
                // removing it does not _seem_ to affect anything

                // global::System.Threading.Thread.Sleep(50);

                if (sound.hasHandle() && this.sounds.ContainsKey(sound))
                {
                    // cbnba
                    global::System.Threading.Thread.Sleep(10);

                    result = sound.release();
                    FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "sound.release");


                    // remove stopped sound from internal track list and from static callbacks
                    this.sounds.Remove(sound);
                    FMOD_System.pcmCallbackBuffers.Remove(sound.handle);

                    Log.LOG(LogLevel.DEBUG, logLevel, gameObjectName, "Released sound {0} for system {1}, output {2}", sound.handle, this.SystemHandle, this.initialOutpuDevice.id);

                    // sets properly sound's handle to IntPtr.Zero
                    sound.clearHandle();
                }
                else
                {
                    Log.LOG(LogLevel.WARNING, LogLevel.WARNING, gameObjectName, "Not releasing sound {0} for system {1}, output {2}, {3} in list", sound.handle, this.SystemHandle, this.initialOutpuDevice.id, this.sounds.ContainsKey(sound) ? "" : "NOT");
                }

                return result;
            }
            /// <summary>
            /// Pass on the output audio
            /// </summary>
            /// <param name="sound"></param>
            /// <param name="bpcm"></param>
            public void Feed(FMOD.Sound sound, byte[] bpcm)
            {
                if (sound.hasHandle() && FMOD_System.pcmCallbackBuffers.ContainsKey(sound.handle))
                    FMOD_System.pcmCallbackBuffers[sound.handle].Enqueue(bpcm);
            }
            /// <summary>
            /// Returns # of sounds currently being played by this system
            /// </summary>
            /// <returns></returns>
            //public int SoundsPlaying()
            //{
            //    return this.sounds.Count;
            //}
            #endregion
            // ========================================================================================================================================
            #region User sound
            /// <summary>
            /// Plays user audio file (allowed netstream) directly by FMOD
            /// - creates a new sound on this system and optionally sets a mix matrix on channel - that means that there's always 1 sound : 1 channel, subsequent calls can update mix matrix
            /// </summary>
            /// <param name="audioUri">filename - full or relative file path or netstream address</param>
            /// <param name="loop"></param>
            /// <param name="volume"></param>
            /// <param name="startImmediately">the sound can be created in paused state or be played immediately after creation</param>
            /// <param name="withMixmatrix">optional mix matrix - pass null when not needed</param>
            /// <param name="outchannels">ignored if withMixmatrix == null</param>
            /// <param name="inchannels">ignored if withMixmatrix == null</param>
            /// <param name="logLevel"></param>
            /// <param name="gameObjectName"></param>
            /// <param name="onError"></param>
            /// <param name="channel">FMOD channel returned to the user to be used in subsequent calls</param>
            /// <returns></returns>
            public FMOD.RESULT CreateUserSound(string audioUri
                , bool loop
                , float volume
                , bool startImmediately
                , float[,] withMixmatrix
                , int outchannels
                , int inchannels
                , LogLevel logLevel
                , string gameObjectName
                , EventWithStringStringParameter onError
                , out FMOD.Channel channel
                )
            {
                channel = default(FMOD.Channel);

                // preliminary check for matrix dimensions if it's specified
                if (withMixmatrix != null
                    &&
                    outchannels * inchannels != withMixmatrix.Length)
                {
                    Log.LOG(LogLevel.ERROR, logLevel, gameObjectName, "Make sure to provide correct mix matrix dimensions: {0} x {1} != {2}", outchannels, inchannels, withMixmatrix.Length);
                    return FMOD.RESULT.ERR_INVALID_PARAM;
                }

                // create a new sound
                var sound = default(FMOD.Sound);
                FMOD.CREATESOUNDEXINFO exinfo = new FMOD.CREATESOUNDEXINFO();
                exinfo.cbsize = global::System.Runtime.InteropServices.Marshal.SizeOf(exinfo);

                result = this.system.createSound(audioUri
                        , FMOD.MODE.DEFAULT
                        | FMOD.MODE.LOOP_NORMAL // looping is controlled w/ setLoopCount
                        , ref exinfo
                        , out sound);
                FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, string.Format("this.system.createSound {0}", audioUri), false);

                if (result != FMOD.RESULT.OK)
                    return result;

                result = this.PlayUserChannel(sound, volume, startImmediately, loop, withMixmatrix, outchannels, inchannels, logLevel, gameObjectName, onError, out channel);
                if (result != FMOD.RESULT.OK)
                    return result;

                this.sounds.Add(sound, channel);

                return result;
            }
            /// <summary>
            /// Releases and removes sound associated with the channel from the system 
            /// </summary>
            /// <param name="channel"></param>
            /// <param name="logLevel"></param>
            /// <param name="gameObjectName"></param>
            /// <param name="onError"></param>
            /// <returns></returns>
            public FMOD.RESULT ReleaseUserSound(FMOD.Channel channel
                , LogLevel logLevel
                , string gameObjectName
                , EventWithStringStringParameter onError
                )
            {
                var sound = this.sounds.FirstOrDefault(f => f.Value.handle == channel.handle);

                // prevent warning on already released sound
                if (!sound.Key.hasHandle())
                    return FMOD.RESULT.OK;

                if (result == FMOD.RESULT.OK)
                    return this.StopSound(sound.Key, logLevel, gameObjectName, onError);
                else
                    return FMOD.RESULT.ERR_INVALID_PARAM;
            }
            /// <summary>
            /// Plays new channel on the sound / master
            /// </summary>
            /// <param name="onSound"></param>
            /// <param name="volume"></param>
            /// <param name="startImmediately"></param>
            /// <param name="withMixmatrix"></param>
            /// <param name="outchannels"></param>
            /// <param name="inchannels"></param>
            /// <param name="logLevel"></param>
            /// <param name="gameObjectName"></param>
            /// <param name="onError"></param>
            /// <param name="channel"></param>
            /// <returns></returns>
            FMOD.RESULT PlayUserChannel(FMOD.Sound onSound
                , float volume
                , bool startImmediately
                , bool loop
                , float[,] withMixmatrix
                , int outchannels
                , int inchannels
                , LogLevel logLevel
                , string gameObjectName
                , EventWithStringStringParameter onError
                , out FMOD.Channel channel
                )
            {
                // play the sound on master
                FMOD.ChannelGroup master;
                result = this.system.getMasterChannelGroup(out master);
                FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "this.system.getMasterChannelGroup");

                var loopcount = loop ? -1 : 0;
                result = onSound.setLoopCount(loopcount);
                FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "onSound.setLoopCount");

                // (log verification)
                result = onSound.getLoopCount(out loopcount);
                FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "onSound.getLoopCount");


                result = this.system.playSound(onSound, master, !startImmediately, out channel);
                FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "this.system.playSound");

                result = channel.setVolume(volume);
                FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "channel.setVolume");

                if (withMixmatrix != null)
                {
                    result = this.SetMixMatrix(channel, withMixmatrix, outchannels, inchannels);
                    FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "SetMixMatrix");
                }

                return result;
            }
            /// <summary>
            /// Takes now dead channel, tries to find its sound ( see this.sounds declaratino ) and start a new channel on it
            /// </summary>
            /// <param name="channel"></param>
            /// <param name="volume"></param>
            /// <param name="startImmediately"></param>
            /// <param name="withMixmatrix"></param>
            /// <param name="outchannels"></param>
            /// <param name="inchannels"></param>
            /// <param name="logLevel"></param>
            /// <param name="gameObjectName"></param>
            /// <param name="onError"></param>
            /// <param name="newChannel"></param>
            /// <returns></returns>
            public FMOD.RESULT PlayUserChannel(FMOD.Channel channel
                , float volume
                , bool loop
                , float[,] withMixmatrix
                , int outchannels
                , int inchannels
                , LogLevel logLevel
                , string gameObjectName
                , EventWithStringStringParameter onError
                , out FMOD.Channel newChannel
                )
            {
                var sound = this.sounds.FirstOrDefault(f => f.Value.handle == channel.handle);

                if (this.sounds.ContainsKey(sound.Key))
                {
                    result = this.PlayUserChannel(sound.Key, volume, true, loop, withMixmatrix, outchannels, inchannels, logLevel, gameObjectName, onError, out newChannel);
                    if (result == FMOD.RESULT.OK)
                    {
                        this.sounds.Remove(sound.Key);
                        this.sounds.Add(sound.Key, newChannel);
                    }

                    return result;
                }
                else
                {
                    newChannel = channel;
                    return FMOD.RESULT.ERR_DSP_NOTFOUND;
                }
            }
            #endregion
            // ========================================================================================================================================
            #region User channel mix matrix
            public FMOD.RESULT SetMixMatrix(FMOD.Channel forChannel
                , float[,] mixMatrix
                , int outchannels
                , int inchannels)
            {
                // check for matrix dimensions
                if (outchannels * inchannels != mixMatrix.Length)
                    return FMOD.RESULT.ERR_INVALID_PARAM;

                // flatten the matrix for API call
                float[] mixMatrix_flatten = new float[outchannels * inchannels];
                for (var r = 0; r < outchannels; ++r)
                {
                    for (var c = 0; c < inchannels; ++c)
                        mixMatrix_flatten[r * inchannels + c] = mixMatrix[r, c];
                }

                return forChannel.setMixMatrix(mixMatrix_flatten, outchannels, inchannels);
            }

            public FMOD.RESULT GetMixMatrix(FMOD.Channel ofChannel, out float[,] mixMatrix, out int outchannels, out int inchannels)
            {
                mixMatrix = null;
                outchannels = inchannels = 0;

                float[] mixMatrix_flatten = null;

                result = ofChannel.getMixMatrix(mixMatrix_flatten, out outchannels, out inchannels);
                if (result != FMOD.RESULT.OK)
                    return result;

                mixMatrix = new float[outchannels, inchannels];
                for (var i = 0; i < mixMatrix_flatten.Length; ++i)
                    mixMatrix[i / inchannels, i % outchannels] = mixMatrix_flatten[i];

                return result;
            }
            #endregion
        }
        #endregion
        // ========================================================================================================================================
        #region Output enumeration
        /// <summary>
        /// output info from getDriverInfo
        /// </summary>
        public struct OUTPUT_DEVICE
        {
            public int id;
            public string name;
            public System.Guid guid;
            public int samplerate;
            public FMOD.SPEAKERMODE speakermode;
            public int channels;
        }
        /// <summary>
        /// Enumerates available audio outputs in the system and returns their list
        /// Uses default system for enumeration
        /// Enumeration is done via newly created (and immediately afterwards released) separate FMOD system for driver 0 to correctly reflect runtime devices changes
        /// </summary>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        /// <returns></returns>
        public static List<OUTPUT_DEVICE> AvailableOutputs(LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            // (make sure to not throw an exception anywhere so the system is always released)
            var fmodsystem = FMOD_SystemW.FMODSystem0_Create(logLevel, gameObjectName, onError);

            List<OUTPUT_DEVICE> availableDrivers = new List<OUTPUT_DEVICE>();

            var result = fmodsystem.Update();
            FMODHelpers.ERRCHECK(result, LogLevel.ERROR, gameObjectName, onError, "fmodsystem.Update", false);

            int numDrivers;
            result = fmodsystem.system.getNumDrivers(out numDrivers);
            FMODHelpers.ERRCHECK(result, LogLevel.ERROR, gameObjectName, onError, "fmodsystem.System.getNumDrivers", false);

            for (int i = 0; i < numDrivers; ++i)
            {
                int namelen = 255;
                string name;
                System.Guid guid;
                int systemrate;
                FMOD.SPEAKERMODE speakermode;
                int speakermodechannels;

                result = fmodsystem.system.getDriverInfo(i, out name, namelen, out guid, out systemrate, out speakermode, out speakermodechannels);
                // nspam FMODHelpers.ERRCHECK(result, LogLevel.ERROR, gameObjectName, onError, "fmodsystem.System.getDriverInfo", false);

                if (result != FMOD.RESULT.OK)
                {
                    // 00000000-0000-0000-0000-000000000000
                    if (guid == System.Guid.Empty)
                    {
                        // this happen only when a device is added/removed only *after* a system is created - which should not be the case here
                        Log.LOG(LogLevel.ERROR, logLevel, gameObjectName, "Output #{0} probably disappeared", i);
                    }
                    else
                    {
                        Log.LOG(LogLevel.ERROR, logLevel, gameObjectName, "!error output {0} guid: {1} systemrate: {2} speaker mode: {3} channels: {4}"
                            , name
                            , guid
                            , systemrate
                            , speakermode
                            , speakermodechannels
                            );
                    }

                    continue;
                }

                availableDrivers.Add(new OUTPUT_DEVICE() { id = i, name = name, guid = guid, samplerate = systemrate, speakermode = speakermode, channels = speakermodechannels });

                Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "{0} guid: {1} systemrate: {2} speaker mode: {3} channels: {4}"
                    , name
                    , guid
                    , systemrate
                    , speakermode
                    , speakermodechannels
                    );
            }

            // release
            FMOD_SystemW.FMODSystem0_Release(ref fmodsystem, logLevel, gameObjectName, onError);

            return availableDrivers;
        }
        #endregion
    }
}
