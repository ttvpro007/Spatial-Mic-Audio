// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System.Collections;
using UnityEngine;

namespace AudioStream
{
    public class ResonanceSoundfield : AudioStreamBase
    {
        // ========================================================================================================================================
        #region Resonance Plugin + Resonance parameters
        ResonancePlugin resonancePlugin = null;
        FMOD.DSP resonanceSoundfield_DSP;
        FMOD.DSP resonanceListener_DSP;
        FMOD.ChannelGroup resonanceChannelGroup;

        [Header("[3D Settings]")]
        /// <summary>
        /// 
        /// </summary>
        public Transform listener;

        [Range(-80f, 24f)]
        [Tooltip("Gain")]
        public float gain = 0f;

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

            this.last_relative_position = this.transform.position - this.listener.position;
            this.last_position = this.transform.position;
        }

        void Update()
        {
            // update source position for resonance plugin
            if (this.resonancePlugin != null
                && this.resonanceSoundfield_DSP.hasHandle()
                )
            {
                this.resonancePlugin.ResonanceSoundfield_SetGain(this.gain, this.resonanceSoundfield_DSP);

                // The position of the sound relative to the listeners.
                Vector3 rel_position = this.transform.position - this.listener.position;
                Vector3 rel_velocity = rel_position - this.last_relative_position;
                this.last_relative_position = rel_position;

                // The position of the sound in world coordinates.
                Vector3 abs_velocity = this.transform.position - this.last_position;
                this.last_position = this.transform.position;

                this.resonancePlugin.ResonanceSoundfield_Set3DAttributes(
                    this.listener.InverseTransformDirection(rel_position)
                    , rel_velocity
                    , this.listener.InverseTransformDirection(this.transform.forward)
                    , this.listener.InverseTransformDirection(this.transform.up)
                    , this.transform.position
                    , abs_velocity
                    , this.transform.forward
                    , this.transform.up
                    , this.resonanceSoundfield_DSP
                    );

                this.resonancePlugin.ResonanceSoundfield_SetOverallGain(this.overallLinearGain, this.overallLinearGainAdditive, this.resonanceSoundfield_DSP);
            }
        }

        public override void OnDestroy()
        {
            // was even started
            if (this.resonancePlugin != null)
            {
                // remove DSPs first (if e.g. changing scene directly)
                this.StreamStopping();
            }

            base.OnDestroy();
        }
        #endregion

        // ========================================================================================================================================
        #region AudioStreamBase
        protected override void StreamStarting()
        {
            this.SetOutput(this.outputDriverID);
            // 
            // Load Resonance + DSPs
            // has to be just here since it needs a valid system (needs to wait for the base, but base can call record from Start)
            //
            this.resonancePlugin = ResonancePlugin.Load(this.fmodsystem.system, this.logLevel);
            this.resonanceSoundfield_DSP = ResonancePlugin.New_ResonanceSoundfield_DSP();
            this.resonanceListener_DSP = ResonancePlugin.New_ResonanceListener_DSP();
            this.resonancePlugin.ResonanceListener_SetRoomProperties(this.resonanceListener_DSP);
            this.resonancePlugin.ResonanceListener_SetGain(0, this.resonanceListener_DSP);
            // 
            // Create separate 'Resonance' channel group
            // 
            this.fmodsystem.system.createChannelGroup(this.gameObject.name, out this.resonanceChannelGroup);
            ERRCHECK(result, "system.createChannelGroup");

            result = this.resonanceChannelGroup.setVolume(0);
            ERRCHECK(result, "resonanceChannelGroup.setVolume");
            // 
            // Add DSPs to 'Resonance' channel group
            // 
            result = this.resonanceChannelGroup.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, this.resonanceListener_DSP);
            ERRCHECK(result, "resonanceChannelGroup.addDSP");

            result = this.resonanceChannelGroup.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, this.resonanceSoundfield_DSP);
            ERRCHECK(result, "resonanceChannelGroup.addDSP", false);
            //
            // and play current stream to it
            //
            this.channel.setChannelGroup(this.resonanceChannelGroup);
            ERRCHECK(result, "channel.setChannelGroup");

            result = this.resonanceChannelGroup.setVolume(1);
            ERRCHECK(result, "resonanceChannelGroup.setVolume");

            this.channel.setVolume(1);
            ERRCHECK(result, "channel.setVolume");
        }

        protected override void StreamStarving()
        {
        }

        protected override void StreamStopping()
        {
            if (this.resonanceChannelGroup.hasHandle())
            {
                if (this.resonancePlugin != null)
                {
                    result = this.resonanceChannelGroup.removeDSP(this.resonanceSoundfield_DSP);
                    ERRCHECK(result, "channel.removeDSP", false);

                    result = this.resonanceChannelGroup.removeDSP(this.resonanceListener_DSP);
                    ERRCHECK(result, "channel.removeDSP", false);

                    result = this.resonanceSoundfield_DSP.release();
                    ERRCHECK(result, "resonanceSoundfield_DSP.release", false);

                    result = this.resonanceListener_DSP.release();
                    ERRCHECK(result, "resonanceListener_DSP.release", false);

                    this.resonanceChannelGroup.release();
                    ERRCHECK(result, "resonanceChannelGroup.release", false);

                    ResonancePlugin.Unload();
                    this.resonancePlugin = null;
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