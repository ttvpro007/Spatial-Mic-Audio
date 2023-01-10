// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System.Collections.Generic;

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
        }
        #endregion
        // ========================================================================================================================================
        #region Input enumeration
        /// <summary>
        /// output info from getRecordDriverInfo
        /// </summary>
        public struct INPUT_DEVICE
        {
            public int id;
            public string name;
            public System.Guid guid;
            public int samplerate;
            public FMOD.SPEAKERMODE speakermode;
            public int channels;
            public bool isDefault;
        }
        /// <summary>
        /// Enumerates available audio inputs in the system 0 and returns them as ordered list
        /// </summary>
        /// <param name="logLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        /// <param name="includeLoopbackInterfaces"></param>
        /// <returns></returns>
        public static List<INPUT_DEVICE> AvailableInputs(LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            , bool includeLoopbackInterfaces = true)
        {
            // (make sure to not throw an exception anywhere so the system is always released)
            var fmodsystem = FMOD_SystemW.FMODSystem0_Create(logLevel, gameObjectName, onError);

            var result = fmodsystem.Update();
            FMODHelpers.ERRCHECK(result, LogLevel.ERROR, "FMODSystemsManager.FMODSystemInputDevice", null, "fmodsystem.Update", false);

            List<INPUT_DEVICE> availableDrivers = new List<INPUT_DEVICE>();

            /*
            Enumerate record devices
            */
            int numAllDrivers = 0;
            int numConnectedDrivers = 0;
            result = fmodsystem.system.getRecordNumDrivers(out numAllDrivers, out numConnectedDrivers);
            FMODHelpers.ERRCHECK(result, LogLevel.ERROR, "FMODSystemsManager.FMODSystemInputDevice", null, "fmodsystem.system.getRecordNumDrivers", false);

            for (int i = 0; i < numAllDrivers; ++i)
            {
                int recChannels;
                int recRate;
                int namelen = 255;
                string name;
                System.Guid guid;
                FMOD.SPEAKERMODE speakermode;
                FMOD.DRIVER_STATE driverstate;
                result = fmodsystem.system.getRecordDriverInfo(i, out name, namelen, out guid, out recRate, out speakermode, out recChannels, out driverstate);
                FMODHelpers.ERRCHECK(result, LogLevel.ERROR, "FMODSystemsManager.FMODSystemInputDevice", null, "fmodsystem.system.getRecordDriverInfo", false);

                if (result != FMOD.RESULT.OK)
                {
                    Log.LOG(LogLevel.ERROR, logLevel, gameObjectName, "!error input {0} guid: {1} systemrate: {2} speaker mode: {3} channels: {4} state: {5}"
                        , name
                        , guid
                        , recRate
                        , speakermode
                        , recChannels
                        , driverstate
                        );

                    continue;
                }

                // hardcoded string added by FMOD to the adapter name
                var isLoopback = name.ToLowerInvariant().EndsWith("[loopback]");
                var addInterface = includeLoopbackInterfaces ? true : !isLoopback;

                addInterface &= ((driverstate & FMOD.DRIVER_STATE.CONNECTED) == FMOD.DRIVER_STATE.CONNECTED)
                    || ((driverstate & FMOD.DRIVER_STATE.DEFAULT) == FMOD.DRIVER_STATE.DEFAULT)
                    ;

                if (addInterface)
                {
                    availableDrivers.Add(new INPUT_DEVICE() { id = i, name = name, guid = guid, samplerate = recRate, speakermode = speakermode, channels = recChannels, isDefault = (driverstate & FMOD.DRIVER_STATE.DEFAULT) == FMOD.DRIVER_STATE.DEFAULT });
                }

                Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "{0} guid: {1} systemrate: {2} speaker mode: {3} channels: {4} state: {5} - {6}"
                    , name
                    , guid
                    , recRate
                    , speakermode
                    , recChannels
                    , driverstate
                    , addInterface ? "ADDED" : "SKIPPED - IS LOOPBACK"
                    );
            }

            // release
            FMOD_SystemW.FMODSystem0_Release(ref fmodsystem, logLevel, gameObjectName, onError);

            return availableDrivers;
        }
        #endregion
    }
}
