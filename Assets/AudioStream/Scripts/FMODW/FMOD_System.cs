// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System;
using System.Collections.Generic;
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
        #region RT systems per output
        struct RefC_FMOD_System
        {
            public FMOD_System FMOD_System;
            public uint refcount;
        }
        /// <summary>
        /// Output Device <-> FMOD System
        /// </summary>
        readonly static Dictionary<int, RefC_FMOD_System> systems4devices = new Dictionary<int, RefC_FMOD_System>();

        public static FMOD_System FMOD_System_Create(int forOutputDriver
            , bool realtime
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            , out uint dspBufferLength
            , out uint dspNumBUffers
            )
        {
            // TODO: probably check for configuration changes (output spaker mode, DSP buffers when added.. )
            // - need to release existing system and create new one in that case, too

            if (FMOD_SystemW.systems4devices.TryGetValue(forOutputDriver, out var refc_system))
            {
                // is this pointless but fill output values at least with correct ones..
                uint bufferLength;
                int numBuffers;
                refc_system.FMOD_System.system.getDSPBufferSize(out bufferLength, out numBuffers);

                dspBufferLength = bufferLength;
                dspNumBUffers = (uint)numBuffers;

                var system = new RefC_FMOD_System() { FMOD_System = refc_system.FMOD_System, refcount = ++refc_system.refcount };
                FMOD_SystemW.systems4devices[forOutputDriver] = system;

                Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Retrieved FMOD system {0} / {1} [{2}]", system.FMOD_System.system.handle, forOutputDriver, system.refcount);

                return system.FMOD_System;
            }
            else
            {
                var system = new FMOD_System(forOutputDriver, realtime, logLevel, gameObjectName, onError, out dspBufferLength, out dspNumBUffers);
                systems4devices.Add(forOutputDriver, new RefC_FMOD_System() { FMOD_System = system, refcount = 1 });

                Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Created FMOD system {0} / {1} [{2}]", system.system.handle, forOutputDriver, 1);

                return system;
            }
        }
        /// <summary>
        /// Manual system release
        /// </summary>
        /// <param name="fmodsystem"></param>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        public static void FMOD_System_Release(ref FMOD_System fmodsystem
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            if (fmodsystem == null)
            {
                Log.LOG(LogLevel.WARNING, logLevel, gameObjectName, "Already released ({0})", fmodsystem);
                return;
            }

            var driverId = fmodsystem.initialOutpuDevice.id;

            if (FMOD_SystemW.systems4devices.TryGetValue(driverId, out var refc_system))
            {
                if (refc_system.refcount < 1)
                    Debug.LogWarningFormat("System is being overreleased");

                if (fmodsystem != refc_system.FMOD_System)
                    Debug.LogErrorFormat("System being released was not previously created via manager");

                if (--refc_system.refcount < 1)
                {
                    refc_system.FMOD_System.Release(logLevel, gameObjectName, onError);

                    FMOD_SystemW.systems4devices.Remove(driverId);

                    Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Released system {0} / {1}", fmodsystem.system.handle, driverId);

                    fmodsystem = null;
                }
                else
                {
                    var system = new RefC_FMOD_System() { FMOD_System = refc_system.FMOD_System, refcount = refc_system.refcount };
                    FMOD_SystemW.systems4devices[driverId] = system;

                    Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Decreased refcount on system {0} / {1} [{2}]", fmodsystem.system.handle, driverId, refc_system.refcount);
                }
            }
            else
            {
                Debug.LogErrorFormat("System being released was not previously created via manager");

                // although 'overreleasing' fmod call is harmless, the system has to be around until last release is requested
                // parameter _can_ be null though
                if (fmodsystem != null)
                {
                    fmodsystem.Release(logLevel, gameObjectName, onError);
                    Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Released system {0} for {1}", fmodsystem.system.handle, driverId);
                }
            }
        }
        #endregion
        // ========================================================================================================================================
        #region shotcuts for enumerations
        static FMOD_System FMODSystem0_Create(LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            uint dspBufferLength, dspNumBUffers;
            return FMOD_SystemW.FMOD_System_Create(0
                , true
                , logLevel
                , gameObjectName
                , onError
                , out dspBufferLength
                , out dspNumBUffers
                );
        }
        static void FMODSystem0_Release(ref FMOD_System nsystem
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            FMOD_SystemW.FMOD_System_Release(ref nsystem, logLevel, gameObjectName, onError);
            nsystem = null;
        }
        #endregion
        // ========================================================================================================================================
        #region FMOD system object
        /// <summary>
        /// FMOD system object wrapper
        /// single refcounted instance of a FMOD system object per given output
        /// </summary>
        public partial class FMOD_System
        {
            /// <summary>
            /// max. virtual channels for init
            /// https://www.fmod.com/docs/2.01/api/core-api-system.html#system_init
            /// </summary>
            const int MAX_V_CHANNELS = 4095;

            public readonly FMOD.System system;
            public readonly string VersionString;
            /// <summary>
            /// FMOD's sytem handle (contrary to sound handle it seems) is completely unreliable / e.g. clearing it via .clearHandle() has no effect in following check for !null/hasHandle() /
            /// Use this pointer copied after creation as release/functionality guard instead
            /// </summary>
            public System.IntPtr SystemHandle = global::System.IntPtr.Zero;
            FMOD.RESULT result = FMOD.RESULT.OK;
            /// <summary>
            /// initial output driver system was created with/for
            /// </summary>
            public readonly OUTPUT_DEVICE initialOutpuDevice;
            // constructor doesn't throw to be able to release even partially initialized system
            /// <summary>
            /// 
            /// </summary>
            /// <param name="forOutputDriver"></param>
            /// <param name="realtime">(implies recording support - OPENSL on Android)</param>
            /// <param name="logLevel"></param>
            /// <param name="gameObjectName"></param>
            /// <param name="onError"></param>
            /// <param name="dspBufferLength"></param>
            /// <param name="dspNumBUffers"></param>
            public FMOD_System(int forOutputDriver
                , bool realtime
                , LogLevel logLevel
                , string gameObjectName
                , EventWithStringStringParameter onError
                , out uint dspBufferLength
                , out uint dspNumBUffers
                )
            {
                /*
                Create a System object and initialize.
                */
                uint version = 0;
                this.isNotificationSystem = false;

                result = FMOD.Factory.System_Create(out this.system);
                // store even unsusccessfully created system handle to be able to release
                this.SystemHandle = this.system.handle;
                FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "Factory.System_Create", false);

                result = system.getVersion(out version);
                FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "system.getVersion", false);

                if (version < FMOD.VERSION.number)
                {
                    var msg = string.Format("FMOD lib version {0} doesn't match header version {1}", version, FMOD.VERSION.number);
                    Log.LOG(LogLevel.ERROR, logLevel, gameObjectName, msg);

                    if (onError != null)
                        onError.Invoke(gameObjectName, msg);

                    dspBufferLength =
                        dspNumBUffers =
                        0;

                    return;
                }

                /*
                    FMOD version number: 0xaaaabbcc -> aaaa = major version number.  bb = minor version number.  cc = development version number.
                */
                var versionString = System.Convert.ToString(version, 16).PadLeft(8, '0');
                this.VersionString = string.Format("{0}.{1}.{2}", System.Convert.ToUInt32(versionString.Substring(0, 4)), versionString.Substring(4, 2), versionString.Substring(6, 2));

                /*
                 * ALL before init - output type, + dsp buffers, speaker mode
                */

                // setOutput:
                // must be be4 init on iOS and w/ ASIO

                // setDSPBufferSize:
                // This function cannot be called after FMOD is already activated with System::init.
                // It must be called before System::init, or after System::close.
                // https://fmod.com/docs/2.02/api/core-api-system.html#system_setdspbuffersize

                // setSoftwareFormat:
                // This function must be called before System::init, or after System::close.
                // https://fmod.com/docs/2.02/api/core-api-system.html#system_setsoftwareformat

                var outputType = FMOD.OUTPUTTYPE.AUTODETECT;
                var initFlags = FMOD.INITFLAGS.NORMAL;

                var sampleRate = 0;
                var speakerMode = FMOD.SPEAKERMODE.DEFAULT;
                int numOfRawSpeakers = 0;
                uint DSP_bufferLength = 0;
                int DSP_numBuffers = 0;

                var devicesconfigs = DevicesConfiguration.Instance.devicesConfiguration;

                // realtime - playback - systems are all using normal/default output type:
                //      they're either direct, or running in realtime from custom file system
                // FMOD 2.02.04 NOSOUND (previously for AS w/ capture DSP) exhibited buggy behaviour when running excessively fast after initial (~1 min) period [thus draining dl buffer]
                // TODO: report ?
                // so NRT system only has no sound output
                // , RT systems which don't want to output audio use channel volume to silence it
                if (realtime)
                {
                    // user customization of the output
                    if (devicesconfigs.Count > forOutputDriver)
                    {
                        sampleRate = devicesconfigs[forOutputDriver].sampleRate;
                        speakerMode = devicesconfigs[forOutputDriver].SPEAKERMODE;
                        numOfRawSpeakers = devicesconfigs[forOutputDriver].NumOfRawSpeakers;
                        DSP_bufferLength = devicesconfigs[forOutputDriver].DSP_bufferLength;
                        DSP_numBuffers = devicesconfigs[forOutputDriver].DSP_numBuffers;

                        Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Using user override for output device {0}: samplerate: {1}, speaker mode: {2}, no. of speakers: {3}, DSP length: {4}, DSP buffers: {5}", forOutputDriver, sampleRate, speakerMode, numOfRawSpeakers, DSP_bufferLength, DSP_numBuffers);
                    }

                    // ASIO also implies Windows only
                    if (DevicesConfiguration.Instance.ASIO)
                    {
                        outputType = FMOD.OUTPUTTYPE.ASIO;
                        DSP_bufferLength = DevicesConfiguration.Instance.ASIO_bufferSize;
                        DSP_numBuffers = DevicesConfiguration.Instance.ASIO_bufferCount;
                    }

#if UNITY_ANDROID && !UNITY_EDITOR
                    // For recording to work on Android OpenSL support is needed:
                    outputType = FMOD.OUTPUTTYPE.OPENSL;
#endif
                }
                else
                {
                    outputType = FMOD.OUTPUTTYPE.NOSOUND_NRT;
                    // update/mix is driven by (hopefully fast) update loop
                    initFlags = FMOD.INITFLAGS.MIX_FROM_UPDATE | FMOD.INITFLAGS.STREAM_FROM_UPDATE;

                    // DSP buffers setup directly affects processing speed: are increased as much as possible
                    // these are empirically found nunmbers..
                    // TODO: do something with it to be user settable ?
                    DSP_bufferLength = 8192;
                    DSP_numBuffers = 2;
                }

                result = system.setOutput(outputType);
                FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "system.setOutput", false);

#if UNITY_ANDROID && !UNITY_EDITOR
                // For recording to work on Android OpenSL support is needed:
                // https://www.fmod.org/questions/question/is-input-recording-supported-on-android/
                if (result != FMOD.RESULT.OK)
                {
                    var msg = "OpenSL ES (OpenSL) support needed for recording not available.";

                    Log.LOG(LogLevel.ERROR, logLevel, gameObjectName, msg);

                    if (onError != null)
                        onError.Invoke(gameObjectName, msg);

                    dspBufferLength =
                        dspNumBUffers =
                        0;

                    return;
                }
#endif
                if (DSP_bufferLength > 0
                    || DSP_numBuffers > 0
                    )
                {
                    result = system.setDSPBufferSize(DSP_bufferLength, DSP_numBuffers);
                    FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "system.setDSPBufferSize", false);
                }

                if (sampleRate == 0)
                    sampleRate = AudioSettings.outputSampleRate;

                if (sampleRate > 0
                    || speakerMode != FMOD.SPEAKERMODE.DEFAULT
                    || numOfRawSpeakers > 0
                    )
                {
                    result = system.setSoftwareFormat(sampleRate, speakerMode, numOfRawSpeakers);
                    FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "system.setSoftwareFormat", false);
                }


                /*
                System initialization
                */
                result = system.init(MAX_V_CHANNELS, initFlags, System.IntPtr.Zero);
                FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "system.init", false);

                if (forOutputDriver > 0)
                {
                    Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Setting output to driver {0} ", forOutputDriver);
                    result = system.setDriver(forOutputDriver);
                    FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "system.setDriver", false);
                }


                // retrieve & log effective DSP used
                uint bufferLength;
                int numBuffers;

                result = system.getDSPBufferSize(out bufferLength, out numBuffers);
                FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "system.getDSPBufferSize", false);

                dspBufferLength = bufferLength;
                dspNumBUffers = (uint)numBuffers;

                Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Effective FMOD DSP buffer: {0} length, {1} buffers", bufferLength, numBuffers);



                // retrieve output driver info
                int od_namelen = 255;
                string od_name;
                System.Guid od_guid;
                int od_systemrate;
                FMOD.SPEAKERMODE od_speakermode;
                int od_speakermodechannels;

                result = this.system.getDriverInfo(forOutputDriver, out od_name, od_namelen, out od_guid, out od_systemrate, out od_speakermode, out od_speakermodechannels);
                FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "this.System.getDriverInfo", false);

                Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Output device {0} info: Output samplerate: {1}, speaker mode: {2}, num. of raw speakers: {3}", forOutputDriver, od_systemrate, od_speakermode, od_speakermodechannels);

                this.initialOutpuDevice = new OUTPUT_DEVICE()
                {
                    id = forOutputDriver,
                    name = od_name,
                    guid = od_guid,
                    samplerate = od_systemrate,
                    speakermode = od_speakermode,
                    channels = od_speakermodechannels
                };
            }
            /// <summary>
            /// Close and release for system
            /// </summary>
            public void Release(LogLevel logLevel
                , string gameObjectName
                , EventWithStringStringParameter onError
                )
            {
                if (this.SystemHandle != global::System.IntPtr.Zero)
                {
                    result = system.close();
                    FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "system.close", false);

                    result = system.release();
                    FMODHelpers.ERRCHECK(result, logLevel, gameObjectName, onError, "system.release", false);

                    // unreliable - e.g. the debug log afterwards still prints !IntPtr.Zero
                    system.clearHandle();
                    // Debug.Log(System.handle);

                    this.SystemHandle = global::System.IntPtr.Zero;
                }
            }
            /// <summary>
            /// Call continuosly (i.e. from Unity Update / OnAudioFilterRead *)
            /// </summary>
            public FMOD.RESULT Update()
            {
                if (this.SystemHandle != global::System.IntPtr.Zero)
                {
                    return this.system.update();
                }
                else
                {
                    return FMOD.RESULT.ERR_INVALID_HANDLE;
                }
            }
        }
        #endregion
        // ========================================================================================================================================
        #region FMOD diagnostics - For debugging only - ! *UNSTABLE* with more than one system
        [AOT.MonoPInvokeCallback(typeof(FMOD.DEBUG_CALLBACK))]
        static FMOD.RESULT DEBUG_CALLBACK(FMOD.DEBUG_FLAGS flags, IntPtr file, int line, IntPtr func, IntPtr message)
        {
            // TODO: verify string marshalling
            var message_s = new FMOD.StringWrapper(message);
            if (!message_s.ToString().Contains("FMOD_RESULT = 63")) // missing tags in stream is reported for every frame
            {
                var file_s = new FMOD.StringWrapper(file);
                var func_s = new FMOD.StringWrapper(func);
                Debug.LogFormat("{0} {1}:{2} {3} {4}", flags, System.IO.Path.GetFileName(file_s), line, func_s, message_s);
            }

            return FMOD.RESULT.OK;
        }

        static FMOD.DEBUG_CALLBACK DEBUG_CALLBACK_DELEGATE = null;

        public static void InitializeFMODDiagnostics(FMOD.DEBUG_FLAGS flags)
        {
            if (FMOD_SystemW.DEBUG_CALLBACK_DELEGATE == null)
            {
                FMOD_SystemW.DEBUG_CALLBACK_DELEGATE = new FMOD.DEBUG_CALLBACK(FMOD_SystemW.DEBUG_CALLBACK);

                Debug.LogFormat("new FMOD_SystemW.DEBUG_CALLBACK_DELEGATE {0}", FMOD_SystemW.DEBUG_CALLBACK_DELEGATE);

                var result = FMOD.Debug.Initialize(flags
                    , FMOD.DEBUG_MODE.CALLBACK
                    , FMOD_SystemW.DEBUG_CALLBACK_DELEGATE
                    , null
                    );

                if (result != FMOD.RESULT.OK)
                    Debug.LogErrorFormat("InitializeFMODDiagnostics - {0} {1}", result, FMOD.Error.String(result));
            }
        }
        #endregion
    }
}
