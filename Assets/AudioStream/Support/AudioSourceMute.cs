// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
using UnityEngine;

namespace AudioStreamSupport
{
    public class AudioSourceMute : MonoBehaviour
    {
        [Tooltip("Supress AudioSource signal here.\nNote: this is implemented via OnAudioFilterRead, which might not be optimal - you can consider e.g. mixer routing and supress signal there.")]
        public bool mute = true;

#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (mute)
                System.Array.Clear(data, 0, data.Length);
        }
    }
}