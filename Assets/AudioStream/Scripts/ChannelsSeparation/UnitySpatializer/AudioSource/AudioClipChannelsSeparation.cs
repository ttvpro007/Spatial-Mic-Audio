// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Splits playing AudioSource AudioClip's channels into separate single channel AudioSources prefab instances
    /// Resulting channel count depends on the import settings of the AudioClip being played - note the clip's 'Load Type' has to be 'Decompress On Load' in order to use GetData on it -
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioClipChannelsSeparation : MonoBehaviour
    {
        [Header("[AudioSource single channel prefab]")]
        public AudioSourceChannel audioSourceChannelPrefab;
        [Header("[AudioSources created for each clip output channel]")]
        public AudioSourceChannel[] audioSourceChannels;

        MultiChannelBuffer channelBuffer;


        int last_reported_progress;
        IEnumerator Start()
        {
            // process AudioClip by splitting its channels into separate AudioSource prefabs

            var @as = this.GetComponent<AudioSource>();
            var samples = new float[@as.clip.samples * @as.clip.channels];

            Debug.LogFormat(@"Processing clip '{0}'", @as.clip.name);

            // clip has to be decompress on load

            if (!@as.clip.GetData(samples, 0))
            {
                Debug.LogErrorFormat(@"Unable to get data from clip '{0}' - make sure its channels don't exceed Unity supported maximum and its import settings 'Load Type' is 'Decompress On Load'", @as.clip.name);
                yield break;
            }

            yield return null;

            this.last_reported_progress = 0;

            var channels = @as.clip.channels;
            this.channelBuffer = new MultiChannelBuffer(channels);

            var length = samples.Length;

            var channels_separated = new List<float[]>();
            for (var ch = 0; ch < channels; ++ch)
                channels_separated.Add(new float[length / channels]);

            for (var i = 0; i < length; i += channels)
            {
                for (var ch = 0; ch < channels; ++ch)
                    channels_separated[ch][i / channels] = samples[i + ch];

                // report progress each ~20%
                var progress = Mathf.RoundToInt(i / (float)length * 100f);
                if (this.last_reported_progress != progress
                    && progress % 20 == 0
                    )
                {
                    this.last_reported_progress = progress;

                    Debug.LogFormat(@".. {0} %", progress);

                    yield return null;
                }
            }

            for (var ch = 0; ch < channels; ++ch)
                this.channelBuffer.Add(ch, channels_separated[ch]);


            this.audioSourceChannels = new AudioSourceChannel[channels];

            for (var i = 0; i < this.audioSourceChannels.Length; ++i)
            {
                var newAS = Instantiate(this.audioSourceChannelPrefab);
                newAS.Setup(i, this.channelBuffer, AudioSettings.outputSampleRate, this.GetComponent<AudioSource>().clip.name, @as.volume, false);
                this.audioSourceChannels[i] = newAS;
            }

            Debug.LogFormat(@"Playing individual channels of clip '{0}'", @as.clip.name);

            foreach (var ch in this.audioSourceChannels)
                ch.Play();
        }
    }
}