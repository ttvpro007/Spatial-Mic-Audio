// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd
using System.Collections;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Streams selected input and processes it via FMOD's supplied Resonance DSP plugin
    /// Right now the channel is played on default output by FMOD, bypassing Unity AudioSource
    /// </summary>
    public class ResonanceInput : AudioStreamInputBase
    {
        // ========================================================================================================================================
        #region Resonance Plugin + Resonance parameters
        ResonancePlugin resonancePlugin = null;
        FMOD.DSP resonanceSource_DSP;
        FMOD.DSP resonanceListener_DSP;
        FMOD.ChannelGroup resonanceChannelGroup;

        [Header("[3D Settings]")]
        [Tooltip("If left empty, main camera transform will be considered for listener position")]
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

        // very narrow forward oriented cone for testing:
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
        /// <summary>
        /// separate flag for DSPs when Resonance is loaded and DSP are added to desired channel
        /// </summary>
        bool dspRunning = false;

        #endregion
        
        // ========================================================================================================================================
        #region Unity lifecycle
        protected override IEnumerator Start()
        {
            if (this.listener == null)
                this.listener = Camera.main.transform;

            this.last_relative_position = this.transform.position - this.listener.position;
            this.last_position = this.transform.position;

            yield return base.Start();
        }

        protected override void Update()
        {
            base.Update();

            if (this.resonancePlugin != null && this.dspRunning
                && this.resonanceSource_DSP.hasHandle()
                )
            {
                // The position of the sound relative to the listeners.
                Vector3 rel_position = this.transform.position - this.listener.position;
                Vector3 rel_velocity = rel_position - this.last_relative_position;
                this.last_relative_position = rel_position;

                // The position of the sound in world coordinates.
                Vector3 abs_velocity = this.transform.position - this.last_position;
                this.last_position = this.transform.position;

                this.resonancePlugin.ResonanceSource_SetGain(this.gain, this.resonanceSource_DSP);
                this.resonancePlugin.ResonanceSource_SetSpread(this.spread, this.resonanceSource_DSP);
                this.resonancePlugin.ResonanceSource_SetDistanceRolloff(this.distanceRolloff, this.resonanceSource_DSP);
                this.resonancePlugin.ResonanceSource_SetOcclusion(this.occlusion, this.resonanceSource_DSP);
                this.resonancePlugin.ResonanceSource_SetDirectivity(this.directivity, this.resonanceSource_DSP);
                this.resonancePlugin.ResonanceSource_SetDirectivitySharpness(this.directivitySharpness, this.resonanceSource_DSP);
                this.resonancePlugin.ResonanceSource_SetAttenuationRange(this.attenuationRangeMin, this.attenuationRangeMax, this.resonanceSource_DSP);

                this.resonancePlugin.ResonanceSource_Set3DAttributes(
                    this.listener.InverseTransformDirection(rel_position)
                    , rel_velocity
                    , this.listener.InverseTransformDirection(this.transform.forward)
                    , this.listener.InverseTransformDirection(this.transform.up)
                    , this.transform.position
                    , abs_velocity
                    , this.transform.forward
                    , this.transform.up
                    , this.resonanceSource_DSP
                    );

                this.resonancePlugin.ResonanceSource_SetBypassRoom(this.bypassRoom, this.resonanceSource_DSP);
                this.resonancePlugin.ResonanceSource_SetNearFieldFX(this.nearFieldEffects, this.resonanceSource_DSP);
                this.resonancePlugin.ResonanceSource_SetNearFieldGain(this.nearFieldGain, this.resonanceSource_DSP);
                this.resonancePlugin.ResonanceSource_SetOverallGain(this.overallLinearGain, this.overallLinearGainAdditive, this.resonanceSource_DSP);
            }
        }

        protected override void OnDestroy()
        {
            // was even started
            if (this.resonancePlugin != null)
            {
                // remove DSPs first (if e.g. changing scene directly)
                this.RecordingStopped();
            }

            base.OnDestroy();
        }
        #endregion
        // ========================================================================================================================================
        #region AudioStreamInputBase
        protected override void RecordingStarted()
        {
            //
            // Load Resonance + DSPs
            // has to be just here since it needs a valid system (needs to wait for the base, but base can call record from Start)
            //
            this.resonancePlugin = ResonancePlugin.Load(this.recording_system.system, this.logLevel);
            this.resonanceSource_DSP = ResonancePlugin.New_ResonanceSource_DSP();
            this.resonanceListener_DSP = ResonancePlugin.New_ResonanceListener_DSP();
            this.resonancePlugin.ResonanceListener_SetRoomProperties(this.resonanceListener_DSP);
            this.resonancePlugin.ResonanceListener_SetGain(0, this.resonanceListener_DSP);
            //
            // Play the sound on 'Resonance' channel group along recording
            // that will run the DSP chain
            //
            this.recording_system.system.createChannelGroup(this.gameObject.name, out this.resonanceChannelGroup);
            ERRCHECK(result, "system.createChannelGroup");

            result = this.resonanceChannelGroup.setVolume(0);
            ERRCHECK(result, "resonanceChannelGroup.setVolume");
            //
            // Add DSPs to 'Resonance' channel group
            //
            result = this.resonanceChannelGroup.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, this.resonanceListener_DSP);
            ERRCHECK(result, "resonanceChannelGroup.addDSP");

            result = this.resonanceChannelGroup.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, this.resonanceSource_DSP);
            ERRCHECK(result, "resonanceChannelGroup.addDSP");
            //
            // play the sound
            // 
            FMOD.Channel channel;
            result = this.recording_system.system.playSound(this.sound, this.resonanceChannelGroup, false, out channel);
            ERRCHECK(result, "this.recording_system.system.playSound");

            result = this.resonanceChannelGroup.setVolume(1);
            ERRCHECK(result, "resonanceChannelGroup.setVolume");

            this.dspRunning = true;
        }
        /// <summary>
        /// Nothing to do w/ rec buffer since the channel is played directly by FMOD only
        /// </summary>
        protected override void RecordingUpdate()
        {
            result = this.recording_system.Update();
            ERRCHECK(result, "this.recording_system.Update", false);
        }

        protected override void RecordingStopped()
        {
            if (this.resonanceChannelGroup.hasHandle())
            {
                if (this.resonancePlugin != null)
                {
                    result = this.resonanceChannelGroup.removeDSP(this.resonanceSource_DSP);
                    ERRCHECK(result, "channel.removeDSP", false);

                    result = this.resonanceChannelGroup.removeDSP(this.resonanceListener_DSP);
                    ERRCHECK(result, "channel.removeDSP", false);

                    result = this.resonanceSource_DSP.release();
                    ERRCHECK(result, "resonanceSource_DSP.release", false);

                    result = this.resonanceListener_DSP.release();
                    ERRCHECK(result, "resonanceListener_DSP.release", false);

                    this.resonanceChannelGroup.release();
                    ERRCHECK(result, "resonanceChannelGroup.release", false);

                    ResonancePlugin.Unload();
                    this.resonancePlugin = null;
                }
            }

            this.dspRunning = false;
        }
        #endregion
    }
}
