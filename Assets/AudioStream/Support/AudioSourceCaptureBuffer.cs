// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
using UnityEngine;

namespace AudioStreamSupport
{
    /// <summary>
    /// Simple component for capturing a game object's filter callback audio buffer for other filters to read from
    /// (no AudioSource dependency since it can be used e.g. on listener too)
    /// </summary>
    public class AudioSourceCaptureBuffer : MonoBehaviour
    {
        [Tooltip("Post volume")]
        [Range(0f, 1f)]
        public float monitoringVolume = 1f;

        public float[] captureBuffer = null;

#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void OnAudioFilterRead(float[] data, int channels)
        {
            var dlength = data.Length;

            if (this.captureBuffer == null
                || this.captureBuffer.Length != dlength)
                this.captureBuffer = new float[dlength];

            System.Array.Copy(data, 0, this.captureBuffer, 0, dlength);

            for (var i = 0; i < dlength; ++i)
                data[i] *= this.monitoringVolume;
        }
    }
}