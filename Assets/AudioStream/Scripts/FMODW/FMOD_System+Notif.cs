// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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
            #region Notifications lifecycle
            /// <summary>
            /// (it's public to allow to be called at any time for 0 system)
            /// </summary>
            /// <param name="logLevel"></param>
            /// <param name="gameObjectName"></param>
            /// <param name="onError"></param>
            public void SetAsNotificationSystem(LogLevel logLevel
                , string gameObjectName
                , EventWithStringStringParameter onError
                )
            {
                // install notification callback for output 0
                // make it one per application
                if (this.outputDevicesChangedCallback == null)
                {
                    this.outputDevicesChangedCallback = new FMOD.SYSTEM_CALLBACK(FMOD_System.OutputDevicesChangedCallback);

                    // set callback for RECORDLISTCHANGED only
                    // - it looks like RECORDLISTCHANGED *ONLY* captures ALL changes also on output devices
                    // , DEVICELISTCHANGED IS NOT emitted for just e.g. swapping default output in general (probably since those interfaces can be also captured from?)

                    result = this.system.setCallback(this.outputDevicesChangedCallback
                        , FMOD.SYSTEM_CALLBACK_TYPE.RECORDLISTCHANGED
                        //| FMOD.SYSTEM_CALLBACK_TYPE.DEVICELISTCHANGED
                        //| FMOD.SYSTEM_CALLBACK_TYPE.DEVICELOST
                        );
                    FMODHelpers.ERRCHECK(result, logLevel, "FMODSystem_OutputMonitoring", onError, "system.setCallback");

                    Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Installed RECORDLISTCHANGED callback on driver {0} ", this.initialOutpuDevice.id);

                    this.isNotificationSystem = true;
                }
            }
            public void UnsetNotificationSystem(LogLevel logLevel
                , string gameObjectName
                , EventWithStringStringParameter onError
                )
            {
                // if system was already released (scene change) no point in unsetting this
                if (this.system.hasHandle()
                    )
                {
                    result = this.system.setCallback(null
                        , FMOD.SYSTEM_CALLBACK_TYPE.RECORDLISTCHANGED
                        //| FMOD.SYSTEM_CALLBACK_TYPE.DEVICELISTCHANGED
                        //| FMOD.SYSTEM_CALLBACK_TYPE.DEVICELOST
                        );
                    FMODHelpers.ERRCHECK(result, logLevel, "FMODSystem_OutputMonitoring", onError, "system.setCallback");

                    Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Uninstalled RECORDLISTCHANGED callback on driver {0} | {1} ", this.initialOutpuDevice.id, this.system.handle);

                    this.outputDevicesChangedCallback = null;
                }
            }
            #endregion
            // ========================================================================================================================================
            #region Devices changed notification
            public bool isNotificationSystem { get; protected set; }
            /// <summary>
            /// Keep the reference around..
            /// </summary>
            FMOD.SYSTEM_CALLBACK outputDevicesChangedCallback = null;
            /// <summary>
            /// AudioSourceOutputDevice instances added/removed via AddToNotifiedInstances/RemoveFromNotifiedInstances to be notified when system output device list changes
            /// </summary>
            readonly static HashSet<IntPtr> outputDevicesChangedNotify = new HashSet<IntPtr>();
            /// <summary>
            /// we should probably guard access to HashSet
            /// </summary>
            readonly static object notification_callback_lock = new object();
            public static void AddToNotifiedInstances(IntPtr notifiedInstance)
            {
                lock (FMOD_System.notification_callback_lock)
                {
                    FMOD_System.outputDevicesChangedNotify.Add(notifiedInstance);
                }
            }
            public static void RemoveFromNotifiedInstances(IntPtr notifiedInstance)
            {
                lock (FMOD_System.notification_callback_lock)
                {
                    FMOD_System.outputDevicesChangedNotify.Remove(notifiedInstance);
                }
            }
            /// <summary>
            /// is called immediately after the callback has been installed
            /// </summary>
            /// <param name="system"></param>
            /// <param name="type"></param>
            /// <param name="commanddata1"></param>
            /// <param name="commanddata2"></param>
            /// <param name="userdata"></param>
            /// <returns></returns>
            [AOT.MonoPInvokeCallback(typeof(FMOD.SYSTEM_CALLBACK))]
            static FMOD.RESULT OutputDevicesChangedCallback(IntPtr system, FMOD.SYSTEM_CALLBACK_TYPE type, IntPtr commanddata1, IntPtr commanddata2, IntPtr userdata)
            {
                lock (FMOD_System.notification_callback_lock)
                {
                    // Debug.LogFormat("emitting from {0}, type {1}, commanddata1 {2}, commanddata2 {3} userdata {4}, notifs: {5}", system, type, commanddata1, commanddata2, userdata, FMOD_System.outputDevicesChangedNotify.Count);

                    foreach (var instancePtr in FMOD_System.outputDevicesChangedNotify)
                    {
                        GCHandle objecthandle = GCHandle.FromIntPtr(instancePtr);
                        var audioStreamOutput = (objecthandle.Target as AudioStreamDevicesChangedNotify);
                        audioStreamOutput.notificationCallback = type;
                    }
                }

                return FMOD.RESULT.OK;
            }
            #endregion
        }
        #endregion
    }
}
