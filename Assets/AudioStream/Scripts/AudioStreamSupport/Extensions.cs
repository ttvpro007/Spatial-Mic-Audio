// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    public static class Extensions
    {
        public static byte[] ToBytes(this IntPtr value, int length)
        {
            if (value != IntPtr.Zero)
            {
                byte[] byteArray = new byte[length];
                Marshal.Copy(value, byteArray, 0, length);
                return byteArray;
            }
            // Return an empty array if the pointer is null.
            return new byte[1];
        }
    }
}