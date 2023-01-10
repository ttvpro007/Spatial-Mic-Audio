// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Sets up and automatically notifies Unity Event subscribers about changes of audio input/output devices in the system
    /// Uses a system for default (0) device for FMOD callbacks
    /// If/when default(0) device changes it tries to reacquire new notification system using the new default 
    /// </summary>
    public class AudioStreamDevicesChangedNotify : MonoBehaviour
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[Setup]")]
        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;
        #region Unity events
        [Header("[Events]")]
        public EventWithStringStringParameter OnError;
        [Tooltip("Fired when FMOD detects devices change in the system, and also once when the callback is installed (when the system is created).")]
        public EventWithStringParameter OnDevicesChanged;
        #endregion
        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        string gameObjectName = string.Empty;
        #endregion
        // ========================================================================================================================================
        #region FMOD
        /// <summary>
        /// System for output 0 which will be also notification system and used for devices enumeration - one per application, refcounted
        /// == outputdevice_system for output 0
        /// </summary>
        FMOD_SystemW.FMOD_System notification_system = null;
        protected FMOD.RESULT result = FMOD.RESULT.OK;
        FMOD.RESULT lastError = FMOD.RESULT.ERR_NOTREADY;
        /// <summary>
        /// 
        /// </summary>
        List<FMOD_SystemW.OUTPUT_DEVICE> outputDevices = new List<FMOD_SystemW.OUTPUT_DEVICE>();
        #endregion
        // ========================================================================================================================================
        #region Unity lifecycle
        /// <summary>
        /// handle to this' ptr for usedata in FMOD callbacks
        /// </summary>
        GCHandle gc_thisPtr;

        void Awake()
        {
            // multiple intances would probably not prevent anything, but are not necessary either
            var existingInstance = FindObjectsOfType<AudioStreamDevicesChangedNotify>();
            if (existingInstance.Length > 1)
                throw new NotImplementedException(string.Format("Please make sure there's max. 1 instance of {0} in the scene", nameof(AudioStreamDevicesChangedNotify)));
        }

        void Start()
        {
            this.gameObjectName = this.gameObject.name;

            // ignore notification resulting from installation of the new callback
            this.ignoreNotification = true;

            this.CreateNotificationSystem();
        }
        [HideInInspector()]
        /// <summary>
        /// Set in callback upon receiving the notification
        /// </summary>
        public FMOD.SYSTEM_CALLBACK_TYPE? notificationCallback;
        /// <summary>
        /// int flag to skip a notification immediately after installing new callback
        /// </summary>
        bool ignoreNotification = false;
        void Update()
        {
            if (this.notificationCallback.HasValue)
            {
                // ignore 1st notif immediately after the callback installation
                if (this.ignoreNotification)
                {
                    Log.LOG(LogLevel.INFO, this.logLevel, this.gameObjectName, "Ignoring {0} notification", this.notificationCallback.Value);
                    this.ignoreNotification = false;
                    this.notificationCallback = null;
                }
                else
                {
                    // to properly capture new devices state all running systems have to be released
                    // -> 1] stop all running sounds + release all systems, including notification system
                    // -> 2] restart them (+ let them figure where to play), reinstate the notification system

                    Log.LOG(LogLevel.INFO, this.logLevel, this.gameObjectName, "Processing {0} notification", this.notificationCallback.Value);

                    // although running sounds are switched by FMOD automatically, output drivers ids are not
                    // all components notified from here will either recreate their FMOD systems to capture new outputs state or just try to setDriver on new output in case of *Minimal components (which might fail)
                    //      notify AudioStreamBase components - this will include ASODs attached to AudioStreamBase game objects and AudioStreamMinimal game object (which don't have ASOD attached)
                    //      notify all ASODs attached directly to an AudioSource/Listener i.e. without sibling AudioStream*
                    //      notify all MediaSourceOutputDevice components
                    //      notify all input components

                    var astreambases = FindObjectsOfType<AudioStreamBase>()
                        .ToList() // ... needs to be a copy, so it won't be enumerated dynamically later
                        ;

                    var asods = FindObjectsOfType<AudioSourceOutputDevice>()
                        .Where(w => !astreambases.Contains(w.gameObject.GetComponent<AudioStreamBase>())
                        )
                        .ToList() // ... needs to be a copy, so it won't be enumerated dynamically later
                        ;

                    var msods = FindObjectsOfType<MediaSourceOutputDevice>()
                        .ToList() // ... needs to be a copy, so it won't be enumerated dynamically later
                        ;

                    var asis = FindObjectsOfType<AudioStreamInputBase>()
                        .ToList()
                        ;

                    // 1] - stop & release

                    // release instance on (potentially) old 0
                    this.ReleaseNotificationSystem();

                    foreach (var asod in asods)
                        asod.ReflectOutput_Start();

                    foreach (var astream in astreambases)
                        astream.ReflectOutput_Start();

                    foreach (var msod in msods)
                        msod.ReflectOutput_Start();

                    foreach (var asi in asis)
                        asi.ReflectInput_Start();

                    // 2] restart

                    // ignore notification resulting from installation of the new callback
                    // this.ignoreNotification = true;

                    // install new system on (potentially) new 0
                    this.CreateNotificationSystem();

                    // get new updated devices list
                    var newOutputs = FMOD_SystemW.AvailableOutputs(this.logLevel, this.gameObjectName, this.OnError);
                    this.outputDevices = newOutputs;

                    foreach (var asod in asods)
                        asod.ReflectOutput_Finish(newOutputs);

                    foreach (var astream in astreambases)
                        astream.ReflectOutput_Finish(newOutputs);

                    foreach (var msod in msods)
                        msod.ReflectOutput_Finish(newOutputs);

                    foreach (var asi in asis)
                        asi.ReflectInput_Finish();

                    this.notificationCallback = null;

                    if (this.OnDevicesChanged != null)
                        this.OnDevicesChanged.Invoke(this.gameObjectName);
                }
            }

            if (this.notification_system != null
                && this.notification_system.SystemHandle != IntPtr.Zero
                )
            {
                result = this.notification_system.Update();
                ERRCHECK(result, "notification_system.update", false);
            }
        }
        void OnDestroy()
        {
            this.ReleaseNotificationSystem();
        }
        #endregion
        // ========================================================================================================================================
        #region
        /// <summary>
        /// 
        /// </summary>
        void CreateNotificationSystem()
        {
            // FMODSystemsManager.InitializeFMODDiagnostics(FMOD.DEBUG_FLAGS.LOG);
            this.gc_thisPtr = GCHandle.Alloc(this);

            uint dspBufferLength, dspBufferCount;
            // create new fmod system for enumerating available output drivers and registering output devices changes notification
            this.notification_system = FMOD_SystemW.FMOD_System_Create(
                0
                , true
                , this.logLevel
                , this.gameObjectName
                , this.OnError
                , out dspBufferLength
                , out dspBufferCount
                );

            this.notification_system.SetAsNotificationSystem(this.logLevel, this.gameObjectName, this.OnError);

            // add this to be notified
            FMOD_SystemW.FMOD_System.AddToNotifiedInstances(GCHandle.ToIntPtr(this.gc_thisPtr));

            this.lastError = FMOD.RESULT.OK;
            // Debug.LogFormat(@"Created notif on {0} {1}", this.notification_system.forOutpuDevice.name, this.notification_system.forOutpuDevice.guid);
        }
        /// <summary>
        /// 
        /// </summary>
        void ReleaseNotificationSystem()
        {
            // release notification system
            FMOD_SystemW.FMOD_System.RemoveFromNotifiedInstances(GCHandle.ToIntPtr(this.gc_thisPtr));

            if (this.notification_system != null)
            {
                this.notification_system.UnsetNotificationSystem(this.logLevel, this.gameObjectName, this.OnError);
                FMOD_SystemW.FMOD_System_Release(ref this.notification_system, this.logLevel, this.gameObjectName, this.OnError);
            }

            if (this.gc_thisPtr.IsAllocated)
                this.gc_thisPtr.Free();
        }
        #endregion
        // ========================================================================================================================================
        #region Support
        void ERRCHECK(FMOD.RESULT result, string customMessage, bool throwOnError = true)
        {
            this.lastError = result;

            FMODHelpers.ERRCHECK(result, this.logLevel, this.gameObjectName, this.OnError, customMessage, throwOnError);
        }
        public string GetLastError(out FMOD.RESULT errorCode)
        {
            errorCode = this.lastError;

            return FMOD.Error.String(errorCode);
        }
        /// <summary>
        /// UI notification
        /// </summary>
        /// <returns></returns>
        public bool IsBeingNotified()
        {
            return this.notificationCallback.HasValue;
        }
        #endregion
    }
}