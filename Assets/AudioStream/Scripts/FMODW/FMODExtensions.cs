// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    public static class FMODExtensions
    {
        // Convert from/to few FMOD types for (UI) convenience and better decoupling of support method/s
        // e.g. when FMOD is not installed AudioStreamSupport can still be used

        /// <summary>
        /// FMOD.SOUND_FORMAT -> AudioStreamSupport.Sound.SOUND_FORMAT
        /// </summary>
        /// <param name="fmod_sound_format"></param>
        /// <returns></returns>
        public static AudioStreamSupport.Sound.SOUND_FORMAT ToAudioStreamSoundFormat(this FMOD.SOUND_FORMAT fmod_sound_format)
        {
            // handle each case separately for potential better future compatibility
            switch (fmod_sound_format)
            {
                case FMOD.SOUND_FORMAT.NONE:
                    return AudioStreamSupport.Sound.SOUND_FORMAT.NONE;
                case FMOD.SOUND_FORMAT.PCM8:
                    return AudioStreamSupport.Sound.SOUND_FORMAT.PCM8;
                case FMOD.SOUND_FORMAT.PCM16:
                    return AudioStreamSupport.Sound.SOUND_FORMAT.PCM16;
                case FMOD.SOUND_FORMAT.PCM24:
                    return AudioStreamSupport.Sound.SOUND_FORMAT.PCM24;
                case FMOD.SOUND_FORMAT.PCM32:
                    return AudioStreamSupport.Sound.SOUND_FORMAT.PCM32;
                case FMOD.SOUND_FORMAT.PCMFLOAT:
                    return AudioStreamSupport.Sound.SOUND_FORMAT.PCMFLOAT;
                case FMOD.SOUND_FORMAT.BITSTREAM:
                    return AudioStreamSupport.Sound.SOUND_FORMAT.BITSTREAM;
                case FMOD.SOUND_FORMAT.MAX:
                    return AudioStreamSupport.Sound.SOUND_FORMAT.MAX;
                default:
                    throw new System.NotSupportedException("AudioStream needs to be updated for latest FMOD sound formats");
            }
        }
        /// <summary>
        /// AudioStream.StreamAudioType -> FMOD.SOUND_TYPE
        /// </summary>
        /// <param name="audiostream_audio_type"></param>
        /// <returns></returns>
        public static FMOD.SOUND_TYPE ToFMODSoundType(this AudioStream.StreamAudioType audiostream_audio_type)
        {
            // handle each case rather separately
            switch (audiostream_audio_type)
            {
                case AudioStreamBase.StreamAudioType.AIFF:
                    return FMOD.SOUND_TYPE.AIFF;
                case AudioStreamBase.StreamAudioType.ASF:
                    return FMOD.SOUND_TYPE.ASF;
                case AudioStreamBase.StreamAudioType.AT9:
                    return FMOD.SOUND_TYPE.AT9;
                case AudioStreamBase.StreamAudioType.AUDIOQUEUE:
                    return FMOD.SOUND_TYPE.AUDIOQUEUE;
                case AudioStreamBase.StreamAudioType.AUTODETECT:
                    return FMOD.SOUND_TYPE.UNKNOWN;
                case AudioStreamBase.StreamAudioType.DLS:
                    return FMOD.SOUND_TYPE.DLS;
                case AudioStreamBase.StreamAudioType.FADPCM:
                    return FMOD.SOUND_TYPE.FADPCM;
                case AudioStreamBase.StreamAudioType.FLAC:
                    return FMOD.SOUND_TYPE.FLAC;
                case AudioStreamBase.StreamAudioType.FSB:
                    return FMOD.SOUND_TYPE.FSB;
                case AudioStreamBase.StreamAudioType.IT:
                    return FMOD.SOUND_TYPE.IT;
                case AudioStreamBase.StreamAudioType.MEDIACODEC:
                    return FMOD.SOUND_TYPE.MEDIACODEC;
                case AudioStreamBase.StreamAudioType.MEDIA_FOUNDATION:
                    return FMOD.SOUND_TYPE.MEDIA_FOUNDATION;
                case AudioStreamBase.StreamAudioType.MIDI:
                    return FMOD.SOUND_TYPE.MIDI;
                case AudioStreamBase.StreamAudioType.MOD:
                    return FMOD.SOUND_TYPE.MOD;
                case AudioStreamBase.StreamAudioType.MPEG:
                    return FMOD.SOUND_TYPE.MPEG;
                case AudioStreamBase.StreamAudioType.OGGVORBIS:
                    return FMOD.SOUND_TYPE.OGGVORBIS;
                case AudioStreamBase.StreamAudioType.OPUS:
                    return FMOD.SOUND_TYPE.OPUS;
                case AudioStreamBase.StreamAudioType.PLAYLIST:
                    return FMOD.SOUND_TYPE.PLAYLIST;
                case AudioStreamBase.StreamAudioType.RAW:
                    return FMOD.SOUND_TYPE.RAW;
                case AudioStreamBase.StreamAudioType.S3M:
                    return FMOD.SOUND_TYPE.S3M;
                case AudioStreamBase.StreamAudioType.USER:
                    return FMOD.SOUND_TYPE.USER;
                case AudioStreamBase.StreamAudioType.VORBIS:
                    return FMOD.SOUND_TYPE.VORBIS;
                case AudioStreamBase.StreamAudioType.WAV:
                    return FMOD.SOUND_TYPE.WAV;
                case AudioStreamBase.StreamAudioType.XM:
                    return FMOD.SOUND_TYPE.XM;
                case AudioStreamBase.StreamAudioType.XMA:
                    return FMOD.SOUND_TYPE.XMA;
                default:
                    throw new System.NotSupportedException("AudioStream.StreamAudioType not consistent");
            }
        }

        public static FMOD.VECTOR ToFMODVector(this Vector3 value)
        {
            FMOD.VECTOR result = new FMOD.VECTOR();
            result.x = value.x;
            result.y = value.y;
            result.z = value.z;

            return result;
        }

        public static FMOD.SPEAKERMODE ToFMODSpeakerMode(this AudioSpeakerMode unitySpeakerMode)
        {
            switch (unitySpeakerMode)
            {
                case AudioSpeakerMode.Mono:
                    return FMOD.SPEAKERMODE.MONO;
                case AudioSpeakerMode.Stereo:
                    return FMOD.SPEAKERMODE.STEREO;
                case AudioSpeakerMode.Quad:
                    return FMOD.SPEAKERMODE.QUAD;
                case AudioSpeakerMode.Surround:
                    return FMOD.SPEAKERMODE.SURROUND;
                case AudioSpeakerMode.Mode5point1:
                    return FMOD.SPEAKERMODE._5POINT1;
                case AudioSpeakerMode.Mode7point1:
                    return FMOD.SPEAKERMODE._7POINT1;
                case AudioSpeakerMode.Prologic:
                    return FMOD.SPEAKERMODE.STEREO;
                default:
                    return FMOD.SPEAKERMODE.DEFAULT;
            }
        }
        /// <summary>
        /// More usage friendly FMOD.DSP_PARAMETER_DESC
        /// simple value types/enums used from original
        /// byte[] string, IntPtr fields
        /// </summary>
        public struct DSP_PARAMETER_DESC
        {
            public FMOD.DSP_PARAMETER_TYPE type;
            public string name;
            public string unitLabel;
            public string description;

            public DSP_PARAMETER_DESC_UNION desc;
        }
        public struct DSP_PARAMETER_DESC_UNION
        {
            public DSP_PARAMETER_DESC_FLOAT floatdesc;
            public DSP_PARAMETER_DESC_INT intdesc;
            public DSP_PARAMETER_DESC_BOOL booldesc;
            public DSP_PARAMETER_DESC_DATA datadesc;
        }
        public struct DSP_PARAMETER_DESC_FLOAT
        {
            public float min;
            public float max;
            public float defaultval;
            public DSP_PARAMETER_FLOAT_MAPPING mapping;
        }
        public struct DSP_PARAMETER_DESC_INT
        {
            public int min;
            public int max;
            public int defaultval;
            public bool goestoinf;
            public string[] valuenames; // max - min + 1
        }
        public struct DSP_PARAMETER_DESC_BOOL
        {
            public bool defaultval;
            public string[] valuenames; // 2
        }
        public struct DSP_PARAMETER_DESC_DATA
        {
            public FMOD.DSP_PARAMETER_DATA_TYPE datatype;
        }


        public struct DSP_PARAMETER_FLOAT_MAPPING
        {
            public FMOD.DSP_PARAMETER_FLOAT_MAPPING_TYPE type;
            public DSP_PARAMETER_FLOAT_MAPPING_PIECEWISE_LINEAR piecewiselinearmapping;
        }

        public struct DSP_PARAMETER_FLOAT_MAPPING_PIECEWISE_LINEAR
        {
            public int numpoints;
            public float[] pointparamvalues;
            public float[] pointpositions;
        }

        public static DSP_PARAMETER_DESC ToAudioStreamDSP_PARAMETER_DESC(this FMOD.DSP_PARAMETER_DESC fmod_DSP_PARAMETER_DESC)
        {
            // https://fmod.com/docs/2.02/api/glossary.html#string-format
            // All FMOD Public APIs and structures use UTF-8 strings.
            // for many things byte[16] strings are broken after conversion, having bunch of \0 chars more often than not

            var result = new DSP_PARAMETER_DESC();

            result.type = fmod_DSP_PARAMETER_DESC.type;

            result.name = StringHelper.ByteArrayToString(fmod_DSP_PARAMETER_DESC.name);
            result.unitLabel = StringHelper.ByteArrayToString(fmod_DSP_PARAMETER_DESC.label);
            result.description = fmod_DSP_PARAMETER_DESC.description;

            switch (result.type)
            {
                case FMOD.DSP_PARAMETER_TYPE.FLOAT:
                    result.desc.floatdesc = new DSP_PARAMETER_DESC_FLOAT();
                    result.desc.floatdesc.min = fmod_DSP_PARAMETER_DESC.desc.floatdesc.min;
                    result.desc.floatdesc.max = fmod_DSP_PARAMETER_DESC.desc.floatdesc.max;
                    result.desc.floatdesc.defaultval = fmod_DSP_PARAMETER_DESC.desc.floatdesc.defaultval;
                    result.desc.floatdesc.mapping = new DSP_PARAMETER_FLOAT_MAPPING();
                    result.desc.floatdesc.mapping.type = fmod_DSP_PARAMETER_DESC.desc.floatdesc.mapping.type;

                    if (result.desc.floatdesc.mapping.type == FMOD.DSP_PARAMETER_FLOAT_MAPPING_TYPE.DSP_PARAMETER_FLOAT_MAPPING_TYPE_PIECEWISE_LINEAR)
                    {
                        result.desc.floatdesc.mapping.piecewiselinearmapping = new DSP_PARAMETER_FLOAT_MAPPING_PIECEWISE_LINEAR();
                        result.desc.floatdesc.mapping.piecewiselinearmapping.numpoints = fmod_DSP_PARAMETER_DESC.desc.floatdesc.mapping.piecewiselinearmapping.numpoints;

                        // etc
                        // fmod_DSP_PARAMETER_DESC.desc.floatdesc.mapping.piecewiselinearmapping.numpoints -1492209792

                        result.desc.floatdesc.mapping.piecewiselinearmapping.pointparamvalues = new float[result.desc.floatdesc.mapping.piecewiselinearmapping.numpoints];
                        result.desc.floatdesc.mapping.piecewiselinearmapping.pointpositions = new float[result.desc.floatdesc.mapping.piecewiselinearmapping.numpoints];

                        // fmod_DSP_PARAMETER_DESC.desc.floatdesc.mapping.piecewiselinearmapping.pointparamvalues == 0x18 .. ?
                        // fmod_DSP_PARAMETER_DESC.desc.floatdesc.mapping.piecewiselinearmapping.pointpositions == 0x0
                        /*
                        if (fmod_DSP_PARAMETER_DESC.desc.floatdesc.mapping.piecewiselinearmapping.pointparamvalues.ToInt64() != 0)
                            Marshal.Copy(fmod_DSP_PARAMETER_DESC.desc.floatdesc.mapping.piecewiselinearmapping.pointparamvalues
                                , result.desc.floatdesc.mapping.piecewiselinearmapping.pointparamvalues
                                , 0
                                , result.desc.floatdesc.mapping.piecewiselinearmapping.numpoints
                                );

                        if (fmod_DSP_PARAMETER_DESC.desc.floatdesc.mapping.piecewiselinearmapping.pointpositions.ToInt64() != 0)
                            Marshal.Copy(fmod_DSP_PARAMETER_DESC.desc.floatdesc.mapping.piecewiselinearmapping.pointpositions
                                , result.desc.floatdesc.mapping.piecewiselinearmapping.pointpositions
                                , 0
                                , result.desc.floatdesc.mapping.piecewiselinearmapping.numpoints
                                );
                        */
                    }
                    break;

                case FMOD.DSP_PARAMETER_TYPE.INT:
                    result.desc.intdesc = new DSP_PARAMETER_DESC_INT();
                    result.desc.intdesc.min = fmod_DSP_PARAMETER_DESC.desc.intdesc.min;
                    result.desc.intdesc.max = fmod_DSP_PARAMETER_DESC.desc.intdesc.max;
                    result.desc.intdesc.defaultval = fmod_DSP_PARAMETER_DESC.desc.intdesc.defaultval;
                    result.desc.intdesc.goestoinf = fmod_DSP_PARAMETER_DESC.desc.intdesc.goestoinf;
                    var l = result.desc.intdesc.max - result.desc.intdesc.min + 1;
                    result.desc.intdesc.valuenames = StringHelper.PtrToStringArray(l, fmod_DSP_PARAMETER_DESC.desc.intdesc.valuenames);
                    break;

                case FMOD.DSP_PARAMETER_TYPE.BOOL:
                    result.desc.booldesc = new DSP_PARAMETER_DESC_BOOL();
                    result.desc.booldesc.defaultval = fmod_DSP_PARAMETER_DESC.desc.booldesc.defaultval;
                    result.desc.booldesc.valuenames = StringHelper.PtrToStringArray(2, fmod_DSP_PARAMETER_DESC.desc.booldesc.valuenames);
                    break;

                case FMOD.DSP_PARAMETER_TYPE.DATA:
                    result.desc.datadesc = new DSP_PARAMETER_DESC_DATA();
                    result.desc.datadesc.datatype = (FMOD.DSP_PARAMETER_DATA_TYPE)fmod_DSP_PARAMETER_DESC.desc.datadesc.datatype;

                    break;
            }

            return result;
        }
    }
}