// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using UnityEngine;

namespace AudioStream
{
    // ========================================================================================================================================
    #region About
    /// <summary>
    /// About informational strings
    /// </summary>
    public static class About
    {
        public static string versionNumber = "3.2";
        public static string versionString = "AudioStream v " + About.versionNumber + " © 2016-2023 Martin Cvengros";
        public static string fmodNotice = ", uses FMOD by Firelight Technologies Pty Ltd";
        /// <summary>
        /// Log version at startup
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        public static void LogInitInfo()
        {
            Log.LOG(LogLevel.INFO, LogLevel.INFO, null, "AudioStream {0}", About.versionNumber);
        }
    }
    #endregion
}