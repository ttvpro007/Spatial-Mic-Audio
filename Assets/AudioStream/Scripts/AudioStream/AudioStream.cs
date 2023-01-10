// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using FMOD;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AudioStream
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioStream : AudioStreamBase
    {
        /// <summary>
        /// autoresolved reference for automatic playback redirection
        /// </summary>
        AudioSourceOutputDevice audioSourceOutputDevice = null;
        // ========================================================================================================================================
        #region Unity lifecycle
        protected override IEnumerator Start()
        {
            yield return base.Start();

            // setup the AudioSource
            var audiosrc = this.GetComponent<AudioSource>();
            audiosrc.playOnAwake = false;
            audiosrc.Stop();

            // and check if AudioSourceOutputDevice is present
            this.audioSourceOutputDevice = this.GetComponent<AudioSourceOutputDevice>();
        }
        /// PCMReaderCallback data filters are applied in AudioClip - don't perform any processing here, just return them
        /// No dependency of FMOD state here - rely just on existing provided PCM data
        /// (On all platforms it seems to behave consistently the same (w Best latency): 8x 4096 long data, followed by 1x 2512 long data, repeated)
        /// </summary>
        /// <param name="data"></param>
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void PCMReaderCallback(float[] data)
        {
            var dlength = data.Length;
            // clear the arrays with repeated content..
            Array.Clear(data, 0, dlength);

            if (this.isPlaying && !this.isPaused)
            {
                // copy out all that's available
                var floats = this.decoderAudioQueue.Read(dlength);
                Array.Copy(floats, data, floats.Length);
            }
        }
        #endregion

        // ========================================================================================================================================
        #region AudioStreamBase
        /// <summary>
        /// Forwards the call to this' AudioSourceOutputDevice sibling component if it is attached
        /// </summary>
        /// <param name="outputDriverId"></param>
        public override void SetOutput(int outputDriverId)
        {
            if (this.audioSourceOutputDevice != null && this.audioSourceOutputDevice.enabled)
                this.audioSourceOutputDevice.SetOutput(outputDriverId);
        }
        /// <summary>
        /// Forwards the call to this' AudioSourceOutputDevice sibling component if it is attached
        /// </summary>
        public override void ReflectOutput_Start()
        {
            if (this.audioSourceOutputDevice != null && this.audioSourceOutputDevice.enabled)
                this.audioSourceOutputDevice.ReflectOutput_Start();
        }
        public override void ReflectOutput_Finish(List<FMOD_SystemW.OUTPUT_DEVICE> updatedOutputDevices)
        {
            if (this.audioSourceOutputDevice != null && this.audioSourceOutputDevice.enabled)
                this.audioSourceOutputDevice.ReflectOutput_Finish(updatedOutputDevices);
        }
        protected override void StreamChanged(float samplerate, int channels, SOUND_FORMAT sound_format)
        {
            LOG(LogLevel.INFO, "Stream samplerate change from {0}", this.GetComponent<AudioSource>().clip.frequency);

            this.StreamStopping();

            this.StreamStarting();

            LOG(LogLevel.INFO, "Stream samplerate changed to {0}", samplerate);
        }

        protected override void StreamStarting()
        {
            // create decoder <-> PCM exchange
            this.decoderAudioQueue = new ThreadSafeListFloat(this.streamSampleRate * 5);

            // all RT systems use default/autodetect output - silence normal channel here, after it was captured via DSP
            this.channel.setVolume(0);

            // add capture DSP to feed AudioSource PCM callback
            result = this.channel.addDSP(CHANNELCONTROL_DSP_INDEX.TAIL, this.captureDSP);
            ERRCHECK(result, "channel.addDSP");


            // create short (5 sec) looping Unity audio clip based on stream properties
            int loopingBufferSamplesCount = AudioSettings.outputSampleRate * 5;

            var asource = this.GetComponent<AudioSource>();
            asource.clip = AudioClip.Create(this.url, loopingBufferSamplesCount, this.streamChannels, AudioSettings.outputSampleRate, true, this.PCMReaderCallback);
            asource.loop = true;

            LOG(LogLevel.INFO, "Created streaming looping audio clip, samples: {0}, channels: {1}, samplerate: {2}", loopingBufferSamplesCount, this.streamChannels, AudioSettings.outputSampleRate);

            asource.Play();

            return;
        }

        protected override void StreamStarving()
        {
        }

        protected override void StreamStopping()
        {
            var asource = this.GetComponent<AudioSource>();
            asource.Stop();

            if (this.channel.hasHandle())
            {
                result = this.channel.removeDSP(this.captureDSP);
                // ERRCHECK(result, "channel.removeDSP", false); - will ERR_INVALID_HANDLE on finished channel -
            }
        }
        #endregion
    }
}