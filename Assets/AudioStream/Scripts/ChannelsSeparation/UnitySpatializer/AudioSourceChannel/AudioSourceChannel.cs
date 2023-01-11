// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
using System;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Creates and plays either streamed or normal single channel AudioClip based on provided MultiChannelBuffer support class and required channel
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioSourceChannel : MonoBehaviour
    {
        int channel;
        MultiChannelBuffer channelBuffer;
        AudioSource audioSource;

        void Awake()
        {
            // initial AudioSource setup
            this.audioSource = this.GetComponent<AudioSource>();
            if (this.audioSource.clip)
            {
                Debug.LogWarningFormat("Existing AudioClip {0} not used and will be destroyed", this.audioSource.clip.name);
                Destroy(this.audioSource.clip);
            }

            this.audioSource.playOnAwake = false;
            this.audioSource.loop = true;
        }

        public void Setup(int _channel, MultiChannelBuffer ofChannelBuffer, int withSampleRate, string audioClipName, float initialVolume, bool streamed)
        {
            this.channel = _channel;
            this.channelBuffer = ofChannelBuffer;
            this.audioSource.volume = initialVolume;

            if (streamed)
            {
                this.audioSource.clip = AudioClip.Create(string.Format(@"{0}|CH#{1}", audioClipName, this.channel)
                    , withSampleRate /* 1 sec. long */
                    , 1
                    , withSampleRate
                    , true
                    , this.PCMReaderCallback
                    );
            }
            else
            {
                var samples = this.channelBuffer.Remove(this.channel, this.channelBuffer.SampleCount(this.channel));

                this.audioSource.clip = AudioClip.Create(string.Format(@"{0}|CH#{1}", audioClipName, this.channel)
                    , samples.Length
                    , 1
                    , withSampleRate
                    , false
                    );

                this.audioSource.clip.SetData(samples, 0);
            }
        }

        public void Play()
        {
            if (this.audioSource)
                this.audioSource.Play();
        }

        public void Stop()
        {
            if (this.audioSource)
                this.audioSource.Stop();
        }

#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void PCMReaderCallback(float[] data)
        {
            var length = data.Length;
            Array.Clear(data, 0, length);

            var arr = this.channelBuffer.Remove(this.channel, length);
            Array.Copy(arr, data, arr.Length);
        }
    }
}