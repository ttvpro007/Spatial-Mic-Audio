// (c) 2022-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using UnityEngine;

namespace AudioStreamSupport
{
    public static class Platform
    {
        // ========================================================================================================================================
        #region Platform
        /// <summary>
        /// returns true if the project is running on 64-bit architecture, false if 32-bit 
        /// </summary>
        /// <returns></returns>
        public static bool Is64bitArchitecture()
        {
            int sizeOfPtr = System.Runtime.InteropServices.Marshal.SizeOf(typeof(System.IntPtr));
            return (sizeOfPtr > 4);
        }
        public static int mainThreadId;

        [RuntimeInitializeOnLoadMethod]
        static void InitPlatform()
        {
            Platform.mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }
        #endregion
    }
}