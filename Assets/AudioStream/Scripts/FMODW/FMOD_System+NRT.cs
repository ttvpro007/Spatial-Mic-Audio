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
        }
        #endregion
        // ========================================================================================================================================
        #region NRT system
        struct RefC_FMOD_System_NRT
        {
            public FMOD_System FMOD_System;
            public uint refcount;
        }
        /// <summary>
        /// Output Device <-> FMOD System
        /// </summary>
        static RefC_FMOD_System_NRT refC_FMOD_System_NRT = new RefC_FMOD_System_NRT();

        public static FMOD_System FMOD_System_NRT_Create(
            LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            FMOD_System result = null;

            if (FMOD_SystemW.refC_FMOD_System_NRT.refcount == 0)
            {
                uint dspBufferLength, dspBufferCount;
                result = new FMOD_System(0, false, logLevel, gameObjectName, onError, out dspBufferLength, out dspBufferCount);
                FMOD_SystemW.refC_FMOD_System_NRT = new RefC_FMOD_System_NRT() { FMOD_System = result, refcount = 1 };

                Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Created FMOD system for non realtime decoding {0}", FMOD_SystemW.refC_FMOD_System_NRT.FMOD_System.SystemHandle);
            }
            else
            {
                result = FMOD_SystemW.refC_FMOD_System_NRT.FMOD_System;
                var refc = FMOD_SystemW.refC_FMOD_System_NRT.refcount + 1;
                FMOD_SystemW.refC_FMOD_System_NRT = new RefC_FMOD_System_NRT() { FMOD_System = result, refcount = refc };
                Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Retrieved FMOD system for non realtime decoding {0} / {1}", FMOD_SystemW.refC_FMOD_System_NRT.FMOD_System.SystemHandle, FMOD_SystemW.refC_FMOD_System_NRT.refcount);
            }

            return result;
        }
        public static void FMOD_System_NRT_Release(ref FMOD_System fmodsystem
            , LogLevel logLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            )
        {
            if (fmodsystem == null)
                return;

            if (fmodsystem != FMOD_SystemW.refC_FMOD_System_NRT.FMOD_System)
                Debug.LogErrorFormat("NRT system {0} being released was not previously created via FMOD_System_NRT_Create", fmodsystem.SystemHandle);

            var refc = FMOD_SystemW.refC_FMOD_System_NRT.refcount;

            if (refc < 1)
                Debug.LogWarningFormat("System is being overreleased");

            if (--refc < 1)
            {
                fmodsystem.Release(logLevel, gameObjectName, onError);
                Log.LOG(LogLevel.INFO, logLevel, gameObjectName, "Released system for non realtime decoding {0}", fmodsystem.SystemHandle);
                fmodsystem = null;

                FMOD_SystemW.refC_FMOD_System_NRT = new RefC_FMOD_System_NRT();
            }
            else
            {
                FMOD_SystemW.refC_FMOD_System_NRT = new RefC_FMOD_System_NRT() { FMOD_System = FMOD_SystemW.refC_FMOD_System_NRT.FMOD_System, refcount = refc };
            }
        }
        #endregion
    }
}
