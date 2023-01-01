// (c) 2016-2022 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System.Collections;
using UnityEngine;

namespace AudioStream
{
    public class ResonanceSource : AudioStreamBase
    {
        // ========================================================================================================================================
        #region Editor
        // [Range(0f, 1f)]
        // [Tooltip("Volume for AudioStreamMinimal has to be set independently from Unity audio")]
        // Leave volume at 1 for channel using Resonance gain.
        float volume = 1f;
        #endregion

        // ========================================================================================================================================
        #region Resonance Plugin + Resonance parameters
        ResonancePlugin resonancePlugin = null;
        
        [Header("[3D Settings]")]
        [Tooltip("If left empty, main camera transform will be considered to be listener at Start")]
        public Transform listener;

        [Range(-80f, 24f)]
        [Tooltip("Gain")]
        public float gain = 0f;

        [Range(0f, 360f)]
        [Tooltip("Spread")]
        public float spread = 0f;

        [Tooltip("rolloff")]
        public ResonancePlugin.DistanceRolloff distanceRolloff = ResonancePlugin.DistanceRolloff.LOGARITHMIC;

        [Range(0f, 10f)]
        [Tooltip("occlusion")]
        public float occlusion = 0f;

        // very narrow forward oriented cone for testing
        // directivity          -   0.8 -   forward cone only
        // directivitySharpness -   10  -   narrow focused cone

        [Range(0f, 1f)]
        [Tooltip("directivity")]
        public float directivity = 0f;

        [Range(1f, 10f)]
        [Tooltip("directivity sharpness")]
        public float directivitySharpness = 1f;

        [Range(0f, 10000f)]
        [Tooltip("Attenuation Range Min")]
        public float attenuationRangeMin = 1f;

        [Range(0f, 10000f)]
        [Tooltip("Attenuation Range Max")]
        public float attenuationRangeMax = 500f;

        [Tooltip("Room is not fully implemented. If OFF a default room will be applied resulting in slight reverb")]
        public bool bypassRoom = true;

        [Tooltip("Enable Near-Field Effects")]
        public bool nearFieldEffects = false;

        [Range(0f, 9f)]
        [Tooltip("Near-Field Gain")]
        public float nearFieldGain = 1f;

        [Range(-80f, 24f)]
        [Tooltip("Overall Gain")]
        public float overallLinearGain = 0f;

        [Range(-80f, 24f)]
        [Tooltip("Overall Gain")]
        public float overallLinearGainAdditive = 0f;

        /// <summary>
        /// previous positions for velocity
        /// </summary>
		Vector3 last_relative_position = Vector3.zero;
		Vector3 last_position = Vector3.zero;

        #endregion

        // ========================================================================================================================================
        #region Unity lifecycle
        protected override IEnumerator Start()
        {
            if (this.listener == null)
                this.listener = Camera.main.transform;

            yield return base.Start();

            while (!channel.hasHandle())
                yield return null;

            this.last_relative_position = this.transform.position - this.listener.position;
            this.last_position = this.transform.position;
        }

        void Update()
        {
            // update source position for resonance plugin
            if (this.resonancePlugin != null)
            {
                // The position of the sound relative to the listeners.
                Vector3 rel_position = this.transform.position - this.listener.position;
                Vector3 rel_velocity = rel_position - this.last_relative_position;
                this.last_relative_position = rel_position;

                // The position of the sound in world coordinates.
                Vector3 abs_velocity = this.transform.position - this.last_position;
                this.last_position = this.transform.position;

                this.resonancePlugin.ResonanceSource_SetGain(this.gain);
                this.resonancePlugin.ResonanceSource_SetSpread(this.spread);
                this.resonancePlugin.ResonanceSource_SetDistanceRolloff(this.distanceRolloff);
                this.resonancePlugin.ResonanceSource_SetOcclusion(this.occlusion);
                this.resonancePlugin.ResonanceSource_SetDirectivity(this.directivity);
                this.resonancePlugin.ResonanceSource_SetDirectivitySharpness(this.directivitySharpness);
                this.resonancePlugin.ResonanceSource_SetAttenuationRange(this.attenuationRangeMin, this.attenuationRangeMax);

                this.resonancePlugin.ResonanceSource_Set3DAttributes(
                    this.listener.InverseTransformDirection(rel_position)
                    , rel_velocity
                    , this.listener.InverseTransformDirection(this.transform.forward)
                    , this.listener.InverseTransformDirection(this.transform.up)
                    , this.transform.position
                    , abs_velocity
                    , this.transform.forward
                    , this.transform.up
                    );

                this.resonancePlugin.ResonanceSource_SetBypassRoom(this.bypassRoom);
                this.resonancePlugin.ResonanceSource_SetNearFieldFX(this.nearFieldEffects);
                this.resonancePlugin.ResonanceSource_SetNearFieldGain(this.nearFieldGain);
                this.resonancePlugin.ResonanceSource_SetOverallGain(this.overallLinearGain, this.overallLinearGainAdditive);
            }
        }

        public override void OnDestroy()
        {
            // was even started
            if (this.resonancePlugin != null)
            {
                // remove DSPs first (if e.g. changing scene directly)
                this.StreamStopping();

                this.resonancePlugin.Release();
                this.resonancePlugin = null;
            }

            base.OnDestroy();
        }
        #endregion

        // ========================================================================================================================================
        #region AudioStreamBase
        protected override void StreamStarting()
        {
            this.SetOutput(this.outputDriverID);

            result = channel.setVolume(this.volume);
            ERRCHECK(result, "channel.setVolume", false);

            /*
             *  load Resonance DSPs
             */
            this.resonancePlugin = new ResonancePlugin(this.fmodsystem.system, this.logLevel);

            /*
             * Add Resonance DSPs to the default channel when started.
             */
            result = channel.addDSP(0, this.resonancePlugin.ResonanceListener_DSP);
            ERRCHECK(result, "channel.addDSP", false);

            result = channel.addDSP(1, this.resonancePlugin.ResonanceSource_DSP);
            ERRCHECK(result, "channel.addDSP", false);
        }

        protected override void StreamStarving()
        {
            if (channel.hasHandle())
            {
                if (!starving)
                {
                    result = channel.setVolume(this.volume);
                    // ERRCHECK(result, "channel.setVolume", false);
                }
            }
        }

        protected override void StreamStopping()
        {
            if (channel.hasHandle())
            {
                if (this.resonancePlugin != null)
                {
                    result = channel.removeDSP(this.resonancePlugin.ResonanceListener_DSP);
                    // channel object handle seems to be not reliable when using plugin (?)
                    // ERRCHECK(result, "channel.removeDSP", false);

                    result = channel.removeDSP(this.resonancePlugin.ResonanceSource_DSP);
                    // channel object handle seems to be not reliable when using plugin (?)
                    // ERRCHECK(result, "channel.removeDSP", false);
                }
            }
        }

        protected override void StreamChanged(float samplerate, int channels, FMOD.SOUND_FORMAT sound_format)
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
       #endregion
    }
}