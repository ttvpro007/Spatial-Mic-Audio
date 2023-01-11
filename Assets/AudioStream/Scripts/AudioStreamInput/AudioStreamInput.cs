// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System.Collections;
using UnityEngine;

namespace AudioStream
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioStreamInput : AudioStreamInputBase
    {
        // ========================================================================================================================================
        #region Recording
        /// <summary>
        /// Create new audio clip with FMOD callback for spatialization. Latency will suffer.
        /// </summary>
        protected override void RecordingStarted()
        {
            var asource = this.GetComponent<AudioSource>();

            // set looping audio clip samples to match the size of recording buffer ( see exinfo.length in base )
            // otherwise Android (weaker devices?) couldn't cope 

            int loopingBufferSamplesCount = this.recRate * this.channelSize * this.recChannels;
            var inputs = FMOD_SystemW.AvailableInputs(this.logLevel, this.gameObject.name, this.OnError);
            asource.clip = AudioClip.Create(inputs[this.recordDeviceId].name, loopingBufferSamplesCount, this.recChannels, this.resampleInput ? this.recRate : AudioSettings.outputSampleRate, true, this.PCMReaderCallback);
            asource.loop = true;

            LOG(LogLevel.DEBUG, "Created streaming looping recording clip for AudioSource, samples: {0}, channels: {1}, samplerate: {2}", loopingBufferSamplesCount, this.recChannels, this.recRate);

            asource.Play();
        }
        /// <summary>
        /// Provide recording data for FMOD callback
        /// </summary>
        protected override void RecordingUpdate()
        {
            // keep record update loop running even if paused
            this.UpdateRecordBuffer();
        }

        protected override void RecordingStopped()
        {
            var asource = this.GetComponent<AudioSource>();
            asource.Stop();
        }
        /// <summary>
        /// Satisfy AudioClip data requests
        /// PCMReaderCallback data filters are applied in AudioClip - don't perform any additional filtering here
        /// </summary>
        /// <param name="data"></param>
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void PCMReaderCallback(float[] data)
        {
            System.Array.Clear(data, 0, data.Length);

            // always drain incoming buffer
            var fArr = this.GetAudioOutputBuffer((uint)data.Length);

            // if paused don't output it
            if (this.isPaused)
                return;

            var len = Mathf.Min(fArr.Length, data.Length);
            for (var i = 0; i < len; ++i)
                data[i] = fArr[i];

            // TODO: Note - find out why System.Buffer.BlockCopy doesn't support block copy between same typed arrays
            // System.Buffer.BlockCopy(fArr, 0, data, 0, fArr.Length);
        }
        #endregion
    }
}