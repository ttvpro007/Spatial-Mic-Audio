// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using FMOD;
using UnityEngine;

namespace AudioStream
{
    public class AudioStreamMinimal : AudioStreamBase
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[AudioStreamMinimal]")]
        [Range(0f, 1f)]
        [Tooltip("Volume for AudioStreamMinimal has to be set independently from Unity audio")]
        public float volume = 1f;
        #endregion

        protected override void StreamChanged(float samplerate, int channels, SOUND_FORMAT sound_format)
        {
            float defFrequency;
            int defPriority;
            result = sound.getDefaults(out defFrequency, out defPriority);
            ERRCHECK(result, "sound.getDefaults", false);

            LOG(LogLevel.INFO, "Stream samplerate change from {0}, {1}", defFrequency, sound_format);

            result = sound.setDefaults(samplerate, defPriority);
            ERRCHECK(result, "sound.setDefaults", false);

            LOG(LogLevel.INFO, "Stream samplerate changed to {0}, {1}", samplerate, sound_format);
        }

        protected override void StreamStarting()
        {
            this.SetOutput(this.outputDriverID);

            result = channel.setVolume(this.volume);
            ERRCHECK(result, "channel.setVolume", false);
        }

        protected override void StreamStarving()
        {
            if (channel.hasHandle())
            {
                if (!this.starving)
                {
                    result = channel.setVolume(this.volume);
                    // ERRCHECK(result, "channel.setVolume", false);
                }
            }
        }

        protected override void StreamStopping()
        {
        }
    }
}