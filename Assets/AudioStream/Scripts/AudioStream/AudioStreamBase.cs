// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using FMOD;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

namespace AudioStream
{
    /// <summary>
    /// Abstract base with download handler, file system for FMOD and playback controls
    /// </summary>
    public abstract partial class AudioStreamBase : MonoBehaviour
    {
        // ========================================================================================================================================
        #region UnityWebRequest
        #region Custom certificate handler
#if UNITY_2018_1_OR_NEWER
        /// <summary>
        /// https://docs.unity3d.com/ScriptReference/Networking.CertificateHandler.html
        /// Note: Custom certificate validation is currently only implemented for the following platforms - Android, iOS, tvOS and desktop platforms.
        /// </summary>
        class NoCheckPolicyCertificateHandler : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData)
            {
                // Allow all certificates to pass..
                return true;

                /*
                 * optional key check:
                 */

                // var certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(certificateData);
                // var pubk = certificate.GetPublicKeyString();
                // Debug.LogFormat("Certificate public key: {0}", pubk);

                // if (pk.ToLowerInvariant().Equals(PUBLIC_KEY.ToLower())) ..
                // ;
            }
        }
#endif
        #endregion
        #region Custom download handler
        /// <summary>
        /// Custom download handler which writes to injected FMOD filesystem
        /// </summary>
        class ByteStreamDownloadHandler : DownloadHandlerScript
        {
            /// <summary>
            /// Content-Lenght returned in response header should be the lenght of the body/content, but it's not always the case, see ReceiveContentLengthHeader 
            /// </summary>
            public uint contentLength = INFINITE_LENGTH;
            public uint downloaded;
            public bool downloadComplete = false;
            /// <summary>
            /// injected the whole class mainly for logging
            /// </summary>
            AudioStreamBase audioStream;
            /// <summary>
            /// Pre-allocated scripted download handler - should eliminate memory allocations
            /// </summary>
            /// <param name="downloadHandlerBuffer"></param>
            /// <param name="audioStreamWithFileSystem"></param>
            public ByteStreamDownloadHandler(byte[] downloadHandlerBuffer, AudioStreamBase audioStreamWithFileSystem)
                : base(downloadHandlerBuffer)
            {
                this.contentLength = INFINITE_LENGTH;
                this.downloaded = 0;
                this.downloadComplete = false;

                this.audioStream = audioStreamWithFileSystem;
            }
            /// <summary>
            /// Required by DownloadHandler base class.
            /// Not used - the data is being written directly into injected audio buffer
            /// </summary>
            /// <returns></returns>
            protected override byte[] GetData()
            {
                return null;
            }
            /// <summary>
            /// Called once per frame when data has been received from the network.
            /// </summary>
            /// <param name="data"></param>
            /// <param name="dataLength"></param>
            /// <returns></returns>
            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || data.Length < 1 || dataLength < 1)
                {
                    this.downloadComplete = true;
                    return false;
                }

                // take just given length
                var newData = new byte[dataLength];
                Array.Copy(data, 0, newData, 0, dataLength);

                // Debug.LogFormat("ReceiveData/Writing: {0}", dataLength);

                // write incoming buffer
                this.audioStream.mediaBuffer.Write(newData);
                this.downloaded += (uint)dataLength;

                return true;
            }
            string lastLogMessage; // log spam prevention
            /// <summary>
            /// Called when all data has been received from the server and delivered via ReceiveData.
            /// </summary>
            protected override void CompleteContent()
            {
                this.downloadComplete = true;
            }
            /// <summary>
            /// Called when a Content-Length header is received from the server.
            /// ... can be sent more than once but it looks like whatever came last is good enough question mark
            /// </summary>
            /// <param name="_contentLength"></param>
#if UNITY_2019_1_OR_NEWER
            protected override void ReceiveContentLengthHeader(ulong _contentLength)
            {
                base.ReceiveContentLengthHeader(_contentLength);

                this.audioStream.LOG(LogLevel.DEBUG, "Received Content length: {0}", _contentLength);

                // Content-Lenght should be body/media lenght, but some servers return (probably misconfigured) value - such as http://stream.antenne.de:80/antenne -> _contentLength == 60 bytes
                // since this is values used for stream/file lenght FMOD - correctly - immediately stops - detect this for 'low' values and don't use it

                if (_contentLength < 1024)
                {
                    var msg = string.Format("Will ignore received Content length: {0} / modify handler here if needed", _contentLength);
                    if (this.lastLogMessage != msg)
                    {
                        this.lastLogMessage = msg;
                        this.audioStream.LOG(LogLevel.WARNING, msg);
                    }
                }
                else
                    this.contentLength = (uint)_contentLength;
            }
#else
            protected override void ReceiveContentLength(int _contentLength)
            {
                base.ReceiveContentLength(_contentLength);

                this.audioStream.LOG(LogLevel.DEBUG, "Received Content length: {0}", _contentLength);

                // Content-Lenght should be body/media lenght, but some servers return (probably misconfigured) value - such as http://stream.antenne.de:80/antenne -> _contentLength == 61 bytes
                // since this is values used for stream/file lenght FMOD - correctly - immediately stops - detect this for 'low' values and don't use it

                if (_contentLength < 1024)
                {
                    var msg = string.Format("Will ignore received Content length: {0} / modify handler here if needed", _contentLength);
                    if (this.lastLogMessage != msg)
                    {
                        this.lastLogMessage = msg;
                        this.audioStream.LOG(LogLevel.WARNING, msg);
                    }
                }
                else
                    this.contentLength = (uint)_contentLength;
            }
#endif
        }
        #endregion
        /*
         * FMOD file system
         */
        public DownloadFileSystemBase mediaBuffer { get; protected set; }
        /*
         * network streams
         */
        ByteStreamDownloadHandler downloadHandler = null;
        UnityWebRequest webRequest = null;
        /// <summary>
        /// Custom request headers
        /// </summary>
        readonly Dictionary<string, string> webRequestCustomHeaders = new Dictionary<string, string>();
        public void SetCustomRequestHeader(string key, string value)
        {
            if (this.webRequestCustomHeaders.ContainsKey(key))
                this.webRequestCustomHeaders[key] = value;
            else
                this.webRequestCustomHeaders.Add(key, value);
        }
        public void ClearCustomRequestHeaders()
        {
            this.webRequestCustomHeaders.Clear();
        }
        #endregion

        // ========================================================================================================================================
        #region FMOD filesystem callbacks
        readonly static object fmod_callback_lock = new object();
        /*
            File callbacks
            due to IL2CPP not supporting marshaling delegates that point to instance methods to native code we need to circumvent this via static dispatch
            (similarly as for exinfo for output device )
        */
        [AOT.MonoPInvokeCallback(typeof(FMOD.FILE_OPEN_CALLBACK))]
        static RESULT Media_Open(IntPtr name, ref uint filesize, ref IntPtr handle, IntPtr userdata)
        {
            lock (AudioStreamBase.fmod_callback_lock)
            {
                if (userdata == IntPtr.Zero)
                {
                    UnityEngine.Debug.LogWarning("Media_Open userdata == 0");
                    return RESULT.ERR_FILE_EOF;
                }

                GCHandle objecthandle = GCHandle.FromIntPtr(userdata);
                var audioStream = (objecthandle.Target as AudioStreamBase);

                // callback was universal but is for network streams only now 
                if (audioStream.mediaType != MEDIATYPE.NETWORK)
                {
                    audioStream.ERRCHECK(RESULT.ERR_INVALID_PARAM, "Custom filesystem works with network streams only", false);
                    return RESULT.ERR_INVALID_PARAM;
                }

                // will 4GB stream be always enough, question mark
                filesize = audioStream.downloadHandler.contentLength;

                audioStream.mediaLength = filesize;
                audioStream.mediaAvailable = 0;

                handle = IntPtr.Zero;

                audioStream.LOG(LogLevel.DEBUG, "--------------- media_open ---------------");

                return FMOD.RESULT.OK;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(FMOD.FILE_CLOSE_CALLBACK))]
        static FMOD.RESULT Media_Close(IntPtr handle, IntPtr userdata)
        {
            lock (AudioStreamBase.fmod_callback_lock)
            {
                if (userdata == IntPtr.Zero)
                {
                    UnityEngine.Debug.LogWarningFormat("Media_Close userdata == 0");
                    return RESULT.OK;
                }

                GCHandle objecthandle = GCHandle.FromIntPtr(userdata);
                var audioStream = (objecthandle.Target as AudioStreamBase);

                audioStream.LOG(LogLevel.DEBUG, "--------------- media_close ---------------");

                return FMOD.RESULT.OK;
            }
        }
        /// <summary>
        /// See below
        /// </summary>
        FMOD.RESULT media_read_lastResult = FMOD.RESULT.OK;
        /*
         * async version of the API
        */
        /// <summary>
        /// flag for Marshal.StructureToPtr 
        /// https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.structuretoptr?view=netframework-4.8
        /// If you use the StructureToPtr<T>(T, IntPtr, Boolean) method to copy a different instance to the memory block at a later time, specify true for fDeleteOld to remove reference counts for reference types in the previous instance. Otherwise, the managed reference types and unmanaged copies are effectively leaked.
        /// </summary>
        // bool fDeleteOld = false;
        /// <summary>
        /// Retrieves any encoded data based on info and immediately satisfies read request
        /// Sets 'media_read_lastResult' for main thread to detect media data shortage
        /// <param name="infoptr"></param>
        /// <param name="userdata"></param>
        /// <returns>FMOD.RESULT.OK if all requested bytes were read</returns>
        [AOT.MonoPInvokeCallback(typeof(FMOD.FILE_ASYNCREAD_CALLBACK))]
        static FMOD.RESULT Media_AsyncRead(IntPtr infoptr, IntPtr userdata)
        {
            lock (AudioStreamBase.fmod_callback_lock)
            {
                var info = (FMOD.ASYNCREADINFO)Marshal.PtrToStructure(infoptr, typeof(FMOD.ASYNCREADINFO));

                if (userdata == IntPtr.Zero)
                {
                    UnityEngine.Debug.LogWarningFormat("Media_AsyncRead userdata == 0");
                    info.done(infoptr, RESULT.ERR_INVALID_HANDLE);
                    return RESULT.ERR_INVALID_HANDLE;
                }

                GCHandle objecthandle = GCHandle.FromIntPtr(userdata);
                var audioStream = (objecthandle.Target as AudioStreamBase);


                if (infoptr == IntPtr.Zero)
                {
                    audioStream.media_read_lastResult = FMOD.RESULT.ERR_FILE_EOF;
                    info.done(infoptr, audioStream.media_read_lastResult);
                    return audioStream.media_read_lastResult;
                }

                // media type is verified while opening

                var downloadBytes = audioStream.mediaBuffer.Read(info.offset, info.sizebytes);
                Marshal.Copy(downloadBytes, 0, info.buffer, downloadBytes.Length);
                info.bytesread = (uint)downloadBytes.Length;

                if (info.bytesread < info.sizebytes)
                {
                    audioStream.LOG(LogLevel.DEBUG, "FED     {0}/{1} bytes, offset {2} (* EOF)", info.bytesread, info.sizebytes, info.offset);
                    audioStream.media_read_lastResult = FMOD.RESULT.ERR_FILE_EOF;
                }
                else
                {
                    audioStream.LOG(LogLevel.DEBUG,"FED     {0}/{1} bytes, offset {2}", info.bytesread, info.sizebytes, info.offset);
                    audioStream.media_read_lastResult = FMOD.RESULT.OK;
                }

                // update the unmanaged data (here esp. bytesread)
                Marshal.StructureToPtr(info, infoptr, false);

                info.done(infoptr, audioStream.media_read_lastResult);

                return audioStream.media_read_lastResult;
            }
        }
        [AOT.MonoPInvokeCallback(typeof(FMOD.FILE_ASYNCCANCEL_CALLBACK))]
        static FMOD.RESULT Media_AsyncCancel(IntPtr infoptr, IntPtr userdata)
        {
            lock (AudioStreamBase.fmod_callback_lock)
            {
                var info = (FMOD.ASYNCREADINFO)Marshal.PtrToStructure(infoptr, typeof(FMOD.ASYNCREADINFO));

                if (userdata == IntPtr.Zero)
                {
                    UnityEngine.Debug.LogWarningFormat("Media_AsyncCancel userdata == 0");
                    info.done(infoptr, FMOD.RESULT.ERR_FILE_DISKEJECTED);
                    return RESULT.ERR_FILE_DISKEJECTED;
                }

                GCHandle objecthandle = GCHandle.FromIntPtr(userdata);
                var audioStream = (objecthandle.Target as AudioStreamBase);

                audioStream.LOG(LogLevel.DEBUG, "--------------- media_asynccancel --------------- {0}", infoptr);

                if (infoptr == IntPtr.Zero)
                {
                    info.done(infoptr, FMOD.RESULT.ERR_FILE_DISKEJECTED);
                    return FMOD.RESULT.ERR_FILE_DISKEJECTED;
                }

                // Signal FMOD to wake up, this operation has been cancelled

                // Debug.LogFormat("CANCEL {0} bytes, offset {1} PRIORITY = {2}.", info_deptr.sizebytes, info_deptr.offset, info_deptr.priority);

                info.done(infoptr, FMOD.RESULT.ERR_FILE_DISKEJECTED);
                return FMOD.RESULT.ERR_FILE_DISKEJECTED;
            }
        }
        FMOD.FILE_OPEN_CALLBACK fileOpenCallback;
        FMOD.FILE_CLOSE_CALLBACK fileCloseCallback;
        // async version of the API
        FMOD.FILE_ASYNCREAD_CALLBACK fileAsyncReadCallback;
        FMOD.FILE_ASYNCCANCEL_CALLBACK fileAsyncCancelCallback;
        #endregion

        // ========================================================================================================================================
        #region Required descendant's implementation
        /// <summary>
        /// Called immediately after a valid stream has been established
        /// </summary>
        /// <param name="samplerate"></param>
        /// <param name="channels"></param>
        /// <param name="sound_format"></param>
        protected abstract void StreamStarting();
        /// <summary>
        /// Called per frame to determine runtime status of the playback
        /// Channel state for AudioStreamMinimal, channel state + later PCM callback for AudioStream
        /// </summary>
        /// <returns></returns>
        protected abstract void StreamStarving();
        /// <summary>
        /// Called immediately before stopping and releasing the sound
        /// </summary>
        protected abstract void StreamStopping();
        /// <summary>
        /// Called when mid-stream samplerate changed
        /// (very rare, not tested properly)
        /// </summary>
        /// <param name="samplerate"></param>
        /// <param name="channels"></param>
        /// <param name="sound_format"></param>
        protected abstract void StreamChanged(float samplerate, int channels, FMOD.SOUND_FORMAT sound_format);
        /// <summary>
        /// Return time of the file being played, or -1 (undefined) for streams
        /// </summary>
        /// <returns></returns>
        // TODO: public abstract double MediaTimeSeconds();
        #endregion

        // ========================================================================================================================================
        #region Editor
        /// <summary>
        /// Slightly altered FMOD.SOUND_TYPE for convenience
        /// replaced name of UNKNOWN for AUTODETECT, and omitted the last (MAX) value
        /// 'PLAYLIST' and 'USER' are included to keep easy type correspondence, but are not implemented - if used an exception is thrown when attempting to play
        /// fmod.cs cca 2.01.xx {'21}
        /// </summary>
        public enum StreamAudioType
        {
            AUTODETECT,      /* let FMOD guess the stream format */

            AIFF,            /* AIFF. */
            ASF,             /* Microsoft Advanced Systems Format (ie WMA/ASF/WMV). */
            DLS,             /* Sound font / downloadable sound bank. */
            FLAC,            /* FLAC lossless codec. */
            FSB,             /* FMOD Sample Bank. */
            IT,              /* Impulse Tracker. */
            MIDI,            /* MIDI. extracodecdata is a pointer to an FMOD_MIDI_EXTRACODECDATA structure. */
            MOD,             /* Protracker / Fasttracker MOD. */
            MPEG,            /* MP2/MP3 MPEG. */
            OGGVORBIS,       /* Ogg vorbis. */
            PLAYLIST,        /* Information only from ASX/PLS/M3U/WAX playlists */
            RAW,             /* Raw PCM data. */
            S3M,             /* ScreamTracker 3. */
            USER,            /* User created sound. */
            WAV,             /* Microsoft WAV. */
            XM,              /* FastTracker 2 XM. */
            XMA,             /* Xbox360 XMA */
            AUDIOQUEUE,      /* iPhone hardware decoder, supports AAC, ALAC and MP3. extracodecdata is a pointer to an FMOD_AUDIOQUEUE_EXTRACODECDATA structure. */
            AT9,             /* PS4 / PSVita ATRAC 9 format */
            VORBIS,          /* Vorbis */
            MEDIA_FOUNDATION,/* Windows Store Application built in system codecs */
            MEDIACODEC,      /* Android MediaCodec */
            FADPCM,          /* FMOD Adaptive Differential Pulse Code Modulation */
            OPUS
        }

        [Header("[Source]")]

        [Tooltip("Audio stream - such as shoutcast/icecast - direct URL or m3u/8/pls playlist URL,\r\nor direct URL link to a single audio file.\r\n\r\nNOTE: it is possible to stream a local file. Pass complete file path with or without the 'file://' prefix in that case.")]
        public string url = string.Empty;
        /// <summary>
        /// carrying over url rewrite/s between methods
        /// might be e.g. cached file or url extracted from a playlist
        /// </summary>
        protected string url_from_cache_or_updated = string.Empty;

        [Tooltip("Audio format of the stream\r\n\r\nAutodetect lets FMOD autodetect the stream format and is default and recommended for desktop and Android platforms.\r\n\r\nFor iOS please select correct type - autodetecting there most often does not work.\r\n\r\nBe aware that if you select incorrect format for a given radio/stream you will risk problems such as unability to connect and stop stream.\r\n\r\nFor RAW audio format user must specify at least frequency, no. of channles and byte format.")]
        public StreamAudioType streamType = StreamAudioType.AUTODETECT;

        [Header("[RAW codec parameters]")]
        public FMOD.SOUND_FORMAT RAWSoundFormat = FMOD.SOUND_FORMAT.PCM16;
        public int RAWFrequency = 44100;
        public int RAWChannels = 2;

        [Header("[Setup]")]
        [Tooltip("When checked the stream will play at start. Otherwise use Play() method of this instance.")]
        public bool playOnStart = false;

        [Tooltip("If ON the component will attempt to stream continuosly regardless of any error/s that may occur. This is done automatically by restarting 'Play' method when needed (an error/end of file occurs)\r\nRecommended for streams.\r\nNote: if used with finite sized files the streaming of the file will restart from beginning even when reaching the end, too. You might want to turn this OFF for finite sized files, and check state via OnPlaybackStopped event.\r\n\r\nFlag is ignored when needed, e.g. for AudioStreamMemory component")]
        public bool continuosStreaming = false;

        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;

        [Header("[Setup (download cache)]")]
        [Tooltip("If there's previously downloaded media for specified Url in local disk cache it will be played instead of (live) Url stream")]
        public bool playFromCache = false;
        [Tooltip("Save media from Url into local disk cache as it's being streamed.\r\nThis will overwrite any previously existing content for a given Url")]
        public bool downloadToCache = false;

        #region Unity events
        [Header("[Events]")]
        public EventWithStringParameter OnPlaybackStarted;
        public EventWithStringBoolParameter OnPlaybackPaused;
        public EventWithStringStringParameter OnPlaybackStopped;
        public EventWithStringStringObjectParameter OnTagChanged;
        public EventWithStringStringParameter OnError;
        #endregion

        [Header("[Advanced]")]
        [Tooltip("Directly affects decoding, and the size of the initial download until playback is started.\r\n" +
            "Do not change this unless you have problems opening certain files/streams, or you want to speed up initial startup of playback on slower networks.\r\n" +
            "You can try 2-4 kB to capture a file/stream quickly, though you might risk its format might not be recognized correctly in that case.\r\n" +
            "Generally increasing this to some bigger value of few tens kB should help when having trouble opening the stream - this often occurs with e.g. mp3s containing tags with embedded artwork, or when working with different audio formats\r\n\r\n" +
            "Important: Currently Ogg/Vorbis format requires this to be set to the size of the whole file - if that is known/possible - in order to play it (the whole Vorbis file has to be downloaded first)\r\n\r\n" +
            "(technically, it's 'blockalign' parameter of FMOD setFileSystem call: https://fmod.com/resources/documentation-api?version=2.0&page=core-api-system.html#system_setfilesystem)"
            )]
        public uint blockalign = 16 * 1024; // 'blockalign' setFileSystem parameter - default works for most formats. FMOD default is 2048
        [Tooltip("This works in conjuction with previous parameter - several 'blockalign' sized chunks have to be downloaded in order for decoder to properly recognize format, parse tags/artwork and start playback.\r\n" +
            ": total initial download before the playback is started is (blockalign * blockalignDownloadMultiplier) bytes\r\n" +
            "Currently Ogg/Vorbis format needs the whole file to be downloaded first: please set blockalign to file size (if it is/can be known) and this paramter to 1 for Ogg/Vorbis format.\r\n" +
            "\r\n" +
            "Applies only for playback from network - local files are accessed directly, and use 'blockalign' parameter only.")]
        [Range(1, 16)]
        public uint blockalignDownloadMultiplier = 2;
        [Tooltip("Frame count after which the playback is stopped when the network is starving continuosly.\r\nDefault is 60 which for 60 fps means ~ 1 sec.\r\n(only applies to network streams)")]
        public int starvingRetryCount = 60;
        [Tooltip("Recommended to turn off if not needed/handled (e.g. for AudioStreamMemory/AudioStreamRuntimeImport)\r\nDefault on")]
        public bool readTags = true;
        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        protected string gameObjectName = string.Empty;
        #endregion

        // ========================================================================================================================================
        #region Init && FMOD structures
        /// <summary>
        /// Set once FMOD had been fully initialized
        /// You should always check this flag before starting the playback
        /// </summary>
        public bool ready { get; protected set; }
        [HideInInspector]
        public string fmodVersion;

        protected FMOD_SystemW.FMOD_System fmodsystem = null;
        protected FMOD.Sound sound;
        protected FMOD.Channel channel;
        float channelIsAudibleTime;
        public FMOD.RESULT result { get; protected set; }
        protected FMOD.OPENSTATE openstate = FMOD.OPENSTATE.MAX;

        FMOD.RESULT lastError = FMOD.RESULT.OK;
        /// <summary>
        /// main thread id - for invoking action (texture creation) on the main thread
        /// </summary>
        int mainThreadId;
        /// <summary>
        /// Type of source media - async/read callbacks and processing will differ based on that
        /// </summary>
        public enum MEDIATYPE
        {
            NETWORK
                , FILESYSTEM
                , MEMORY
        }
        /// <summary>
        /// Type of source media - async/read callbacks and processing will differ based on that
        /// </summary>
        MEDIATYPE mediaType;
        /// <summary>
        /// handle to this' ptr for usedata in callbacks
        /// </summary>
        GCHandle gc_thisPtr;
        protected virtual IEnumerator Start()
        {
            this.gc_thisPtr = GCHandle.Alloc(this);

            this.mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

            // FMODSystemsManager.InitializeFMODDiagnostics(FMOD.DEBUG_FLAGS.LOG);

            this.gameObjectName = this.gameObject.name;

#if AUDIOSTREAM_IOS_DEVICES
            // AllowBluetooth is only valid with AVAudioSessionCategoryRecord and AVAudioSessionCategoryPlayAndRecord
            // DefaultToSpeaker is only valid with AVAudioSessionCategoryPlayAndRecord
            // So user has 'Prepare iOS for recording' and explicitely enable it here
            AVAudioSessionWrapper.UpdateAVAudioSession(false, true);

            // be sure to wait until session change notification
            while (!AVAudioSessionWrapper.IsSessionReady())
                yield return null;
#endif
            /*
             * Create and init required FMOD system
             */
            if (this is AudioStreamMemory
                || this is AudioStreamRuntimeImport
                )
                this.fmodsystem = FMOD_SystemW.FMOD_System_NRT_Create(this.logLevel, this.gameObjectName, this.OnError);
            else
            {
                uint dspBufferLength, dspBufferCount;
                this.fmodsystem = FMOD_SystemW.FMOD_System_Create(0, true, this.logLevel, this.gameObjectName, this.OnError, out dspBufferLength, out dspBufferCount);
            }

            this.fmodVersion = this.fmodsystem.VersionString;


            int rate;
            FMOD.SPEAKERMODE sm;
            int smch;
            result = fmodsystem.system.getSoftwareFormat(out rate, out sm, out smch);
            ERRCHECK(result, "fmodsystem.system.getSoftwareFormat");

            LOG(LogLevel.INFO, "FMOD samplerate: {0}, speaker mode: {1}, num. of raw speakers: {2}", rate, sm, smch);

            // wait for FMDO to catch up to be safe if requested to play immediately [i.e. autoStart]
            // (technically we probably don't need ouput for the memory component...)
            int numDrivers;
            int retries = 0;

            do
            {
                result = fmodsystem.system.getNumDrivers(out numDrivers);
                ERRCHECK(result, "fmodsystem.system.getNumDrivers");

                LOG(LogLevel.INFO, "Got {0} driver/s available", numDrivers);

                if (++retries > 500)
                {
                    var msg = string.Format("There seems to be no audio output device connected");

                    ERRCHECK(FMOD.RESULT.ERR_OUTPUT_NODRIVERS, msg, false);

                    yield break;
                }

                yield return null;

            } while (numDrivers < 1);

            // make a capture DSP when needed
            if (this is AudioStream
                || this is AudioStreamRuntimeImport
                || this is AudioStreamMemory)
            {
                // prepare the DSP
                this.dsp_ReadCallback = new DSP_READ_CALLBACK(AudioStreamBase.DSP_READCALLBACK);

                DSP_DESCRIPTION dspdesc = new DSP_DESCRIPTION();

                dspdesc.pluginsdkversion = FMOD.VERSION.number;
                // TODO: IL2CPP ArgumentException: Type could not be marshaled because the length of an embedded array instance does not match the declared length in the layout.
                // dspdesc.name = "_-===-_ capture".ToCharArray().Select(s => (byte)s).ToArray();
                dspdesc.version = 0x00010000;
                dspdesc.numinputbuffers = 1;
                dspdesc.numoutputbuffers = 1;
                dspdesc.read = this.dsp_ReadCallback;
                dspdesc.userdata = GCHandle.ToIntPtr(this.gc_thisPtr);

                result = fmodsystem.system.createDSP(ref dspdesc, out this.captureDSP);
                ERRCHECK(result, "fmodsystem.system.createDSP");
            }

            this.ready = true;

            if (this.playOnStart)
                this.Play();
        }

        #endregion

        // ========================================================================================================================================
        #region Playback
        /// <summary>
        /// undefined length indicator
        /// </summary>
        public const uint INFINITE_LENGTH = uint.MaxValue;
        /// <summary>
        /// Detected media size [bytes] (uint max value indicates undefined/infinite stream)
        /// </summary>
        public uint mediaLength
        {
            get { return this._mediaLength; }
            protected set { this._mediaLength = value; }
        }
        // be nice to default UI:
        private uint _mediaLength = AudioStreamBase.INFINITE_LENGTH;
        /// <summary>
        /// Downloaded overall [bytes]
        /// </summary>
        public uint mediaDownloaded
        {
            get
            {
                return this.downloadHandler != null ? this.downloadHandler.downloaded : this._mediaDownloaded;
            }

            protected set { this._mediaDownloaded = value; }
        }
        private uint _mediaDownloaded;
        /// <summary>
        /// Availabile for playback [bytes]
        /// </summary>
        public uint mediaAvailable
        {
            get
            {
                return this.mediaBuffer != null ? this.mediaBuffer.available : this._mediaAvailable;
            }

            protected set { this._mediaAvailable = value; }
        }
        private uint _mediaAvailable;
        public uint mediaCapacity
        {
            get
            {
                return this.mediaBuffer != null ? this.mediaBuffer.capacity : 0;
            }
        }
        /// <summary>
        /// Infinite stream / finite file indicator
        /// </summary>
        public bool MediaIsInfinite
        {
            get
            {
                return this.mediaLength == INFINITE_LENGTH;
            }
        }
        /// <summary>
        /// bufferFillPercentage has nothing to do with network any more
        /// </summary>
        // [Range(0f, 100f)]
        // [Tooltip("Set during playback. Playback buffer fullness")]
        // public
        uint bufferFillPercentage = 0;
        /// <summary>
        /// User pressed Play (UX updates)
        /// </summary>
        bool isPlayingUser;
        public bool isPlaying
        {
            get
            {
                return this.isPlayingChannel || this.isPlayingUser;
            }
        }
        /// <summary>
        /// Channel playing during playback
        /// </summary>
        bool isPlayingChannel
        {
            get
            {
                bool channelPlaying = false;

                if (this.channel.hasHandle())
                {
                    result = channel.isPlaying(out channelPlaying);
                    // ERRCHECK(result, "channel.isPlaying", false); // - will ERR_INVALID_HANDLE on finished channel -
                }

                return channelPlaying && result == FMOD.RESULT.OK;
            }
        }
        /// <summary>
        /// Channel paused during playback
        /// </summary>
        public bool isPaused
        {
            get
            {
                bool channelPaused = false;
                if (channel.hasHandle())
                {
                    result = this.channel.getPaused(out channelPaused);
                    // ERRCHECK(result, "channel.getPaused", false); // - will ERR_INVALID_HANDLE on finished channel -
                }

                return channelPaused && result == FMOD.RESULT.OK;
            }

            set
            {
                if (channel.hasHandle())
                {
                    result = this.channel.setPaused(value);
                    ERRCHECK(result, "channel.setPaused", false);
                }
            }
        }
        /// <summary>
        /// starving flag is now meaningless - FMOD doesn't seem to be updating it correctly despite FS returning nothing
        /// </summary>
        // [Tooltip("Set during playback.")]
        // public
        protected bool starving = false;
        /// <summary>
        /// Refreshing from media buffer is purely cosmetic
        /// </summary>
        // [Tooltip("Set during playback when stream is refreshing data.")]
        // public
        bool deviceBusy = false;
        [Header("[Playback info]")]
        [Tooltip("Radio station title. Set from PLS playlist.")]
        [ReadOnly] public string title;
        [Tooltip("Retrieved stream properties.")]
        [ReadOnly] public FMOD.SOUND_TYPE runtimeSoundType;
        [ReadOnly] public FMOD.SOUND_FORMAT streamFormat;
        [ReadOnly] public int streamChannels;
        [ReadOnly] public int streamBits;
        [ReadOnly] public byte streamBytesPerSample;
        [ReadOnly] public int streamSampleRate;
        //: - [Tooltip("Tags supplied by the stream. Varies heavily from stream to stream")]
        Dictionary<string, object> tags = new Dictionary<string, object>();
        public void Play()
        {
            if (this.streamType == StreamAudioType.USER
                || this.streamType == StreamAudioType.PLAYLIST)
            {
                ERRCHECK(FMOD.RESULT.ERR_FORMAT, string.Format("{0} stream type is currently not supported. Please use other than {1} and {2} stream type.", this.streamType, StreamAudioType.USER, StreamAudioType.PLAYLIST), false);
                return;
            }

            if (this.isPlaying)
            {
                LOG(LogLevel.WARNING, "Already playing.");
                return;
            }

            if (!this.isActiveAndEnabled)
            {
                LOG(LogLevel.ERROR, "Will not start on disabled GameObject.");
                return;
            }

            /*
             * Check basic format and playback from cache
             */
            this.url_from_cache_or_updated = string.Empty;
            if (!(this is AudioStreamMemory))
            {
                /*
                 * + url format check
                 */
                if (string.IsNullOrEmpty(this.url))
                {
                    var msg = "Can't stream empty URL";
                    ERRCHECK(FMOD.RESULT.ERR_FILE_NOTFOUND, msg, false);
                    return;
                }

                if (this.url.ToLowerInvariant().EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase)
                    && (this.streamType != StreamAudioType.OPUS && this.streamType != StreamAudioType.OGGVORBIS && this.streamType != StreamAudioType.AUTODETECT)
                    )
                {
                    // OPUS might be in OGG containter - though as of 082021 this still doesn't work..
                    var msg = "It looks like you're trying to play OGGVORBIS stream, but have not selected proper 'Stream Type'. This might result in various problems while playing and stopping unsuccessful connection with this setup.";

                    ERRCHECK(FMOD.RESULT.ERR_FORMAT, msg, false);

                    return;
                }

                this.tags = new Dictionary<string, object>();

                if (this.playFromCache)
                {
                    // if cache media exists play that instead
                    // the extension/audio format might not yet be known at this point
                    var fname = FileSystem.DownloadCacheFilePath(this.url, "");
                    if (File.Exists(fname))
                        this.url_from_cache_or_updated = fname;
                }

                // check for early exit if download is not to be overwritten
                if (this is AudioStreamRuntimeImport)
                {
                    var asimp = this as AudioStreamRuntimeImport;
                    var filepath = FileSystem.TempFilePath(this.url, (this as AudioStreamRuntimeImport).uniqueCacheId, ".raw");

                    if (!asimp.overwriteCachedDownload
                        && System.IO.File.Exists(filepath)
                        )
                    {
                        LOG(LogLevel.INFO, "Playing from cache: {0}", filepath);

                        // pair start / stop event to indicate start and stop of download/construction of AudioClip

                        if (this.OnPlaybackStarted != null)
                            this.OnPlaybackStarted.Invoke(this.gameObjectName);

                        // create clip from cache
                        asimp.StopDownloadAndCreateAudioClip(true);

                        // and early stop
                        this.StopWithReason(ESTOP_REASON.User);

                        return;
                    }
                }
            }
            else
            {
                // parameters check for AudioStreamMemory
                var asm = this as AudioStreamMemory;
                if (asm.memoryLocation == System.IntPtr.Zero)
                {
                    ERRCHECK(FMOD.RESULT.ERR_INVALID_PARAM, "Set memory location before calling Play", false);
                    return;
                }

                if (asm.memoryLength < 1) // -)
                {
                    ERRCHECK(FMOD.RESULT.ERR_INVALID_PARAM, "Set memory length before calling Play", false);
                    return;
                }

                // retrieve cache is requested and stop immediately
                if (!string.IsNullOrEmpty(asm.cacheIdentifier))
                {
                    var filepath = FileSystem.TempFilePath(asm.cacheIdentifier, "", ".raw");

                    if (System.IO.File.Exists(filepath))
                    {
                        LOG(LogLevel.INFO, "Playing from cache: {0}", filepath);

                        // pair start / stop event to indicate start and stop of download/construction of AudioClip

                        if (this.OnPlaybackStarted != null)
                            this.OnPlaybackStarted.Invoke(this.gameObjectName);

                        // create clip from cache
                        asm.StopDecodingAndCreateAudioClip(true);

                        // and early stop
                        this.StopWithReason(ESTOP_REASON.User);

                        return;
                    }
                }
            }

            StartCoroutine(this.PlayCR());
        }

        enum PlaylistType
        {
            PLS
                , M3U
                , M3U8
        }

        PlaylistType? playlistType;

        IEnumerator PlayCR()
        {
            // yield a frame for potential Stop explicit call to finish
            yield return null;

            this.isPlayingUser = true;

            // don't resolve playlist if playing local disk cache - since fname there might have original playlist extension..
            var playing_online = string.IsNullOrEmpty(this.url_from_cache_or_updated);

            // if not cche assume original url
            if (playing_online)
                this.url_from_cache_or_updated = this.url;

            // determine the type of the source media
            if (this is AudioStreamMemory)
                this.mediaType = MEDIATYPE.MEMORY;
            else if (this.url_from_cache_or_updated.ToLowerInvariant().StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
                this.mediaType = MEDIATYPE.NETWORK;
            else
                this.mediaType = MEDIATYPE.FILESYSTEM;

            // cached files can't be playlists
            // (we would also have to add an actual extension which is determined only after starting the playback)

            // retrieve playlist / entry
            if (this.mediaType != MEDIATYPE.MEMORY
                && playing_online
                )
            {
                this.playlistType = null;

                // playlist takes precedence..
                //if (this.streamType == StreamAudioType.AUTODETECT
                //    || this.streamType == StreamAudioType.PLAYLIST)
                {
                    if (this.url_from_cache_or_updated.ToLowerInvariant().EndsWith("pls", System.StringComparison.OrdinalIgnoreCase))
                        this.playlistType = PlaylistType.PLS;
                    else if (this.url_from_cache_or_updated.ToLowerInvariant().EndsWith("m3u", System.StringComparison.OrdinalIgnoreCase))
                        this.playlistType = PlaylistType.M3U;
                    else if (this.url_from_cache_or_updated.ToLowerInvariant().EndsWith("m3u8", System.StringComparison.OrdinalIgnoreCase))
                        this.playlistType = PlaylistType.M3U8;
                }

                // TODO:
                // Allow to explicitely set that the link is a playlist
                // e.g. http://yp.shoutcast.com/sbin/tunein-station.pls?id=1593461

                if (this.playlistType.HasValue)
                {
                    string playlist = string.Empty;

                    // allow local playlist
                    if (!this.url_from_cache_or_updated.ToLowerInvariant().StartsWith("http", System.StringComparison.OrdinalIgnoreCase) && !this.url_from_cache_or_updated.ToLower().StartsWith("file", System.StringComparison.OrdinalIgnoreCase))
                        this.url_from_cache_or_updated = "file://" + this.url_from_cache_or_updated;

                    //
                    // UnityWebRequest introduced in 5.2, but WWW still worked on standalone/mobile
                    // However, in 5.3 is WWW hardcoded to Abort() on iOS on non secure requests - which is likely a bug - so from 5.3 on we require UnityWebRequest
                    //
#if UNITY_5_3_OR_NEWER
#if UNITY_5_3
                    using (UnityEngine.Experimental.Networking.UnityWebRequest www = UnityEngine.Experimental.Networking.UnityWebRequest.Get(this.url_from_cache_or_updated))
#else
                    using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(this.url_from_cache_or_updated))
#endif
                    {
                        // custom request headers
                        foreach (var kvp in this.webRequestCustomHeaders)
                            www.SetRequestHeader(kvp.Key, kvp.Value);

                        LOG(LogLevel.INFO, "Retrieving {0}", this.url_from_cache_or_updated);

#if UNITY_2017_2_OR_NEWER
                        yield return www.SendWebRequest();
#else
                        yield return www.Send();
#endif

                        if (
#if UNITY_2017_1_OR_NEWER
#if UNITY_2020_2_OR_NEWER
                            www.result != UnityEngine.Networking.UnityWebRequest.Result.Success
#else
                            www.isNetworkError
#endif
#else
                            www.isError
#endif
                            || !string.IsNullOrEmpty(www.error)
                            )
                        {
                            var msg = string.Format("Can't read playlist from {0} - {1}", this.url_from_cache_or_updated, www.error);

                            ERRCHECK(FMOD.RESULT.ERR_NET_URL, msg, false);

                            // pause little bit before possible next retrieval attempt
                            yield return new WaitForSeconds(0.5f);

                            this.StopWithReason(ESTOP_REASON.ErrorOrEOF);

                            yield break;
                        }

                        playlist = www.downloadHandler.text;
                    }
#else
                    using (WWW www = new WWW(this.url_from_cache_or_updated))
                    {
					    LOG(LogLevel.INFO, "Retrieving {0}", this.url_from_cache_or_updated );

                        yield return www;

                        if (!string.IsNullOrEmpty(www.error))
                        {
                            var msg = string.Format("Can't read playlist from {0} - {1}", this.url_from_cache_or_updated, www.error);

                            ERRCHECK(FMOD.RESULT.ERR_NET_URL, msg, false);

                            // pause little bit before possible next retrieval attempt
                            yield return new WaitForSeconds(0.5f);

                            this.StopWithReason(ESTOP_REASON.ErrorOrEOF);
                
                            yield break;
                        }

                        playlist = www.text;
                    }
#endif
                    // TODO: !HLS
                    // - relative entries
                    // - recursive entries
                    // - AAC - streaming chunks ?

                    if (this.playlistType.Value == PlaylistType.M3U
                        || this.playlistType.Value == PlaylistType.M3U8)
                    {
                        this.url_from_cache_or_updated = this.URLFromM3UPlaylist(playlist);
                        LOG(LogLevel.INFO, "URL from M3U/8 playlist: {0}", this.url_from_cache_or_updated);
                    }
                    else
                    {
                        this.url_from_cache_or_updated = this.URLFromPLSPlaylist(playlist);
                        LOG(LogLevel.INFO, "URL from PLS playlist: {0}", this.url_from_cache_or_updated);
                    }

                    if (string.IsNullOrEmpty(this.url_from_cache_or_updated))
                    {
                        var msg = string.Format("Can't parse playlist {0}", this.url_from_cache_or_updated);

                        ERRCHECK(FMOD.RESULT.ERR_FORMAT, msg, false);

                        // pause little bit before possible next retrieval attempt
                        yield return new WaitForSeconds(0.5f);

                        this.StopWithReason(ESTOP_REASON.ErrorOrEOF);

                        yield break;
                    }
                }
            }

            // create/open appropriate .net media handlers
            switch (this.mediaType)
            {
                case MEDIATYPE.NETWORK:

                    // start the request

                    // download handler buffer

                    // size (will fetch mostly less)
                    // several blockAlign size blocks are needed for correct codec detection and decoder start
                    var webRequestBuffer = new byte[Mathf.Max(2048, (int)this.blockalign * (int)this.blockalignDownloadMultiplier)];

                    // decoder buffer to write to
                    if (this.downloadToCache)
                        this.mediaBuffer = new DownloadFileSystemCachedFile(FileSystem.TempFilePath(this.url, "", ".compressed"), (uint)webRequestBuffer.Length);
                    else
                        this.mediaBuffer = new DownloadFileSystemMemoryBuffer((uint)webRequestBuffer.Length);

                    this.downloadHandler = new ByteStreamDownloadHandler(webRequestBuffer, this);

                    this.webRequest = new UnityWebRequest(this.url_from_cache_or_updated)
                    {
                        disposeDownloadHandlerOnDispose = true,
                        downloadHandler = this.downloadHandler
                    };

                    // custom request headers
                    foreach (var kvp in this.webRequestCustomHeaders)
                        this.webRequest.SetRequestHeader(kvp.Key, kvp.Value);

                    // new runtime (?) needs to ignore certificate explicitely it seems
#if UNITY_2018_1_OR_NEWER
                    this.webRequest.certificateHandler = new NoCheckPolicyCertificateHandler();
#endif
                    this.webRequest.SendWebRequest();

                    // fill initial dl buffer (if not finished already)
                    while (this.mediaDownloaded < webRequestBuffer.Length
                        && !this.downloadHandler.downloadComplete)
                    {
                        if (
#if UNITY_2020_2_OR_NEWER
                            (this.webRequest.result != UnityEngine.Networking.UnityWebRequest.Result.Success && this.webRequest.result != UnityWebRequest.Result.InProgress)
#else
                            this.webRequest.isNetworkError
                            || this.webRequest.isHttpError

#endif
                            || !string.IsNullOrEmpty(this.webRequest.error)
                            )
                        {
                            var msg = string.Format("{0}", this.webRequest.error);

                            ERRCHECK(FMOD.RESULT.ERR_NET_CONNECT, msg, false);

                            // pause little bit before possible next retrieval attempt
                            yield return new WaitForSeconds(0.5f);

                            this.StopWithReason(ESTOP_REASON.ErrorOrEOF);

                            yield break;
                        }

                        LOG(LogLevel.DEBUG, "Getting initial download data: {0}/{1} [{2} * {3}]", this.mediaDownloaded, webRequestBuffer.Length, this.blockalign, webRequestBuffer.Length / this.blockalign);

                        yield return null;
                    }

                    LOG(LogLevel.INFO, "Downloaded initial data: {0}/{1} [{2} * {3}]", this.mediaDownloaded, webRequestBuffer.Length, this.blockalign, webRequestBuffer.Length / this.blockalign);

                    break;
            }

            // start FMOD decoder

            // If userasyncread callback is specified - userread and userseek will not be called at all, so they can be set to 0 / null.
            // Explicitly create the delegate object and assign it to a member so it doesn't get freed by the garbage collector while it's not being used
            this.fileOpenCallback = new FMOD.FILE_OPEN_CALLBACK(Media_Open);
            this.fileCloseCallback = new FMOD.FILE_CLOSE_CALLBACK(Media_Close);
            this.fileAsyncReadCallback = new FMOD.FILE_ASYNCREAD_CALLBACK(Media_AsyncRead);
            this.fileAsyncCancelCallback = new FMOD.FILE_ASYNCCANCEL_CALLBACK(Media_AsyncCancel);
            /*
             * opening flags for streaming createSound
             */
            // -- FMOD.MODE.IGNORETAGS has to be OFF for MPEG (general mp3s with artwork won't play at all), and doesn't affect other formats - leaving OFF for all -
            // -- use FMOD.MODE.NONBLOCKING to have at least some control over opening the sound which would otherwise lock up FMOD when loading/opening currently impossible formats (hello netstream Vorbis)
            // -- FMOD.MODE.OPENONLY doesn't seem to make a difference
            var flags = FMOD.MODE.CREATESTREAM
                // | FMOD.MODE.OPENONLY
                // | FMOD.MODE.IGNORETAGS
                | FMOD.MODE.NONBLOCKING
                | FMOD.MODE.LOWMEM
                ;

            if (this is AudioStreamMemory)
                // FMOD.MODE.OPENMEMORY_POINT should not duplicate/copy memory (compared to FMOD.MODE.OPENMEMORY)
                flags |= FMOD.MODE.OPENMEMORY_POINT;

            /*
             * pass empty / default CREATESOUNDEXINFO, otherwise it hits nomarshalable unmanaged structure path on IL2CPP 
             */
            var extInfo = new FMOD.CREATESOUNDEXINFO();
            extInfo.suggestedsoundtype = this.streamType.ToFMODSoundType();

            // suggestedsoundtype must be hinted on iOS due to ERR_FILE_COULDNOTSEEK on getOpenState
            // allow any type for local files

            switch (extInfo.suggestedsoundtype)
            {
                case SOUND_TYPE.UNKNOWN:
                    if (this.mediaType == MEDIATYPE.NETWORK
                        && Application.platform == RuntimePlatform.IPhonePlayer
                        )
                    {
                        LOG(LogLevel.ERROR, "Please set stream type explicitely when streaming from network on iOS");
                        this.StopWithReason(ESTOP_REASON.User);

                        yield break;
                    }

                    break;

                case SOUND_TYPE.RAW:
                    extInfo.suggestedsoundtype = FMOD.SOUND_TYPE.RAW;

                    // raw data needs to ignore audio format and
                    // Use FMOD_CREATESOUNDEXINFO to specify format.Requires at least defaultfrequency, numchannels and format to be specified before it will open.Must be little endian data.
                    flags |= FMOD.MODE.OPENRAW;

                    extInfo.format = this.RAWSoundFormat;
                    extInfo.defaultfrequency = this.RAWFrequency;
                    extInfo.numchannels = this.RAWChannels;

                    break;
            }

            if (this is AudioStreamMemory)
            {
                // set memory location length for the sound:
                extInfo.length = (this as AudioStreamMemory).memoryLength;
            }

            /*
             * + this ptr as userdata in exinfo/fs callbacks
             */
            extInfo.fileuserdata = GCHandle.ToIntPtr(this.gc_thisPtr);
            extInfo.cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO));

            //uint deffbsize;
            //TIMEUNIT deftimeunit;
            //result = fmodsystem.System.getStreamBufferSize(out deffbsize, out deftimeunit);
            //ERRCHECK(result, "fmodsystem.System.getStreamBufferSize");
            //UnityEngine.Debug.LogFormat(@"def. fb size: {0}, def. time unit: {1}", deffbsize, deftimeunit);
            // defaults r 16k + RAWBYTES..

            // stream buffer is increased for netstreams which is not the case here
            // result = fmodsystem.System.setStreamBufferSize(16384, FMOD.TIMEUNIT.RAWBYTES);
            // ERRCHECK(result, "fmodsystem.System.setStreamBufferSize");

            /* 
             * setup 'file system' callbacks for audio data downloaded by unity web reqeust
             * also tags ERR_FILE_COULDNOTSEEK:
                http://stackoverflow.com/questions/7154223/streaming-mp3-from-internet-with-fmod
                https://www.fmod.com/docs/2.02/api/core-api-system.html#system_setfilesystem
             */
            switch (this.mediaType)
            {
                case MEDIATYPE.NETWORK:
                    // use async API to have offset in read requests
                    result = fmodsystem.system.setFileSystem(this.fileOpenCallback, this.fileCloseCallback, null, null, this.fileAsyncReadCallback, this.fileAsyncCancelCallback, (int)this.blockalign);
                    ERRCHECK(result, "fmodsystem.system.setFileSystem");

                    break;

                case MEDIATYPE.FILESYSTEM:
                    // check the file and its size -:
                    var binaryReader = new System.IO.BinaryReader(System.IO.File.Open(this.url_from_cache_or_updated, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read));
                    this.mediaLength = this.mediaDownloaded = this.mediaAvailable = (uint)binaryReader.BaseStream.Length;

                    result = fmodsystem.system.setFileSystem(this.fileOpenCallback, this.fileCloseCallback, null, null, null, null, (int)this.blockalign);
                    ERRCHECK(result, "fmodsystem.system.setFileSystem");

                    break;

                case MEDIATYPE.MEMORY:
                    this.mediaLength = this.mediaDownloaded = this.mediaAvailable = (this as AudioStreamMemory).memoryLength;

                    result = fmodsystem.system.setFileSystem(this.fileOpenCallback, this.fileCloseCallback, null, null, null, null, (int)this.blockalign);
                    ERRCHECK(result, "fmodsystem.system.setFileSystem");

                    break;
            }

            /*
             * Start streaming
             */
            LOG(LogLevel.DEBUG, "Creating sound for: {0}...", this.url_from_cache_or_updated);

            switch (this.mediaType)
            {
                case MEDIATYPE.NETWORK:
                    result = fmodsystem.system.createSound("_-====-_ decoder"
                        , flags
                        , ref extInfo
                        , out sound);
                    ERRCHECK(result, "fmodsystem.system.createSound");

                    // looks like this is needed after setting up the custom filesystem
                    System.Threading.Thread.Sleep(20);

                    break;

                case MEDIATYPE.FILESYSTEM:
                    result = fmodsystem.system.createSound(this.url_from_cache_or_updated
                        , flags
                        , ref extInfo
                        , out sound);
                    ERRCHECK(result, "fmodsystem.system.createSound");

                    break;

                case MEDIATYPE.MEMORY:
                    result = fmodsystem.system.createSound((this as AudioStreamMemory).memoryLocation
                        , flags
                        , ref extInfo
                        , out sound);
                    ERRCHECK(result, "fmodsystem.system.createSound");

                    break;
            }

            // do a graceful stop if our decoding skills are not up to par for now
            // TODO: should be for all result != OK possibly handled at ERR method level
            if (result != FMOD.RESULT.OK)
            {
                this.StopWithReason(ESTOP_REASON.User);
                yield break;
            }

            // Since 2017.1 there is a setting 'Force iOS Speakers when Recording' for this workaround needed in previous versions
#if !UNITY_2017_1_OR_NEWER
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                LOG(LogLevel.INFO, "Setting playback output to speaker...");
                iOSSpeaker.RouteForPlayback();
            }
#endif
            LOG(LogLevel.INFO, "About to play from: {0}...", this.url_from_cache_or_updated);

            yield return this.StreamCR();
        }
        IEnumerator StreamCR()
        {
            var isNetworkSource = this.url_from_cache_or_updated.ToLowerInvariant().StartsWith("http");
            // try few frames if playback can't initiated
            var initialConnectionRetryCount = 30;
            //
            bool streamCaught = false;
            //
            float stopFade = 0;
            //
            uint starvingFrames = 0;
            //
            this.channelIsAudibleTime = 0f;

            for (; ; )
            {
                if (this.isPaused)
                    yield return null;

                // FMOD playSound after it was opened
                if (!streamCaught)
                {
                    int c = 0;
                    do
                    {
                        fmodsystem.Update();

                        result = sound.getOpenState(out this.openstate, out this.bufferFillPercentage, out this.starving, out this.deviceBusy);
                        ERRCHECK(result, string.Format("sound.getOpenState {0}", openstate), false);

                        LOG(LogLevel.INFO, "Stream open state: {0}, buffer fill {1} starving {2} networkBusy {3}", this.openstate, this.bufferFillPercentage, this.starving, this.deviceBusy);

                        if (result == FMOD.RESULT.OK && (openstate == FMOD.OPENSTATE.READY || openstate == FMOD.OPENSTATE.PLAYING))
                        {
                            /*
                             * stream caught
                             */
                            result = sound.getFormat(out this.runtimeSoundType, out this.streamFormat, out this.streamChannels, out this.streamBits);
                            ERRCHECK(result, null);

                            float freq; int prio;
                            result = sound.getDefaults(out freq, out prio);
                            ERRCHECK(result, null);

                            // do small sanity check of stream properties too
                            if (
                                this.streamFormat != FMOD.SOUND_FORMAT.NONE
                                && this.streamChannels > 0
                                && this.streamBits > 0
                                && freq > 0
                                )
                            {
                                this.streamSampleRate = (int)freq;
                                this.streamBytesPerSample = (byte)(this.streamBits / 8);

                                LOG(LogLevel.INFO, "Stream type: {0} format: {1}, {2} channels {3} bits {4} samplerate", this.runtimeSoundType, this.streamFormat, this.streamChannels, this.streamBits, this.streamSampleRate);

                                // get master channel group
                                FMOD.ChannelGroup masterChannelGroup;
                                result = fmodsystem.system.getMasterChannelGroup(out masterChannelGroup);
                                ERRCHECK(result, "fmodsystem.system.getMasterChannelGroup");

                                // play the sound
                                result = fmodsystem.system.playSound(sound, masterChannelGroup, false, out channel);
                                ERRCHECK(result, "fmodsystem.system.playSound");

                                this.StreamStarting();

                                streamCaught = true;

                                if (this.OnPlaybackStarted != null)
                                    this.OnPlaybackStarted.Invoke(this.gameObjectName);
                            }

                            break;
                        }
                        else
                        {
                            /*
                             * Unable to stream
                             */
                            if (++c > initialConnectionRetryCount)
                            {
                                if (isNetworkSource)
                                {
                                    LOG(LogLevel.ERROR, "Can't start playback. Please make sure that correct audio type of stream is selected, network is reachable and possibly check Advanced setting. {0} {1}", result, openstate);
#if UNITY_EDITOR
                                    LOG(LogLevel.ERROR, "If everything seems to be ok, restarting the editor often helps while having trouble connecting to especially Ogg/Vorbis streams. {0} {1}", result, openstate);
#endif
                                }
                                else
                                {
                                    LOG(LogLevel.ERROR, "Can't start playback. Unrecognized audio type.");
                                }


                                ERRCHECK(FMOD.RESULT.ERR_FILE_BAD, string.Format("Can't start playback{0} {1}", result, openstate), false);

                                // pause little bit before possible next retrieval attempt
                                yield return new WaitForSeconds(0.5f);

                                this.StopWithReason(ESTOP_REASON.ErrorOrEOF);

                                yield break;
                            }
                        }

                        yield return new WaitForSeconds(0.1f);

                    } while (result != FMOD.RESULT.OK || openstate != FMOD.OPENSTATE.READY);
                }

                //
                // Updates
                //

                // (cosmetic) network connection check, since absolutely nothing in webRequst or handler indicates any error state when e.g. network is disconnected (tF)
                // leaving it here for now.. 
                if (this.mediaType == MEDIATYPE.NETWORK)
                {
                    if (
#if UNITY_2020_2_OR_NEWER
                            (this.webRequest.result != UnityEngine.Networking.UnityWebRequest.Result.Success && this.webRequest.result != UnityWebRequest.Result.InProgress)
#else
                            this.webRequest.isNetworkError
                            || this.webRequest.isHttpError

#endif
                        || !string.IsNullOrEmpty(this.webRequest.error)
                        )
                    {
                        var msg = string.Format("{0}", this.webRequest.error);
                        ERRCHECK(FMOD.RESULT.ERR_NET_CONNECT, msg, false);

                        // pause little bit before possible next retrieval attempt
                        yield return new WaitForSeconds(0.5f);

                        this.StopWithReason(ESTOP_REASON.ErrorOrEOF);

                        yield break;
                    }
                }

                // process actions dispatched on the main thread such as for album art texture creation / calls from callbacks which need to call Unity
                lock (this.executionQueue)
                {
                    if (this.executionQueue.Count > 0)
                    {
                        this.executionQueue.Dequeue().Invoke();
                    }
                }

                // notify tags update
                if (this.tagsChanged)
                {
                    this.tagsChanged = false;

                    if (this.OnTagChanged != null)
                        lock (this.tagsLock)
                            foreach (var tag in this.tags)
                                this.OnTagChanged.Invoke(this.gameObjectName, tag.Key, tag.Value);
                }

                //
                // FMOD update & playing check
                //
                result = fmodsystem.Update();
                ERRCHECK(result, "fmodsystem.Update", false);

                if (this.isPlayingChannel && !this.isPaused)
                    this.channelIsAudibleTime += Time.deltaTime;

                // channel isPlaying is reliable for finite sizes (FMOD will stop channel automatically on finite lengths)
                // for 'infinite' size nothing in FMOD (results from update calls, open state, ...) seems to be indicating any data shortage whatsoever - everything just keeps playing in seemingly correct state, despite returning RESULT.ERR_FILE_EOF from async read ... (tF)

                // result = sound.getOpenState(out openstate, out bufferFillPercentage, out this.starving, out deviceBusy);
                // ERRCHECK(result, "sound.getOpenState", false);
                // LOG(LogLevel.DEBUG, "Stream open state: {0}, buffer fill {1} starving {2} networkBusy {3}", openstate, bufferFillPercentage, this.starving, deviceBusy);

                // in that case check for consecutive unchanged RESULT.ERR_FILE_EOF media_asyncread (~~ downloaded media buffer) results:
                // [ this.downloadHandler.downloaded resp. this.webRequest.downloadedBytes dont'change, too, we picked the above ]
                bool haveEnoughData = true;

                if (this.isPlayingChannel)
                {
                    // Silence the stream until there's data for smooth playback
                    result = channel.setMute(this.starving);
                    ERRCHECK(result, "channel.setMute", false);

                    // update specific
                    this.StreamStarving();

                    // update incoming tags
                    if (!this.starving
                        && this.readTags)
                        this.ReadTags();

                    // count stalled frames for non finite streams - otherwise FMOD should stop channel automatically
                    if (this.mediaType == MEDIATYPE.NETWORK)
                    {
                        // -- initial empty reads (from supposedly end of the stream) are exhausted on opening, so these should occur only after real playback has started --
                        if (this.media_read_lastResult == FMOD.RESULT.ERR_FILE_EOF)
                            starvingFrames++;
                        else
                            starvingFrames = 0;

                        if (starvingFrames >= this.starvingRetryCount
                            && this.mediaAvailable < this.blockalign
                            )
                        {
                            haveEnoughData = false;
                            LOG(LogLevel.WARNING, "Download stalled for {0} frames..", starvingFrames);
                        }
                    }
                }

                // end
                if (!this.isPlayingChannel || !haveEnoughData)
                {
                    if (this is AudioStream)
                    {
                        // kill Unity PCM only after some time since it has to yet play some
                        // TODO: try to compute this
                        // this clip length / channels question mark
                        if ((stopFade += Time.deltaTime) > 2f)
                        {
                            LOG(LogLevel.INFO, "Starving frame [playing:{0}({1}), enough media data: {2}]", this.isPlayingChannel, this.media_read_lastResult, haveEnoughData);

                            this.StopWithReason(ESTOP_REASON.ErrorOrEOF);
                            yield break;
                        }
                    }
                    else
                    {
                        // !PCM callback
                        // channel is stopped/finished
                        LOG(LogLevel.INFO, "Starving frame [playing:{0}({1}), enough media data: {2}]", this.isPlayingChannel, this.media_read_lastResult, haveEnoughData);

                        this.StopWithReason(ESTOP_REASON.ErrorOrEOF);
                        yield break;
                    }
                }

                yield return null;
            }
        }

        public void Pause(bool pause)
        {
            if (!this.isPlaying)
            {
                LOG(LogLevel.WARNING, "Not playing..");
                return;
            }

            this.isPaused = pause;

            LOG(LogLevel.INFO, "{0}", this.isPaused ? "paused." : "resumed.");

            if (this.OnPlaybackPaused != null)
                this.OnPlaybackPaused.Invoke(this.gameObjectName, this.isPaused);
        }

        bool tagsChanged = false;
        readonly object tagsLock = new object();
        /// <summary>
        /// Reads 1 tag from running stream
        /// Sets flag for CR to update due to threading
        /// </summary>
        protected void ReadTags()
        {
            // Read any tags that have arrived, this could happen if a radio station switches to a new song.
            FMOD.TAG streamTag;
            // Have to use FMOD >= 1.10.01 for tags to work - https://github.com/fmod/UnityIntegration/pull/11

            while (sound.getTag(null, -1, out streamTag) == FMOD.RESULT.OK)
            {
                // do some tag examination and logging for unhandled tag types
                // special FMOD tag type for detecting sample rate change

                var FMODtag_Type = streamTag.type;
                string FMODtag_Name = (string)streamTag.name;
                object FMODtag_Value = null;

                if (FMODtag_Type == FMOD.TAGTYPE.FMOD
                    && FMODtag_Name == "Sample Rate Change"
                    )
                {
                    // When a song changes, the samplerate may also change, so update here.
                        // resampling is done via the AudioClip - but we have to recreate it for AudioStream ( will cause small stream disruption but there's probably no other way )
                        // , do it via direct calls without events

                        // float frequency = *((float*)streamTag.data);
                        float[] frequency = new float[1];
                        Marshal.Copy(streamTag.data, frequency, 0, 1);

                        LOG(LogLevel.WARNING, "Stream sample rate changed to: {0}", frequency[0]);

                        // get current sound_format
                        result = sound.getFormat(out this.runtimeSoundType, out this.streamFormat, out this.streamChannels, out this.streamBits);
                        ERRCHECK(result, "sound.getFormat", false);

                        this.StreamChanged(frequency[0], this.streamChannels, this.streamFormat);
                }
                else
                {
                    switch (streamTag.datatype)
                    {
                        case FMOD.TAGDATATYPE.BINARY:

                            FMODtag_Value = "binary data";

                            // check if it's ID3v2 'APIC' tag for album/cover art
                            if (FMODtag_Type == FMOD.TAGTYPE.ID3V2)
                            {
                                if (FMODtag_Name == "APIC" || FMODtag_Name == "PIC")
                                {
                                    byte[] picture_data;
                                    byte picture_type;

                                    // read all texture bytes into picture_data
                                    this.ReadID3V2TagValue_APIC(streamTag.data, streamTag.datalen, out picture_data, out picture_type);

                                    // since 'There may be several pictures attached to one file, each in their individual "APIC" frame, but only one with the same content descriptor.'
                                    // we store its type alongside tag name and create every texture present
                                    if (picture_data != null)
                                    {
                                        // Load texture on the main thread, if needed
                                        this.LoadTexture_OnMainThread(picture_data, picture_type);
                                    }
                                }
                            }

                            break;

                        case FMOD.TAGDATATYPE.FLOAT:
                            FMODtag_Value = this.ReadFMODTagValue_float(streamTag.data, streamTag.datalen);
                            break;

                        case FMOD.TAGDATATYPE.INT:
                            FMODtag_Value = this.ReadTagValue_int(streamTag.data, streamTag.datalen);
                            break;

                        case FMOD.TAGDATATYPE.STRING:
                            FMODtag_Value = Marshal.PtrToStringAnsi(streamTag.data, (int)streamTag.datalen);
                            break;

                        case FMOD.TAGDATATYPE.STRING_UTF16:
                        case FMOD.TAGDATATYPE.STRING_UTF16BE:
                        case FMOD.TAGDATATYPE.STRING_UTF8:

                            FMODtag_Value = Marshal.PtrToStringAnsi(streamTag.data, (int)streamTag.datalen);
                            break;
                    }

                    // update tags, binary data (texture) is handled separately
                    if (streamTag.datatype != FMOD.TAGDATATYPE.BINARY)
                    {
                        lock (this.tagsLock)
                            this.tags[FMODtag_Name] = FMODtag_Value;
                        this.tagsChanged = true;
                    }
                }

                LOG(LogLevel.INFO, "{0} tag: {1}, [{2}] value: {3}", FMODtag_Type, FMODtag_Name, streamTag.datatype, FMODtag_Value);
            }
        }
        /// <summary>
        /// Total sound length in seconds for playback info
        /// </summary>
        public float SoundLengthInSeconds
        {
            get
            {
                float seconds = 0f;
                if (sound.hasHandle())
                {
                    uint length_time_uint;
                    result = sound.getLength(out length_time_uint, FMOD.TIMEUNIT.MS);
                    // don't spam the console while opening & not ready
                    if (result != RESULT.ERR_NOTREADY)
                        ERRCHECK(result, "sound.getLength", false);

                    seconds = length_time_uint / 1000f;
                }

                return seconds;
            }
        }
        /// <summary>
        /// Approximate the lenght of sound so far from downloaded bytes based on total length of the sound
        /// </summary>
        public float SoundLengthInDownloadedSeconds
        {
            get
            {
                var dlRatio = this.mediaDownloaded / (float)this.mediaLength;
                return this.SoundLengthInSeconds * dlRatio;
            }
        }
        /// <summary>
        /// position in seconds for playback info
        /// (playback time will be slightly ahead in AudioStream due Unity PCM callback latency)
        /// </summary>
        public float PositionInSeconds
        {
            get
            {
                float position = 0f;
                if (this.isPlayingChannel)
                {
                    uint position_ms;
                    result = this.channel.getPosition(out position_ms, FMOD.TIMEUNIT.MS);
                    // ERRCHECK(result, "channel.getPosition", false); // - will ERR_INVALID_HANDLE on finished channel -

                    if (result == FMOD.RESULT.OK)
                        position = position_ms / 1000f;
                }

                return position;
            }
            set
            {
                if (this.isPlayingChannel)
                {
                    if (!this.MediaIsInfinite)
                    {
                        // FMOD should handle time bounds automatically
                        uint position_ms = (uint)(value * 1000f);
                        result = this.channel.setPosition(position_ms, FMOD.TIMEUNIT.MS);
                        // ERRCHECK(result, "channel.setPosition", false); // - will ERR_INVALID_POSITION when seeking out of bounds of the lenght of the sound, so e.g. don't
                    }
                    else
                    {
                        // at least don't go past realtime, as counted since audio playback / channel  was started
                        // TODO: there's more downloaded so potentially determine how much and remove this limit
                        if (value <= this.channelIsAudibleTime)
                        {
                            uint position_ms = (uint)(value * 1000f);
                            result = this.channel.setPosition(position_ms, FMOD.TIMEUNIT.MS);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// [UX] Allow arbitrary position change on networked media with DISK buffer, or any non networked media
        /// </summary>
        public bool IsSeekable
        {
            get
            {
                return (this.mediaType == MEDIATYPE.NETWORK && this.downloadToCache) || this.mediaType == MEDIATYPE.FILESYSTEM || this.mediaType == MEDIATYPE.MEMORY;
            }
        }
        #endregion

        // ========================================================================================================================================
        #region output and devices updates
        [Header("[Output]")]
        [Tooltip("You can specify any available audio output device present in the system.\r\nPass an interger number between 0 and (# of audio output devices) - 1 (0 is default output).\r\nThis ID will change to reflect addition/removal of devices at runtime when a game object with AudioStreamDevicesChangedNotify component is in the scene. See FMOD_SystemW.AvailableOutputs / notification event from AudioStreamDevicesChangedNotify in the demo scene.")]
        [SerializeField]
        protected int outputDriverID = 0;
        /// <summary>
        /// public facing property
        /// </summary>
        public int OutputDriverID { get { return this.outputDriverID; } protected set { this.outputDriverID = value; } }
        /// <summary>
        /// Complete info about output #outputDriverID - relevant for 'FMOD based' descendants only for SetOutput/ReflectOutput
        /// </summary>
        FMOD_SystemW.OUTPUT_DEVICE outputDevice;
        /// <summary>
        /// Allows setting the output device 
        /// default directly FMOD based implementation (for AudioStreamMinimal/Resonance and similar)
        /// </summary>
        /// <param name="outputDriverId"></param>
        public virtual void SetOutput(int _outputDriverID)
        {
            if (!this.ready)
            {
                LOG(LogLevel.ERROR, "Please make sure to wait for 'ready' flag before calling this method");
                return;
            }

            LOG(LogLevel.INFO, "Setting output to driver {0} ", _outputDriverID);

            // try to help the system to update its surroundings
            result = fmodsystem.Update();
            ERRCHECK(result, "fmodsystem.Update", false);

            if (result != RESULT.OK)
                return;

            result = fmodsystem.system.setDriver(_outputDriverID);
            // ERRCHECK(result, string.Format("fmodsystem.System.setDriver {0}", _outputDriverID), false);

            if (result != RESULT.OK)
            {
                // TODO
                LOG(LogLevel.WARNING, "Setting output to newly appeared device {0} during runtime is currently not supported for FMOD/minimal components", _outputDriverID);
                return;
            }

            this.outputDriverID = _outputDriverID;

            /*
             * Log output device info
             */
            int od_namelen = 255;
            string od_name;
            System.Guid od_guid;
            int od_systemrate;
            FMOD.SPEAKERMODE od_speakermode;
            int od_speakermodechannels;

            result = fmodsystem.system.getDriverInfo(_outputDriverID, out od_name, od_namelen, out od_guid, out od_systemrate, out od_speakermode, out od_speakermodechannels);
            ERRCHECK(result, "fmodsystem.system.getDriverInfo", false);

            if (result != RESULT.OK)
                return;

            LOG(LogLevel.INFO, "Device {0} Info: Output samplerate: {1}, speaker mode: {2}, num. of raw speakers: {3}", _outputDriverID, od_systemrate, od_speakermode, od_speakermodechannels);

            this.outputDriverID = _outputDriverID;
            this.outputDevice = new FMOD_SystemW.OUTPUT_DEVICE() { id = this.outputDriverID, name = od_name, samplerate = od_systemrate, speakermode = od_speakermode, channels = od_speakermodechannels, guid = od_guid };
        }
        /// <summary>
        /// default directly FMOD based implementation (for AudioStreamMinimal and similar)
        /// first phase of reflecting updated outputs on device/s change
        /// (does effectively nothing since there's no sound to stop)
        /// </summary>
        public virtual void ReflectOutput_Start()
        {
            if (!this.ready)
            {
                LOG(LogLevel.ERROR, "Please make sure to wait for 'ready' flag before calling this method");
                return;
            }

            // redirection is always running so restart it with new output
            // this.StopFMODSound();
        }
        /// <summary>
        /// Updates this' runtimeOutputDriverID and FMOD sound/system based on new/updated system devices list
        /// Tries to find output in new list if it was played already, or sets output to be user output id, if possible
        /// default directly FMOD based implementation (for AudioStreamMinimal and similar)
        /// </summary>
        /// <param name="updatedOutputDevices"></param>
        public virtual void ReflectOutput_Finish(List<FMOD_SystemW.OUTPUT_DEVICE> updatedOutputDevices)
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


            // this.StartFMODSound(outputID);

            // might fail in case initial FMOD snapshot didn't capture new id...
            this.SetOutput(outputID);
        }
        #endregion

        // ========================================================================================================================================
        #region Shutdown
        /// <summary>
        /// Reason if calling Stop to distinguish between user initiad stop, and stop on error/end of file.
        /// </summary>
        enum ESTOP_REASON
        {
            /// <summary>
            /// Just stop and don't perform any recovery actions
            /// </summary>
            User,
            /// <summary>
            /// Error from network, of actually finished file on established connection
            /// Will try to reconnect based on user setting
            /// </summary>
            ErrorOrEOF
        }
        /// <summary>
        /// wrong combination of requested audio type and actual stream type leads to still BUFFERING/LOADING state of the stream
        /// don't release sound and system in that case and notify user
        /// </summary>
        bool unstableShutdown = false;
        /// <summary>
        /// User facing Stop -
        /// - if initiated by user we won't be restarting automatically and ignore replay/reconnect attempt
        /// We'll ignore any state we're in and just straight go ahead with stopping - UnityWebRequest and FMOD seem to got better so interruptions should be ok
        /// </summary>
        public void Stop()
        {
            this.StopWithReason(ESTOP_REASON.User);
        }
        /// <summary>
        /// Called by user, and internally when needed to stop automatically
        /// </summary>
        /// <param name="stopReason"></param>
        void StopWithReason(ESTOP_REASON stopReason)
        {
            LOG(LogLevel.INFO, "Stopping..");

            this.isPlayingUser = false;

            this.StopAllCoroutines();

            this.StreamStopping();

            /*
             * try to release FMOD sound resources
             */

            /*
             * Stop the channel, then wait for it to finish opening before we release it.
             */
            if (channel.hasHandle())
            {
                // try to stop the channel, but don't check any error - might have been already stopped because end was reached
                result = channel.stop();
                // ERRCHECK(result, "channel.stop", false);

                channel.clearHandle();
            }

            /*
             * If the sound is still buffering at this point (but not trying to connect without available connection), we can't do much - namely we can't release sound and system since FMOD deadlocks in this state
             * This happens when requesting wrong audio type for stream.
             */
            this.unstableShutdown = false;

            // system has to be still valid for next calls
            if (fmodsystem != null)
            {
                result = fmodsystem.Update();

                if (result == FMOD.RESULT.OK)
                {
                    if (sound.hasHandle())
                    {
                        result = sound.getOpenState(out openstate, out bufferFillPercentage, out starving, out deviceBusy);
                        ERRCHECK(result, "sound.getOpenState", false);

                        LOG(LogLevel.DEBUG, "Stream open state: {0}, buffer fill {1} starving {2} networkBusy {3}", openstate, bufferFillPercentage, starving, deviceBusy);
                    }

                    if (openstate == FMOD.OPENSTATE.BUFFERING || openstate == FMOD.OPENSTATE.LOADING)
                    {
                        this.unstableShutdown = true;
                        var msg = string.Format("Unstable state while stopping the stream detected - will attempt recovery on release. [{0} {1}]"
                            , openstate
                            , result);

                        LOG(LogLevel.WARNING, msg);
                    }

                    /*
                     * Shut down
                     */
                    System.Threading.Thread.Sleep(10);

                    if (sound.hasHandle() && !this.unstableShutdown)
                    {
                        result = sound.release();
                        ERRCHECK(result, "sound.release", false);

                        sound.clearHandle();
                    }
                }
            }

            // needed to dispose handler + callback
            if (this.webRequest != null)
            {
                this.webRequest.Dispose();
                this.webRequest = null;
            }

            if (this.mediaBuffer != null)
            {
                this.mediaBuffer.CloseStore();
                this.mediaBuffer = null;
            }

            this.mediaAvailable = 0;
            this.starving = false;
            this.deviceBusy = false;
            this.tags = new Dictionary<string, object>();

            // based on stopping reason we either stop completely and call user event handler
            // , attempt to reconect on failed attempt

            // process cache, too
            var sourceFilepath = string.Empty;
            // var targetFilename = this.url.Split('/').Last();
            // var targetExtension = "." + this.runtimeSoundType.ToString().ToLowerInvariant();
            var downloadedFilepath = string.Empty;

            switch (stopReason)
            {
                case ESTOP_REASON.User:

                    if (this.downloadToCache
                        && this.mediaType == MEDIATYPE.NETWORK
                        )
                    {
                        // offline cache based on original url
                        sourceFilepath = FileSystem.TempFilePath(this.url, "", ".compressed");
                        downloadedFilepath = FileSystem.DownloadCacheFilePath(this.url, "");
                        this.ProcessDownload(sourceFilepath, downloadedFilepath);
                    }

                    // everything finished - just call user event handler 
                    if (this.OnPlaybackStopped != null)
                        this.OnPlaybackStopped.Invoke(this.gameObjectName, downloadedFilepath);

                    break;

                case ESTOP_REASON.ErrorOrEOF:
                    // we have no way of distinguishing between an actual error, or finished file here
                    // so we need user flag to determine between stop + event, or reconnect

                    if (this.continuosStreaming)
                    {
                        // Coroutine scheduled here should be safe
                        // Start the playback again with existing parameters (few initial checks are skipped)
                        LOG(LogLevel.INFO, "Attempting to restart the connection ('continuous streaming' is ON)...");
                        StartCoroutine(this.PlayCR());
                    }
                    else
                    {
                        if (this.downloadToCache
                            && this.mediaType == MEDIATYPE.NETWORK
                            )
                        {
                            // offline cache based on original url
                            sourceFilepath = FileSystem.TempFilePath(this.url, "", ".compressed");
                            downloadedFilepath = FileSystem.DownloadCacheFilePath(this.url, "");
                            this.ProcessDownload(sourceFilepath, downloadedFilepath);
                        }

                        if (this.OnPlaybackStopped != null)
                            this.OnPlaybackStopped.Invoke(this.gameObjectName, downloadedFilepath);
                    }

                    break;
            }
        }

        public virtual void OnDestroy()
        {
            // try to stop even when only connecting when component is being destroyed - 
            // if the stream is of correct type the shutdown should be clean 
            this.StopWithReason(ESTOP_REASON.User);

            // : based on FMOD Debug logging : Init FMOD file thread. Priority: 1, Stack Size: 16384, Semaphore: No, Sleep Time: 10, Looping: Yes.
            // wait for file thread sleep time+
            // ^ that seems to correctly release the system 

            if (this.unstableShutdown)
            {
                // attempt to release sound once more after a delay -

                System.Threading.Thread.Sleep(20);

                if (sound.hasHandle())
                {
                    result = sound.release();
                    ERRCHECK(result, "sound.release", false);

                    sound.clearHandle();
                }

                System.Threading.Thread.Sleep(20);
            }
            else
            {
                System.Threading.Thread.Sleep(10);
            }

            if (this is AudioStreamMemory
               || this is AudioStreamRuntimeImport
               )
                FMOD_SystemW.FMOD_System_NRT_Release(ref this.fmodsystem, this.logLevel, this.gameObjectName, this.OnError);
            else
                FMOD_SystemW.FMOD_System_Release(ref this.fmodsystem, this.logLevel, this.gameObjectName, this.OnError);

            if (this.gc_thisPtr.IsAllocated)
                this.gc_thisPtr.Free();
        }
        #endregion

        // ========================================================================================================================================
        #region Support

        public void ERRCHECK(FMOD.RESULT result, string customMessage, bool throwOnError = true)
        {
            this.lastError = result;

            if (throwOnError)
            {
                try
                {
                    FMODHelpers.ERRCHECK(result, this.logLevel, this.gameObjectName, this.OnError, customMessage, throwOnError);
                }
                catch (System.Exception ex)
                {
                    // clear the startup flag only when requesting abort on error
                    throw ex;
                }
            }
            else
            {
                FMODHelpers.ERRCHECK(result, this.logLevel, this.gameObjectName, this.OnError, customMessage, throwOnError);
            }
        }

        protected void LOG(LogLevel requestedLogLevel, string format, params object[] args)
        {
            Log.LOG(requestedLogLevel, this.logLevel, this.gameObjectName, format, args);
        }

        public string GetLastError(out FMOD.RESULT errorCode)
        {
            errorCode = this.lastError;
            return FMOD.Error.String(errorCode);
        }

        /// <summary>
        /// M3U/8 = its own simple format: https://en.wikipedia.org/wiki/M3U
        /// </summary>
        /// <param name="_playlist"></param>
        /// <returns></returns>
        string URLFromM3UPlaylist(string _playlist)
        {
            using (System.IO.StringReader source = new System.IO.StringReader(_playlist))
            {
                string s = source.ReadLine();
                while (s != null)
                {
                    // If the read line isn't a metadata, it's a file path
                    if ((s.Length > 0) && (s[0] != '#'))
                        return s;

                    s = source.ReadLine();
                }

                return null;
            }
        }

        /// <summary>
        /// PLS ~~ INI format: https://en.wikipedia.org/wiki/PLS_(file_format)
        /// </summary>
        /// <param name="_playlist"></param>
        /// <returns></returns>
        string URLFromPLSPlaylist(string _playlist)
        {
            using (System.IO.StringReader source = new System.IO.StringReader(_playlist))
            {
                string s = source.ReadLine();

                int equalIndex;
                while (s != null)
                {
                    if (s.Length > 4)
                    {
                        // If the read line isn't a metadata, it's a file path
                        if ("FILE" == s.Substring(0, 4).ToUpper())
                        {
                            equalIndex = s.IndexOf("=") + 1;
                            s = s.Substring(equalIndex, s.Length - equalIndex);

                            return s;
                        }
                    }

                    s = source.ReadLine();
                }

                return null;
            }
        }
        void ProcessDownload(string sourceFilepath, string targetFilepath)
        {
            if (File.Exists(sourceFilepath))
            {
                try
                {
                    if (File.Exists(targetFilepath))
                    {
                        File.Delete(targetFilepath);
                        File.Move(sourceFilepath, targetFilepath);
                        LOG(LogLevel.INFO, "Download: overwritten existing file at '{0}'", targetFilepath);
                    }
                    else
                    {
                        File.Move(sourceFilepath, targetFilepath);
                        LOG(LogLevel.INFO, "Download: saved downloaded file as '{0}'", targetFilepath);
                    }
                }
                catch(System.IO.IOException)
                {
                    LOG(LogLevel.ERROR, "Please make sure only one {0} instance actively downloads from a given Url", typeof(AudioStreamBase).ToString());
                }
            }
        }
        #endregion

        // ========================================================================================================================================
        #region Tags support
        #region Main thread execution queue for texture creation
        readonly Queue<System.Action> executionQueue = new Queue<System.Action>();
        /// <summary>
        /// Locks the queue and adds the Action to the queue
        /// </summary>
        /// <param name="action">function that will be executed from the main thread.</param>
        void ScheduleAction(System.Action action)
        {
            lock (this.executionQueue)
            {
                this.executionQueue.Enqueue(action);
            }
        }
        #endregion
        /// <summary>
        /// Calls texture creation if on main thread, otherwise schedules its creation to the main thread
        /// </summary>
        /// <param name="tagName"></param>
        /// <param name="picture_bytes"></param>
        /// <param name="picture_type"></param>
        void LoadTexture_OnMainThread(byte[] picture_bytes, byte picture_type)
        {
            // follow APIC tag for now
            var tagName = "APIC_" + picture_type;

            // if on main thread, create & load texture
            if (System.Threading.Thread.CurrentThread.ManagedThreadId == this.mainThreadId)
            {
                this.LoadTexture(tagName, picture_bytes);
            }
            else
            {
                this.ScheduleAction(() => { this.LoadTexture(tagName, picture_bytes); });
            }
        }
        /// <summary>
        /// Creates new texture from raw jpg/png bytes and adds it to the tags dictionary
        /// </summary>
        /// <param name="tagName"></param>
        /// <param name="picture_bytes"></param>
        void LoadTexture(string tagName, byte[] picture_bytes)
        {
            Texture2D image = new Texture2D(2, 2);
            image.LoadImage(picture_bytes);

            lock (this.tagsLock)
                this.tags[tagName] = image;
            this.tagsChanged = true;
        }
        // ID3V2 APIC tag specification
        // 
        // Following http://id3.org/id3v2.3.0
        // 
        // Numbers preceded with $ are hexadecimal and numbers preceded with % are binary. $xx is used to indicate a byte with unknown content.
        // 
        // <Header for 'Attached picture', ID: "APIC">
        // Text encoding   $xx
        // MIME type<text string> $00
        // Picture type    $xx
        // Description<text string according to encoding> $00 (00)
        // Picture data<binary data>
        //
        // Frames that allow different types of text encoding have a text encoding description byte directly after the frame size. If ISO-8859-1 is used this byte should be $00, if Unicode is used it should be $01
        //
        // Picture type:
        // $00     Other
        // $01     32x32 pixels 'file icon' (PNG only)
        // $02     Other file icon
        // $03     Cover(front)
        // $04     Cover(back)
        // $05     Leaflet page
        // $06     Media(e.g.lable side of CD)
        // $07     Lead artist/lead performer/soloist
        // $08     Artist/performer
        // $09     Conductor
        // $0A     Band/Orchestra
        // $0B     Composer
        // $0C     Lyricist/text writer
        // $0D     Recording Location
        // $0E     During recording
        // $0F     During performance
        // $10     Movie/video screen capture
        // $11     A bright coloured fish
        // $12     Illustration
        // $13     Band/artist logotype
        // $14     Publisher/Studio logotype

        /// <summary>
        /// Reads value (image data) of the 'APIC' tag as per specification from http://id3.org/id3v2.3.0
        /// We are *hoping* that any and all strings are ASCII/Ansi *only*
        /// </summary>
        /// <param name="fromAddress"></param>
        /// <param name="datalen"></param>
        /// <returns></returns>
        protected void ReadID3V2TagValue_APIC(System.IntPtr fromAddress, uint datalen, out byte[] picture_data, out byte picture_type)
        {
            picture_data = null;

            // Debug.LogFormat("IntPtr: {0}, length: {1}", fromAddress, datalen);

            var text_encoding = (byte)Marshal.PtrToStructure(fromAddress, typeof(byte));
            fromAddress = new System.IntPtr(fromAddress.ToInt64() + 1);
            datalen--;

            // Debug.LogFormat("text_encoding: {0}", text_encoding);

            // Frames that allow different types of text encoding have a text encoding description byte directly after the frame size. If ISO-8859-1 is used this byte should be $00, if Unicode is used it should be $01
            uint terminator_size;
            if (text_encoding == 0)
                terminator_size = 1;
            else if (text_encoding == 1)
                terminator_size = 2;
            else
                // Not 1 && 2 text encoding is invalid - should we try the string to be terminated by... single 0 ?
                terminator_size = 1;

            // Debug.LogFormat("terminator_size: {0}", terminator_size);

            uint bytesRead;

            string MIMEtype = FMODHelpers.StringFromNative(fromAddress, out bytesRead);
            fromAddress = new System.IntPtr(fromAddress.ToInt64() + bytesRead + terminator_size);
            datalen -= bytesRead;
            datalen -= terminator_size;

            // Debug.LogFormat("MIMEtype: {0}", MIMEtype);

            picture_type = (byte)Marshal.PtrToStructure(fromAddress, typeof(byte));
            fromAddress = new System.IntPtr(fromAddress.ToInt64() + 1);
            datalen--;

            // Debug.LogFormat("picture_type: {0}", picture_type);

            // string description_text = 
            FMODHelpers.StringFromNative(fromAddress, out bytesRead);
            fromAddress = new System.IntPtr(fromAddress.ToInt64() + bytesRead + terminator_size);
            datalen -= bytesRead;
            datalen -= terminator_size;

            // Debug.LogFormat("description_text: {0}", description_text);

            // Debug.LogFormat("Supposed picture byte size: {0}", datalen);
            if (
                // "image/" prefix is from spec, but some tags them are broken
                // MIMEtype.ToLowerInvariant().StartsWith("image/")
                // &&
                (
                    MIMEtype.ToLowerInvariant().EndsWith("jpeg")
                    || MIMEtype.ToLowerInvariant().EndsWith("jpg")
                    || MIMEtype.ToLowerInvariant().EndsWith("png")
                    )
                )
            {
                picture_data = new byte[datalen];
                Marshal.Copy(fromAddress, picture_data, 0, (int)datalen);
            }
        }
        /// <summary>
        /// https://www.fmod.com/docs/2.02/api/core-api-sound.html#fmod_tagdatatype
        /// IEEE floating point number. See FMOD_TAG structure to confirm if the float data is 32bit or 64bit (4 vs 8 bytes).
        /// </summary>
        /// <param name="fromAddress"></param>
        /// <param name="datalen"></param>
        /// <returns></returns>
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        protected double ReadFMODTagValue_float(System.IntPtr fromAddress, uint datalen)
        {
            byte[] barray = new byte[datalen];

            for (var offset = 0; offset < datalen; ++offset)
                barray[offset] = Marshal.ReadByte(fromAddress, offset);

            switch(datalen)
            {
                case 4:
                    return System.BitConverter.ToSingle(barray, 0);
                case 8:
                    return System.BitConverter.ToDouble(barray, 0);
            }

            return 0;
        }
        /// <summary>
        /// https://www.fmod.com/docs/2.02/api/core-api-sound.html#fmod_tagdatatype
        /// Integer - Note this integer could be 8bit / 16bit / 32bit / 64bit. See FMOD_TAG structure for integer size (1 vs 2 vs 4 vs 8 bytes).
        /// </summary>
        /// <param name="fromAddress"></param>
        /// <param name="datalen"></param>
        /// <returns></returns>
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        protected long ReadTagValue_int(System.IntPtr fromAddress, uint datalen)
        {
            byte[] barray = new byte[datalen];

            for (var offset = 0; offset < datalen; ++offset)
                barray[offset] = Marshal.ReadByte(fromAddress, offset);

            switch(datalen)
            {
                case 1:
                    return barray[0];
                case 2:
                    return System.BitConverter.ToInt16(barray, 0);
                case 4:
                    return System.BitConverter.ToInt32(barray, 0);
                case 8:
                    return System.BitConverter.ToInt64(barray, 0);
            }

            return 0;
        }
        #endregion
    }
}