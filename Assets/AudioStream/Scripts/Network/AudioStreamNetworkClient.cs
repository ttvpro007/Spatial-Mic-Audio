// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using AudioStreamSupport;
using Concentus.Structs;
using System;
using System.Collections;
#if UNITY_WSA
using System.Threading.Tasks;
#else
using System.Threading;
# endif
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Base abstract class for network client
    /// Provides audio decoding and queuing, leaving newtork implementation for its descendant 
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public abstract class AudioStreamNetworkClient : MonoBehaviour
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[Setup]")]

        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;

        [Header("[Audio]")]
        [Range(0f, 1f)]
        public float volume = 1f;

        [Header("[Decoder]")]

        [Tooltip("You can increase the encoder thread priority if needed, but it's usually ok to leave it even below default Normal depending on how network and main thread perform")]
        public System.Threading.ThreadPriority decoderThreadPriority = System.Threading.ThreadPriority.Normal;

        #region Unity events
        [Header("[Events]")]
        public EventWithStringParameter OnConnected;
        public EventWithStringParameter OnDisonnected;
        public EventWithStringStringParameter OnError;
        #endregion
        #endregion

        // ========================================================================================================================================
        #region Non editor
        public string lastErrorString
        {
            get;
            protected set;
        }
        /// <summary>
        /// received compressed audio packet queue
        /// </summary>
        protected ThreadSafeQueue<byte[]> networkQueue { get; set; }
        public int networkQueueSize
        {
            get
            {
                if (this.networkQueue != null)
                    return this.networkQueue.Size();
                else
                    return 0;
            }
        }
        #endregion

        // ========================================================================================================================================
        #region Opus decoder

        OpusDecoder opusDecoder = null;
        /// <summary>
        /// Last detected frame size.
        /// </summary>
        public int opusPacket_frameSize
        {
            get;
            private set;
        }
        /// <summary>
        /// Decode Info
        /// </summary>
        public Concentus.Enums.OpusBandwidth opusPacket_Bandwidth
        {
            get;
            private set;
        }
        public Concentus.Enums.OpusMode opusPacket_Mode
        {
            get;
            private set;
        }
        public int opusPacket_Channels
        {
            get;
            private set;
        }
        public int opusPacket_NumFramesPerPacket
        {
            get;
            private set;
        }
        public int opusPacket_NumSamplesPerFrame
        {
            get;
            private set;
        }
        /// <summary>
        /// Set once first audio packet is processed
        /// </summary>
        public bool decoderRunning
        {
            get;
            private set;
        }
        /// <summary>
        /// Starts decoding
        /// </summary>
        protected void StartDecoder()
        {
            StartCoroutine(this.StartDecoderCR());
        }
        /// <summary>
        /// Starts decoding; waits for samplerate from network before starting playback
        /// </summary>
        IEnumerator StartDecoderCR()
        {
            this.opusDecoder = new OpusDecoder(AudioStreamNetworkSource.opusSampleRate, AudioStreamNetworkSource.opusChannels);

            this.decodeThread =
#if UNITY_WSA
                new Task(new System.Action(this.DecodeLoop), TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
#else
                new Thread(new ThreadStart(this.DecodeLoop));
            this.decodeThread.Priority = this.decoderThreadPriority;
#endif
            this.decoderRunning = true;
            this.decodeThread.Start();

            this.capturedAudioFrame = false;
            this.serverSamplerate = null;
            this.serverChannels = null;
            this.serverIsLittleEndian = null;

            while (!this.serverSamplerate.HasValue
                || !this.serverChannels.HasValue
                || !this.serverIsLittleEndian.HasValue
                )
            {
                LOG(LogLevel.INFO, "Waiting for payload..");
                yield return null;
            }

            var samples = this.serverSamplerate.Value * 5;
            this.outputAudioSamples = new BasicBufferFloat(samples);
            this.@as.clip = AudioClip.Create(this.gameObject.name, samples, this.serverChannels.Value, this.serverSamplerate.Value, true, this.PCMReaderCallback);
            this.@as.loop = true;
            this.@as.Play();

            LOG(LogLevel.INFO, "Started decoder samplerate: {0}, channels: {1}, server rate: {2}", AudioStreamNetworkSource.opusSampleRate, AudioStreamNetworkSource.opusChannels, this.serverSamplerate.Value);
        }

        protected void StopDecoder()
        {
            StopAllCoroutines(); // StartDecoderCR

            if (this.@as != null)
                @as.Stop();

            this.capturedAudioFrame = false;
            this.serverSamplerate = null;
            this.serverChannels = null;
            this.serverIsLittleEndian = null;

            if (this.decodeThread != null)
            {
                // If the thread that calls Abort holds a lock that the aborted thread requires, a deadlock can occur.
                this.decoderRunning = false;
#if !UNITY_WSA
                this.decodeThread.Join();
#endif
                this.decodeThread = null;
            }

            this.opusDecoder = null;
        }

        #endregion

        // ========================================================================================================================================
        #region Decoder loop
        public Int32? serverSamplerate { get; protected set; } = null;
        public Int32? serverChannels { get; protected set; } = null;
        protected bool? serverIsLittleEndian { get; set; } = null;
        float[] fArr;
#if UNITY_WSA
        Task
#else
        Thread
#endif
        decodeThread = null;
        /// <summary>
        /// Continuosly enqueues (decoded) signal into audioQueue
        /// </summary>
        void DecodeLoop()
        {
            while (this.decoderRunning)
            {
                var networkPacket = this.networkQueue.Dequeue();

                if (networkPacket != null
                    && networkPacket.Length > 0
                    )
                {
                    var payloadLength = networkPacket[0];

                    // get server config from packet payload
                    if (!this.serverSamplerate.HasValue
                        || !this.serverChannels.HasValue
                        || !this.serverIsLittleEndian.HasValue
                        )
                    {
                        // at least the payload (config) has to be present
                        if (networkPacket.Length >= payloadLength
                            && payloadLength > 1
                            )
                        {
                            var c = 1;
                            var srate_bytes_length = networkPacket[c++];
                            var srate_bytes = new byte[srate_bytes_length];
                            Array.Copy(networkPacket, c, srate_bytes, 0, srate_bytes_length);
                            c += srate_bytes_length;

                            var schannels_bytes_length = networkPacket[c++];
                            var schannels_bytes = new byte[schannels_bytes_length];
                            Array.Copy(networkPacket, c, schannels_bytes, 0, schannels_bytes_length);
                            c += schannels_bytes_length;

                            var sendianess_bytes_length = networkPacket[c++];
                            var sendianess_bytes = new byte[sendianess_bytes_length];
                            Array.Copy(networkPacket, c, sendianess_bytes, 0, sendianess_bytes_length);

                            this.serverIsLittleEndian = BitConverter.ToBoolean(sendianess_bytes, 0);

                            // server - client differ in their endianess get correct order
                            if (this.serverIsLittleEndian != BitConverter.IsLittleEndian)
                            {
                                Array.Reverse(srate_bytes);
                                Array.Reverse(schannels_bytes);
                            }

                            this.serverSamplerate = BitConverter.ToInt32(srate_bytes, 0);
                            this.serverChannels = BitConverter.ToInt32(schannels_bytes, 0);
                        }
                    }

                    if (this.opusDecoder == null)
                    {
                        this.W84(10);
                        continue;
                    }

                    // skip payload
                    var audioPacket = new byte[networkPacket.Length - payloadLength];
                    Array.Copy(networkPacket, payloadLength, audioPacket, 0, audioPacket.Length);

                    // Normal decoding
                    this.opusPacket_frameSize = OpusPacketInfo.GetNumSamples(audioPacket, 0, audioPacket.Length, AudioStreamNetworkSource.opusSampleRate);
                    this.opusPacket_Bandwidth = OpusPacketInfo.GetBandwidth(audioPacket, 0);
                    this.opusPacket_Mode = OpusPacketInfo.GetEncoderMode(audioPacket, 0);
                    this.opusPacket_Channels = OpusPacketInfo.GetNumEncodedChannels(audioPacket, 0);
                    this.opusPacket_NumFramesPerPacket = OpusPacketInfo.GetNumFrames(audioPacket, 0, audioPacket.Length);
                    this.opusPacket_NumSamplesPerFrame = OpusPacketInfo.GetNumSamplesPerFrame(audioPacket, 0, AudioStreamNetworkSource.opusSampleRate);

                    // keep 2 channels here - decoder can't cope with 1 channel only when e.g. decreased quality
                    short[] decodeBuffer = new short[this.opusPacket_frameSize * 2];

                    // frameSize == thisFrameSize here 
                    int thisFrameSize = this.opusDecoder.Decode(audioPacket, 0, audioPacket.Length, decodeBuffer, 0, this.opusPacket_frameSize, false);

                    if (thisFrameSize > 0)
                    {
                        AudioStreamSupport.UnityAudio.ShortArrayToFloatArray(decodeBuffer, (uint)decodeBuffer.Length, ref this.fArr);
                        this.outputAudioSamples.Write(this.fArr);
                    }
                }
                //else
                //{
                //    // packet loss path not taken here since decoding loop runs usually much faster than audio loop

                //    this.frameSize = 960;
                //    this.serverChannels = 2;

                //    float[] decodeBuffer = new float[this.frameSize * this.serverChannels];

                //    // int thisFrameSize = 
                //    this.opusDecoder.Decode(null, 0, 0, decodeBuffer, 0, this.frameSize, true);

                //    this.audioQueue.Enqueue(decodeBuffer);
                //}

                // don't tax CPU continuosly, but decode as fast as possible
                this.W84(1);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ms"></param>
        void W84(int ms)
        {
#if UNITY_WSA
            this.decodeThread.Wait(ms);
#else
            Thread.Sleep(ms);
#endif
        }
        #endregion

        // ========================================================================================================================================
        #region Unity lifecycle
        AudioSource @as;

        protected virtual void Awake()
        {
            this.gameObjectName = this.gameObject.name;

            this.@as = this.GetComponent<AudioSource>();

            var ac = AudioSettings.GetConfiguration();
            var clientChannels = UnityAudio.ChannelsFromUnityDefaultSpeakerMode();

            this.dspBufferSize = ac.dspBufferSize * clientChannels;

            // we want to play so I guess want the output to be heard, on iOS too:
            // Since 2017.1 there is a setting 'Force iOS Speakers when Recording' for this workaround needed in previous versions
#if !UNITY_2017_1_OR_NEWER
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                TimeLog.LOG(LogLevel.INFO, LogLevel.INFO, this.gameObject.name, null, "Setting playback output to speaker...");
                iOSSpeaker.RouteForPlayback();
            }
#endif
        }

        protected virtual void Start()
        {

        }

        protected virtual void Update()
        {
            this.@as.volume = this.volume;

#if !UNITY_WSA
            if (this.decodeThread != null)
                this.decodeThread.Priority = this.decoderThreadPriority;
#endif
        }

        BasicBufferFloat outputAudioSamples = new BasicBufferFloat(100000);
        public bool capturedAudioFrame { get; private set; }
        public int capturedAudioSamples { get { return this.outputAudioSamples.Available(); } }
        public int dspBufferSize { get; private set; }

#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void PCMReaderCallback(float[] data)
        {
            var dlength = data.Length;
            Array.Clear(data, 0, dlength);

            if (this.outputAudioSamples.Available() >= dlength)
            {
                var floats = this.outputAudioSamples.Read(dlength);
                Array.Copy(floats, data, floats.Length);
                this.capturedAudioFrame = true;
            }
            else
            {
                // not enough frames arrived
                this.capturedAudioFrame = false;
            }
        }

        protected virtual void OnDestroy()
        {
            this.StopDecoder();
        }

        #endregion

        // ========================================================================================================================================
        #region Support
        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        protected string gameObjectName = string.Empty;
        protected void LOG(LogLevel requestedLogLevel, string format, params object[] args)
        {
            Log.LOG(requestedLogLevel, this.logLevel, this.gameObjectName, format, args);
        }

        #endregion
    }
}