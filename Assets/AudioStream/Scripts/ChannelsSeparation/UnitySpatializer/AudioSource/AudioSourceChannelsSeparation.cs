// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Splits playing AudioSource channels into separate single channel streamed AudioSources prefab instances
    /// Resulting channel count depends on current Unity audio output (source AudioSource channels are down/up mixed by Unity as needed for current output)
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioSourceChannelsSeparation : MonoBehaviour
    {
        [Header("[AudioSource single channel prefab]")]
        public AudioSourceChannel audioSourceChannelPrefab;
        [Header("[AudioSources created for each Unity output channel]")]
        public AudioSourceChannel[] audioSourceChannels;

        MultiChannelBuffer channelBuffer;

        void Update()
        {
            // wait until channels are split in OnAudioFilterRead and instantiate and play AudioSource prefab per playing channel
            if (this.channelBuffer != null
                && (this.audioSourceChannels == null || this.audioSourceChannels.Length < 1)
                )
            {
                this.audioSourceChannels = new AudioSourceChannel[this.channelBuffer.channelCount];

                for (var i = 0; i < this.audioSourceChannels.Length; ++i)
                {
                    var newAS = Instantiate(this.audioSourceChannelPrefab);
                    newAS.Setup(i, this.channelBuffer, AudioSettings.outputSampleRate, this.GetComponent<AudioSource>().clip.name, this.GetComponent<AudioSource>().volume, true);
                    this.audioSourceChannels[i] = newAS;
                }

                foreach (var ch in this.audioSourceChannels)
                    ch.Play();
            }
        }

#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void OnAudioFilterRead(float[] data, int channels)
        {
            var length = data.Length;

            if (this.channelBuffer == null)
                this.channelBuffer = new MultiChannelBuffer(channels);

            var channels_separated = new List<float[]>();
            for (var ch = 0; ch < channels; ++ch)
                channels_separated.Add(new float[length / channels]);

            for (var i = 0; i < length; i += channels)
            {
                for (var ch = 0; ch < channels; ++ch)
                    channels_separated[ch][i / channels] = data[i + ch];
            }

            for (var ch = 0; ch < channels; ++ch)
                this.channelBuffer.Add(ch, channels_separated[ch]);

            Array.Clear(data, 0, length);
        }
    }
}