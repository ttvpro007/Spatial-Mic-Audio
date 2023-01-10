// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using System;
using UnityEngine;

namespace AudioStreamSupport
{
    public static class UnityAudio
    {
        // ========================================================================================================================================
        #region Unity audio / channels / conversions
        /// <summary>
        /// Tries to return no. of output channels Unity is currently using based on AudioSettings.speakerMode (this should match channels in e.g. OnAudioFilterRead callbacks)
        /// If bandwidth of user selected Default Speaker Mode in AudioSettings (AudioSettings.speakerMode) differs from actual HW capabilities (AudioSettings.driverCapabilities), Unity will render audio
        /// with lower/actual bandwidth using AudioSettings.driverCapabilities channels instead in some cases. We report this channel count in this case as well.
        /// </summary>
        /// <returns></returns>
        public static int ChannelsFromUnityDefaultSpeakerMode()
        {
            var speakerMode = AudioSettings.speakerMode;

            // check user selected vs. hw channels/bandwidth
            // it seems they do "stuff" for Mono/Stereo, but channels for all other outputs are incorrect then, except ProLogic since that mixes Stero (!) to 5.1
            // [initial report by @ddf on the forums (thanks, @ddf)
            // problematic case was AudioSettings.speakerMode           == Mode7point1
            // &&                   AudioSettings.driverCapabilities    == Stereo
            // AudioSettings.driverCapabilities was used since that was what OAFR was operating on according to report (pitch was set incorrectly in recording)]
            if (AudioSettings.driverCapabilities != speakerMode)
            {
                if (speakerMode != AudioSpeakerMode.Mono
                    && speakerMode != AudioSpeakerMode.Stereo
                    && speakerMode != AudioSpeakerMode.Prologic)
                {
                    speakerMode = AudioSettings.driverCapabilities;

                    Debug.LogWarningFormat("Output HW driver [{0}] doesn't match currently selected Unity Default Speaker Mode [{1}] - Unity will (probably) use [{2}] in this case. Consider matching your Default Speaker Mode with current actual hardware used for default output"
                        , AudioSettings.driverCapabilities, AudioSettings.speakerMode, AudioSettings.driverCapabilities);
                }
            }

            switch (speakerMode)
            {
                case AudioSpeakerMode.Mode5point1:
                    return 6;
                case AudioSpeakerMode.Mode7point1:
                    return 8;
                case AudioSpeakerMode.Mono:
                    return 1;
                case AudioSpeakerMode.Prologic:
                    // https://docs.unity3d.com/ScriptReference/AudioSpeakerMode.Prologic.html
                    // Channel count is set to 2. Stereo output, but data is encoded in a way that is picked up by a Prologic/Prologic2 decoder and split into a 5.1 speaker setup.
                    return 2;                    
                case AudioSpeakerMode.Quad:
                    return 4;
#if !UNITY_2019_2_OR_NEWER
                case AudioSpeakerMode.Raw:
                    Debug.LogError("Don't call ChannelsFromUnityDefaultSpeakerMode with Unity 'Default Speaker Mode' set to 'Raw' - provide channel count manually in that case. Returning 2 (Stereo).");
                    return 2;
#endif
                case AudioSpeakerMode.Stereo:
                    return 2;
                case AudioSpeakerMode.Surround:
                    return 5;
                default:
                    Debug.LogError("Unknown AudioSettings.speakerMode - Returning 2 (Stereo).");
                    return 2;
            }
        }
        /// <summary>
        /// convert channels to unity speaker mode
        /// this is as per above when Unity could be using different channels (and thus speaker mode) following actual HW as opposed to what's set in Player Settings FFS
        /// </summary>
        /// <param name="channels"></param>
        /// <returns></returns>
        public static AudioSpeakerMode UnitySpeakerModeFromChannels(int channels)
        {
            switch (channels)
            {
                case 6:
                    return AudioSpeakerMode.Mode5point1;
                case 8:
                    return AudioSpeakerMode.Mode7point1;
                case 1:
                    return AudioSpeakerMode.Mono;
                case 2:
                    // https://docs.unity3d.com/ScriptReference/AudioSpeakerMode.Prologic.html
                    // Channel count is set to 2. Stereo output, but data is encoded in a way that is picked up by a Prologic/Prologic2 decoder and split into a 5.1 speaker setup.
                    return AudioSpeakerMode.Stereo;
                case 4:
                    return AudioSpeakerMode.Quad;
                case 5:
                    return AudioSpeakerMode.Surround;
                default:
                    Debug.LogError("Unknown AudioSettings.speakerMode - Returning 2 (Stereo).");
                    return AudioSpeakerMode.Stereo;
            }
        }
        /// <summary>
        /// Empirically observed Unity audio latency string
        /// </summary>
        /// <returns></returns>
        public static string UnityAudioLatencyDescription()
        {
            // 256 - Best latency
            // 512 - Good latency
            // 1024 - Default in pre-2018.1 versions, Best performance

            string result = "Unity audio: ";
            switch (AudioSettings.GetConfiguration().dspBufferSize)
            {
                case 256:
                    result += "Best latency";
                    break;
                case 512:
                    result += "Good latency";
                    break;
                case 1024:
                    result += "Best performance";
                    break;
                default:
                    result += "Unknown latency";
                    break;
            }
            return result;
        }
        #endregion
        // ========================================================================================================================================
        #region audio byte array
        readonly static object _thelock = new object();
        /// <summary>
        /// FMOD stream -> Unity
        /// </summary>
        /// <param name="byteArray"></param>
        /// <param name="byteArray_length"></param>
        /// <param name="bytes_per_value"></param>
        /// <param name="sound_format"></param>
        /// <param name="resultFloatArray"></param>
        /// <returns></returns>
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        public static int ByteArrayToFloatArray(byte[] byteArray, uint byteArray_length, byte bytes_per_value, AudioStreamSupport.Sound.SOUND_FORMAT sound_format, ref float[] resultFloatArray)
        {
            lock (UnityAudio._thelock)
            {
                if (resultFloatArray == null || resultFloatArray.Length != (byteArray_length / bytes_per_value))
                    resultFloatArray = new float[byteArray_length / bytes_per_value];

                int arrIdx = 0;
                for (int i = 0; i < byteArray_length; i += bytes_per_value)
                {
                    var barr = new byte[bytes_per_value];
                    for (int ii = 0; ii < bytes_per_value; ++ii) barr[ii] = byteArray[i + ii];

                    if (sound_format == AudioStreamSupport.Sound.SOUND_FORMAT.PCMFLOAT)
                    {
                        resultFloatArray[arrIdx++] = BitConverter.ToSingle(barr, 0);
                    }
                    else
                    {
                        // inlined former 'BytesToFloat' method
                        // TODO: figure out how to cast this on iOS in IL2CPP - PCM24, & format does not work there
                        // TODO: but widened type does not correctly construct base PCM16 (which we will rather keep)
#if !UNITY_IOS && !UNITY_ANDROID
                        Int64 result = 0;
                        for (int barridx = 0; barridx < barr.Length; ++barridx)
                            result |= ((Int64)barr[barridx] << (8 * barridx));

                        var f = (float)(Int32)result;
#else
                        int result = 0;
                        for (int barridx = 0; barridx < barr.Length; ++barridx)
                            result |= ((int)barr[barridx] << (8 * barridx));

                        var f = (float)(short)result;
#endif
                        switch (sound_format)
                        {
                            case AudioStreamSupport.Sound.SOUND_FORMAT.PCM8:
                                // PCM8 is unsigned:
                                if (f > 127)
                                    f = f - 255f;
                                f = f / (float)127;
                                break;
                            case AudioStreamSupport.Sound.SOUND_FORMAT.PCM16:
                                f = (Int16)f / (float)Int16.MaxValue;
                                break;
                            case AudioStreamSupport.Sound.SOUND_FORMAT.PCM24:
                                if (f > 8388607)
                                    f = f - 16777215;
                                f = f / (float)8388607;
                                break;
                            case AudioStreamSupport.Sound.SOUND_FORMAT.PCM32:
                                f = (Int32)f / (float)Int32.MaxValue;
                                break;
                        }

                        resultFloatArray[arrIdx++] = f;
                    }
                }

                return arrIdx;
            }
        }
        /// <summary>
        /// Unity -> byte stream, floats are converted to UInt16 PCM data
        /// TODO: might use Buffer.BlockCopy here ? - not used in time critical components though..
        /// </summary>
        /// <param name="floatArray"></param>
        /// <param name="floatArray_length"></param>
        /// <param name="resultByteArray"></param>
        /// <returns></returns>
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        public static int FloatArrayToPCM16yteArray(float[] floatArray, uint floatArray_length, ref byte[] resultByteArray)
        {
            if (resultByteArray == null || resultByteArray.Length != (floatArray_length * sizeof(UInt16)))
                resultByteArray = new byte[floatArray_length * sizeof(UInt16)];

            for (int i = 0; i < floatArray_length; ++i)
            {
                var bArr = FloatToByteArray(floatArray[i] * 32768f);

                resultByteArray[i * 2] = bArr[0];
                resultByteArray[(i * 2) + 1] = bArr[1];
            }

            return resultByteArray.Length;
        }
        static byte[] FloatToByteArray(float _float)
        {
            var result = new byte[2];

            var fa = (UInt16)(_float);
            byte b0 = (byte)(fa >> 8);
            byte b1 = (byte)(fa & 0xFF);

            result[0] = b1;
            result[1] = b0;

            return result;

            // BitConverter preserves endianess, but is slower..
            // return BitConverter.GetBytes(Convert.ToInt16(_float));
        }
        /// <summary>
        /// Decoded stream -> Unity
        /// </summary>
        /// <param name="shortArray"></param>
        /// <param name="shortArray_length"></param>
        /// <param name="resultFloatArray"></param>
        /// <returns></returns>
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        public static int ShortArrayToFloatArray(short[] shortArray, uint shortArray_length, ref float[] resultFloatArray)
        {
            if (resultFloatArray == null || resultFloatArray.Length != (shortArray_length))
                resultFloatArray = new float[shortArray_length];

            for (int i = 0; i < shortArray_length; ++i)
            {
                var f = (float)(shortArray[i] / 32768f);

                resultFloatArray[i] = f;
            }

            return resultFloatArray.Length;
        }
        /// <summary>
        /// Unity -> encoder
        /// </summary>
        /// <param name="floatArray"></param>
        /// <param name="floatArray_length"></param>
        /// <param name="resultShortArray"></param>
        /// <returns></returns>
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        public static int FloatArrayToShortArray(float[] floatArray, uint floatArray_length, ref short[] resultShortArray)
        {
            if (resultShortArray == null || resultShortArray.Length != floatArray_length)
                resultShortArray = new short[floatArray_length];

            for (int i = 0; i < floatArray_length; ++i)
            {
                resultShortArray[i] = (short)(floatArray[i] * 32768f);
            }

            return resultShortArray.Length;
        }
        #endregion
    }
}