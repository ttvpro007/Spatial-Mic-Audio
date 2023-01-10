// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using System;
using UnityEngine;

namespace AudioStreamSupport
{
    /// <summary>
    /// just LogLevel enum
    /// </summary>
    public enum LogLevel
    {
        ERROR = 0
            , WARNING = 1 << 0
            , INFO = 1 << 1
            , DEBUG = 1 << 2
    }
    public static class Log
    {
        /// <summary>
        /// Logs message based on log level
        /// </summary>
        /// <param name="requestedLogLevel"></param>
        /// <param name="currentLogLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public static void LOG(
            LogLevel requestedLogLevel
            , LogLevel currentLogLevel
            , string gameObjectName
            , string format
            , params object[] args
            )
        {
            if (requestedLogLevel == LogLevel.ERROR)
            {
                var time = DateTime.Now.ToString("s");

                // not with format from arbitrary unknown source - could lead to failed format parsing
                if (args.Length > 0)
                    Debug.LogErrorFormat(
                        gameObjectName + " [ERROR][" + time + "] " + format + "\r\n=======================================\r\n"
                        , args
                        );
                else
                    Debug.LogError(
                        gameObjectName + " [ERROR][" + time + "] " + format + "\r\n=======================================\r\n"
                        );
            }
            else if (currentLogLevel >= requestedLogLevel)
            {
                var time = DateTime.Now.ToString("s");

                if (requestedLogLevel == LogLevel.WARNING)
                    Debug.LogWarningFormat(
                        gameObjectName + " [WARNING][" + time + "] " + format + "\r\n=======================================\r\n"
                        , args);
                else
                    Debug.LogFormat(
                        gameObjectName + " [" + currentLogLevel + "][" + time + "] " + format + "\r\n=======================================\r\n"
                        , args);
            }
        }

        public static string TimeStringFromSeconds(double seconds)
        {
            // There are 10,000 ticks in a millisecond:
            var ticks = seconds * 1000 * 10000;
            var span = new TimeSpan((long)ticks);

            return string.Format("{0:D2}h : {1:D2}m : {2:D2}s : {3:D3}ms"
                , span.Hours
                , span.Minutes
                , span.Seconds
                , span.Milliseconds
                );
        }
     }
}