// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
using AudioStreamSupport;
using System.Collections.Generic;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Splits running AudioStreamInput channels into separate single channel AudioSources prefab instances
    /// AudioSource on this components serves just as callback to split the input
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioStreamInputChannelsSeparation : AudioStreamInputBase
    {
        [Header("[AudioSource single channel prefab]")]
        public AudioSourceChannel audioSourceChannelPrefab;
        [Header("[AudioSources created for each input channel]")]
        public AudioSourceChannel[] audioSourceChannels;
        
        MultiChannelBuffer channelBuffer;

        protected override void RecordingStarted()
        {
            // setup AudioSource with single channel AudioClip
            var @as = this.GetComponent<AudioSource>();
            Destroy(@as.clip);

            var ac = AudioSettings.GetConfiguration();
            @as.clip = AudioClip.Create("AudioInputPumpLoop", ac.sampleRate, UnityAudio.ChannelsFromUnityDefaultSpeakerMode(), ac.sampleRate, false);
            @as.loop = true;
            @as.Play();

            // instantiate and play AudioSource prefab per recording channel
            this.channelBuffer = new MultiChannelBuffer(this.recChannels);
            this.audioSourceChannels = new AudioSourceChannel[this.channelBuffer.channelCount];
            for (var i = 0; i < this.audioSourceChannels.Length; ++i)
            {
                var newAS = Instantiate(this.audioSourceChannelPrefab);
                newAS.Setup(i, this.channelBuffer, this.recRate, this.GetComponent<AudioSource>().clip.name, this.GetComponent<AudioSource>().volume, true);
                this.audioSourceChannels[i] = newAS;
            }

            // play all channels
            foreach (var ch in this.audioSourceChannels)
                ch.Play();
        }

        protected override void RecordingStopped()
        {
            var @as = this.GetComponent<AudioSource>();
            @as.Stop();

            foreach (var ch in this.audioSourceChannels)
                ch.Stop();

            this.channelBuffer = null;
        }

        protected override void RecordingUpdate()
        {
            // throw new System.NotImplementedException();
        }

#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void OnAudioFilterRead(float[] data, int channels)
        {
            // keep record update loop running even if paused
            this.UpdateRecordBuffer();

            if (this.channelBuffer == null)
                return;

            // immedaitely retrieve recorded audio and split its channels
            var inputSignal = this.GetAudioOutputBuffer((uint)(this.blocklength * this.recChannels));
            var inputSignalLength = inputSignal.Length;

            var channels_separated = new List<float[]>();
            for (var ch = 0; ch < this.recChannels; ++ch)
                channels_separated.Add(new float[inputSignalLength / this.recChannels]);

            for (var i = 0; i < inputSignalLength; i += this.recChannels)
            {
                for (var ch = 0; ch < this.recChannels; ++ch)
                    channels_separated[ch][i / this.recChannels] = inputSignal[i + ch];
            }

            for (var ch = 0; ch < this.recChannels; ++ch)
                this.channelBuffer.Add(ch, channels_separated[ch]);

            System.Array.Clear(data, 0, data.Length);
        }
    }
}