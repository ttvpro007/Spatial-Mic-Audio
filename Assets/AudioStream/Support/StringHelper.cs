// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace AudioStreamSupport
{
    public static class StringHelper
    {
        /// <summary>
        /// Tries to guess text encoding from a stream of bytes
        /// </summary>
        /// <param name="fromBytes"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public static Encoding GuessEncoding(byte[] fromBytes, out string text)
        {
            var ms = new MemoryStream(fromBytes);
            var sr = new StreamReader(ms);
            var enc = sr.CurrentEncoding;
            text = enc.GetString(fromBytes);

            return enc;
        }
        /// <summary>
        /// Filesystem friendly name for an URI/link
        /// </summary>
        /// <param name="ofUri"></param>
        /// <returns></returns>
        public static string EscapedBase64Hash(string ofUri)
        {
            var byteArray = ofUri.ToCharArray().Select(s => (byte)s).ToArray<byte>();

            using (var sha = System.Security.Cryptography.SHA512.Create())
            {
                var hash = sha.ComputeHash(byteArray);

                return Uri.EscapeDataString(
                    Convert.ToBase64String(hash)
                    );
            }
        }

        // ========================================================================================================================================
        #region Marshaling helpers
        // CC from Mono P/Invoke page (https://www.mono-project.com/docs/advanced/pinvoke/)
        public static string PtrToString(IntPtr p)
        {
            // TODO: deal with character set issues.  Will PtrToStringAnsi always
            // "Do The Right Thing"?
            if (p == IntPtr.Zero)
                return null;
            return Marshal.PtrToStringAnsi(p);
        }

#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        public static string[] PtrToStringArray(int count, IntPtr stringArray)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", "< 0");
            if (stringArray == IntPtr.Zero)
                return new string[count];


            string[] members = new string[count];
            for (int i = 0; i < count; ++i)
            {
                IntPtr s = Marshal.ReadIntPtr(stringArray, i * IntPtr.Size);
                members[i] = PtrToString(s);
            }

            return members;
        }

        public static string ByteArrayToString(byte[] bytes)
        {
            var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            var intPtr = pin.AddrOfPinnedObject();
            var result = StringHelper.PtrToString(intPtr);
            pin.Free();

            return result;
        }
        #endregion
    }
}