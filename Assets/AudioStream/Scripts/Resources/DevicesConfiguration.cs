// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections.Generic;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// A scriptable object holding (custom) speaker mode, # of channels/speakers and DSP buffers settings per output device/driver ID
    /// AudioStream will check this asset when accessing an output and overrides its defaults with this configuration if it exists
    /// Note the asset must be named 'DevicesConfiguration' and placed in 'Resources' folder for configuration to be picked up at runtime
    /// </summary>
    // asset creation menu commented out for release to not clutter the Editor UI
    // [CreateAssetMenu(fileName = "DevicesConfiguration", menuName = "AudioStream Output Devices Config", order = 1202)]
    public class DevicesConfiguration : ScriptableObject
    {
        [System.Serializable]
        public class DeviceConfiguration
        {
            [Tooltip("Usually not needed to be changed. Default (0) means Unity samplerate will be used.\r\nsee also https://fmod.com/docs/2.02/api/core-api-system.html#system_setsoftwareformat")]
            public int sampleRate = 0;
            [Tooltip("Speaker mode\r\n\r\nNote: Other than default is rather advanced setup and usually better left untouched.\r\nWhen raw speaker mode is selected that should default to 2 speakers ( stereo ), unless changed by user.")]
            public FMOD.SPEAKERMODE SPEAKERMODE;
            [Tooltip("No. of speakers.\r\nFMOD's maximum is 32 (https://www.fmod.com/docs/2.02/api/core-api-common.html#fmod_max_channel_width)\r\nYou might want also provide mix matrix for custom setups,\r\nsee remarks at https://www.fmod.com/docs/2.02/api/core-api-common.html#fmod_speakermode, \r\nand https://www.fmod.com/docs/2.02/api/core-api-channelcontrol.html#channelcontrol_setmixmatrix about how to setup the matrix.\r\n\r\n")]
            public int NumOfRawSpeakers = 2;
            [Tooltip("When not changed here (0) FMOD defaults to 1024 samples\r\nsee also https://fmod.com/docs/2.02/api/core-api-system.html#system_setdspbuffersize\r\n\r\nYou should be able to go very low on platforms which support it - such as 32/2 on desktops - but you have to find correct *combination* with which the FMOD mixer will still work with current input and drivers.")]
            public uint DSP_bufferLength = 0;   // FMOD default 1024; // samples
            [Tooltip("When not changed here (0) FMOD defaults to 4\r\nsee also https://fmod.com/docs/2.02/api/core-api-system.html#system_setdspbuffersize")]
            public int DSP_numBuffers = 0;      // FMOD default 4;
        }

        [Header("[Settings per output device/driver]")]
        [Tooltip("User override settings for an output device - usually not needed.\r\nEach list element corresponds to output device with that ID\r\nAn example is added in store package for output 0, but since it is set to defaults, defaults of the output 0 are used as well.")]
        public List<DeviceConfiguration> devicesConfiguration = new List<DeviceConfiguration>();

        [Header("[ASIO config on WIndows]")]
        [Tooltip("Enable ASIO on Windows\r\n\r\nnote: Settings from the above for output 0 (except DSP buffer) are also applicable here if they exist")]
        [SerializeField]
        bool asio = false;
        [Tooltip("Should match ASIO config")]
        public uint ASIO_bufferSize = 512;
        [Tooltip("Default (4) should be OK for FMOD - decrease to drive latency down, but stuff might break")]
        public int ASIO_bufferCount = 4;

        public bool ASIO
        {
            get
            {
#if UNITY_STANDALONE_WIN
                return this.asio;
#else
                return false;
#endif
            }
            set
            {
                this.asio = value;
            }
        }

        static DevicesConfiguration _instance;
        public static DevicesConfiguration Instance
        {
            get
            {
                if (DevicesConfiguration._instance == null)
                {
                    DevicesConfiguration._instance = Resources.Load<DevicesConfiguration>("DevicesConfiguration");
                }

                return DevicesConfiguration._instance;
            }
        }
    }
}