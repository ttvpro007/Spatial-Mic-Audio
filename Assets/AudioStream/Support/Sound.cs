// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

namespace AudioStreamSupport
{
    public static class Sound
    {
        /*
         * This follows FMOD sounds format to allow decoupling from FMOD in AudioStream asset
         */
        /// <summary>
        /// fmod.cs cca 2.01.xx {'21}
        /// </summary>
        public enum SOUND_FORMAT : int
        {
            NONE,
            PCM8,
            PCM16,
            PCM24,
            PCM32,
            PCMFLOAT,
            BITSTREAM,

            MAX
        }
    }
}