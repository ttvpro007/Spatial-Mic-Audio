// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System;
using System.Collections.Generic;

namespace AudioStream
{
    public static class FMODHelpers
    {
        /*
         * ERRCHECK has FMOD dependency - moved to separate file
         */

        /// <summary>
        /// Checks FMOD result and either throws an exception with error message, or logs error message
        /// Log requires game object's current log level, name and error event handler
        /// TODO: !thread safe because of event handler
        /// </summary>
        /// <param name="result"></param>
        /// <param name="currentLogLevel"></param>
        /// <param name="gameObjectName"></param>
        /// <param name="onError"></param>
        /// <param name="customMessage"></param>
        /// <param name="throwOnError"></param>
        public static void ERRCHECK(
            FMOD.RESULT result
            , LogLevel currentLogLevel
            , string gameObjectName
            , EventWithStringStringParameter onError
            , string customMessage
            , bool throwOnError = true
            )
        {
            if (result != FMOD.RESULT.OK)
            {
                var m = string.Format("{0} {1} - {2}", customMessage, result, FMOD.Error.String(result));

                if (onError != null && Platform.mainThreadId == System.Threading.Thread.CurrentThread.ManagedThreadId)
                    onError.Invoke(gameObjectName, m);

                if (throwOnError)
                    throw new System.Exception(m);
                else
                    Log.LOG(LogLevel.ERROR, currentLogLevel, gameObjectName, m);
            }
            else
            {
                Log.LOG(LogLevel.DEBUG, currentLogLevel, gameObjectName, "{0} {1} - {2}", customMessage, result, FMOD.Error.String(result));
            }
        }

        // ========================================================================================================================================
        #region FMOD helpers
        /// <summary>
        /// Gets string from native pointer - uses adapted FMOD StringHelper, which is not public
        /// (At the time - around 1.10.04 (?) - also worked around early exit bug in stringFromNative which is since fixed)
        /// </summary>
        /// <param name="nativePtr">pointer to the string</param>
        /// <param name="nativeLen">bytes the string occupies in memory</param>
        /// <returns></returns>
        public static string StringFromNative(IntPtr nativePtr, out uint bytesRead)
        {
            string result = string.Empty;
            int nativeLength = 0;

            using (StringHelper.ThreadSafeEncoding encoder = StringHelper.GetFreeHelper())
            {
                result = encoder.stringFromNative(nativePtr, out nativeLength);
            }

            bytesRead = (uint)nativeLength;

            return result;
        }
        /// <summary>
        /// Adapted based on StringHelper from fmod.cs since - as of FMOD 1.10.10 - it's not public, and we need bytes count the string occupies, too
        /// </summary>
        static class StringHelper
        {
            public class ThreadSafeEncoding : IDisposable
            {
                System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
                byte[] encodedBuffer = new byte[128];
                char[] decodedBuffer = new char[128];
                bool inUse;

                public bool InUse() { return inUse; }
                public void SetInUse() { inUse = true; }

                private int roundUpPowerTwo(int number)
                {
                    int newNumber = 1;
                    while (newNumber <= number)
                    {
                        newNumber *= 2;
                    }

                    return newNumber;
                }

                public byte[] byteFromStringUTF8(string s)
                {
                    if (s == null)
                    {
                        return null;
                    }

                    int maximumLength = encoding.GetMaxByteCount(s.Length) + 1; // +1 for null terminator
                    if (maximumLength > encodedBuffer.Length)
                    {
                        int encodedLength = encoding.GetByteCount(s) + 1; // +1 for null terminator
                        if (encodedLength > encodedBuffer.Length)
                        {
                            encodedBuffer = new byte[roundUpPowerTwo(encodedLength)];
                        }
                    }

                    int byteCount = encoding.GetBytes(s, 0, s.Length, encodedBuffer, 0);
                    encodedBuffer[byteCount] = 0; // Apply null terminator

                    return encodedBuffer;
                }

                public string stringFromNative(IntPtr nativePtr, out int nativeLen)
                {
                    nativeLen = 0;

                    if (nativePtr == IntPtr.Zero)
                    {
                        return "";
                    }

                    while (System.Runtime.InteropServices.Marshal.ReadByte(nativePtr, nativeLen) != 0)
                    {
                        nativeLen++;
                    }

                    if (nativeLen == 0)
                    {
                        return "";
                    }

                    if (nativeLen > encodedBuffer.Length)
                    {
                        encodedBuffer = new byte[roundUpPowerTwo(nativeLen)];
                    }

                    System.Runtime.InteropServices.Marshal.Copy(nativePtr, encodedBuffer, 0, nativeLen);

                    int maximumLength = encoding.GetMaxCharCount(nativeLen);
                    if (maximumLength > decodedBuffer.Length)
                    {
                        int decodedLength = encoding.GetCharCount(encodedBuffer, 0, nativeLen);
                        if (decodedLength > decodedBuffer.Length)
                        {
                            decodedBuffer = new char[roundUpPowerTwo(decodedLength)];
                        }
                    }

                    int charCount = encoding.GetChars(encodedBuffer, 0, nativeLen, decodedBuffer, 0);

                    return new String(decodedBuffer, 0, charCount);
                }

                public void Dispose()
                {
                    lock (encoders)
                    {
                        inUse = false;
                    }
                }
            }

            static List<ThreadSafeEncoding> encoders = new List<ThreadSafeEncoding>(1);

            public static ThreadSafeEncoding GetFreeHelper()
            {
                lock (encoders)
                {
                    ThreadSafeEncoding helper = null;
                    // Search for not in use helper
                    for (int i = 0; i < encoders.Count; i++)
                    {
                        if (!encoders[i].InUse())
                        {
                            helper = encoders[i];
                            break;
                        }
                    }
                    // Otherwise create another helper
                    if (helper == null)
                    {
                        helper = new ThreadSafeEncoding();
                        encoders.Add(helper);
                    }
                    helper.SetInUse();
                    return helper;
                }
            }
        }
        #endregion
    }
}
