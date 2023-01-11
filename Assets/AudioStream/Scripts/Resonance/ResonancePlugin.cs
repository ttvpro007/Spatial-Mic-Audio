// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioStream
{
    public class ResonancePlugin
    {
        // ========================================================================================================================================
        #region Editor simulacrum
        LogLevel logLevel = LogLevel.INFO;
        string gameObjectName = "Resonance Plugin";
        #endregion

        // ========================================================================================================================================
        #region Support
        void ERRCHECK(FMOD.RESULT result, string customMessage, bool throwOnError = true)
        {
            this.lastError = this.result;
            FMODHelpers.ERRCHECK(this.result, this.logLevel, this.gameObjectName, null, customMessage, throwOnError);
        }

        void LOG(LogLevel requestedLogLevel, string format, params object[] args)
        {
            Log.LOG(requestedLogLevel, this.logLevel, this.gameObjectName, format, args);
        }

        public string GetLastError(out FMOD.RESULT errorCode)
        {
            errorCode = this.lastError;
            return FMOD.Error.String(errorCode);
        }
        #endregion

        // ========================================================================================================================================
        #region FMOD
        FMOD.System system;
        FMOD.RESULT result = FMOD.RESULT.OK;
        FMOD.RESULT lastError = FMOD.RESULT.OK;
        FMOD.ChannelGroup master;
        #endregion

        // ========================================================================================================================================
        #region FMOD nested plugins
        uint resonancePlugin_handle = 0;

        // IDs from plugin DSPs descriptions logging
        const int ResonanceListener_DSP_index = 0;
        const int ResonanceListener_paramID_Gain = 0;               // [-80.0 to 0.0] Default = 0.0
        const int ResonanceListener_paramID_RoomProperties = 1;

        const int ResonanceSoundfield_DSP_index = 1;
        const int ResonanceSoundfield_paramID_Gain = 0;             // [-80.0 to 24.0f] Default = 0.0
        const int ResonanceSoundfield_paramID_3DAttributes = 1;
        const int ResonanceSoundfield_paramID_OverallGain = 2;      // Overall Gain    .(?)

        const int ResonanceSource_DSP_index = 2;
        const int ResonanceSource_paramID_Gain = 0;                 // [-80.0 to 24.0f] Default = 0.0
        const int ResonanceSource_paramID_Spread = 1;               // Spread in degrees. Default 0.0
        const int ResonanceSource_paramID_DistRolloff = 2;          // LINEAR, LOGARITHMIC, NONE, LINEAR SQUARED, LOGARITHMIC TAPERED. Default = LOGARITHMIC
        const int ResonanceSource_paramID_Occlusion = 3;            // [0.0 to 10.0] Default = 0.0
        const int ResonanceSource_paramID_Directivity = 4;          // [0.0 to 1.0] Default = 0.0
        const int ResonanceSource_paramID_DirSharpness = 5;         // [1.0 to 10.0)] Default = 1.0
        const int ResonanceSource_paramID_AttenuationRange = 6;
        const int ResonanceSource_paramID_3DAttributes = 7;         
        const int ResonanceSource_paramID_BypassRoom = 8;           // Bypass room effects. Default = false
        const int ResonanceSource_paramID_NearFieldFX = 9;          // Enable Near-Field Effects. Default = false
        const int ResonanceSource_paramID_NearFieldGain = 10;       // [0.0 to 9.0)] Default = 1.0
        const int ResonanceSource_paramID_OverallGain = 11;          // Overall Gain    .(?)

        [System.Serializable()]
        public enum DistanceRolloff
        {
            LINEAR = 0
                , LOGARITHMIC = 1
                , NONE = 2
                , LINEAR_SQUARED = 3
                , LOGARITHMIC_TAPERED = 4
        }
        #endregion
        // ========================================================================================================================================
        #region Plugin singleton/s
        static ResonancePlugin instance = null;
        static int instance_refc = 0;
        public static ResonancePlugin Load(FMOD.System forSystem, LogLevel logLevel)
        {
            if (ResonancePlugin.instance == null)
            {
                ResonancePlugin.instance = new ResonancePlugin(forSystem, logLevel);
                ResonancePlugin.instance.LOG(LogLevel.INFO, "Loaded Resonance");
            }

            ResonancePlugin.instance_refc++;
            return ResonancePlugin.instance;
        }
        public static void Unload()
        {
            ResonancePlugin.instance_refc--;

            if (ResonancePlugin.instance_refc <= 0)
            {
                ResonancePlugin.instance_refc = 0;

                if (ResonancePlugin.instance != null)
                {
                    ResonancePlugin.instance.LOG(LogLevel.INFO, "Unloading Resonance");
                    ResonancePlugin.instance.Release();
                    ResonancePlugin.instance = null;
                }
            }
        }

        public static FMOD.DSP New_ResonanceSource_DSP()
        {
            FMOD.DSP dsp = default(FMOD.DSP);

            if (ResonancePlugin.instance != null)
                dsp = ResonancePlugin.instance.LoadDSP(ResonanceSource_DSP_index);

            return dsp;
        }

        public static FMOD.DSP New_ResonanceListener_DSP()
        {
            FMOD.DSP dsp = default(FMOD.DSP);

            if (ResonancePlugin.instance != null)
                dsp = ResonancePlugin.instance.LoadDSP(ResonanceListener_DSP_index);

            return dsp;
        }

        public static FMOD.DSP New_ResonanceSoundfield_DSP()
        {
            FMOD.DSP dsp = default(FMOD.DSP);

            if (ResonancePlugin.instance != null)
                dsp = ResonancePlugin.instance.LoadDSP(ResonanceSoundfield_DSP_index);

            return dsp;
        }

        FMOD.DSP LoadDSP(int atIndex)
        {
            FMOD.DSP dsp;

            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                uint handle; // handle of the registered DSP plugin
                int numparams; // parameters check for loaded DSP

                result = this.system.getPluginHandle(FMOD.PLUGINTYPE.DSP, atIndex, out handle);
                ERRCHECK(result, "system.getPluginHandle");

                result = this.system.createDSPByPlugin(handle, out dsp);
                ERRCHECK(result, "system.createDSPByPlugin");

                result = dsp.getNumParameters(out numparams);
                ERRCHECK(result, "dsp.getNumParameters");

                for (var p = 0; p < numparams; ++p)
                {
                    FMOD.DSP_PARAMETER_DESC paramdesc;
                    result = dsp.getParameterInfo(p, out paramdesc);
                    ERRCHECK(result, "dsp.getParameterInfo");

                    string p_name = StringHelper.ByteArrayToString(paramdesc.name);
                    string p_label = StringHelper.ByteArrayToString(paramdesc.label);
                    var p_description = paramdesc.description;

                    this.LOG(LogLevel.DEBUG, "DSP {0} || param: {1} || type: {2} || name: {3} || label: {4} || description: {5}", 0, p, paramdesc.type, p_name, p_label, p_description);
                }
            }
            else
            {
                /*
                 * Load nested plugin
                 */
                uint nestedHandle;
                result = this.system.getNestedPlugin(this.resonancePlugin_handle, atIndex, out nestedHandle);
                ERRCHECK(result, "system.getNestedPlugin");

                FMOD.PLUGINTYPE pluginType;
                int namelen = 255;
                string dspPluginName;
                uint version;
                result = this.system.getPluginInfo(nestedHandle, out pluginType, out dspPluginName, namelen, out version);

                this.LOG(LogLevel.DEBUG, "DSP {0} || plugin type: {1} || plugin name: {2} || version: {3}", atIndex, pluginType, dspPluginName, version);

                /*
                 * Create DSP effect
                 */
                result = this.system.createDSPByPlugin(nestedHandle, out dsp);
                ERRCHECK(result, "system.createDSPByPlugin");

                /*
                 * dsp.getInfo seems to be unused
                 */
                /*
                 * check DSP parameters list
                 */
                int numparams;
                result = dsp.getNumParameters(out numparams);
                ERRCHECK(result, "dsp.getNumParameters");

                for (var p = 0; p < numparams; ++p)
                {
                    FMOD.DSP_PARAMETER_DESC paramdesc;
                    result = dsp.getParameterInfo(p, out paramdesc);
                    ERRCHECK(result, "dsp.getParameterInfo");

                    string p_name = StringHelper.ByteArrayToString(paramdesc.name);
                    string p_label = StringHelper.ByteArrayToString(paramdesc.label);
                    var p_description = paramdesc.description;

                    this.LOG(LogLevel.DEBUG, "DSP {0} || param: {1} || type: {2} || name: {3} || label: {4} || description: {5}", atIndex, p, paramdesc.type, p_name, p_label, p_description);
                }
            }

            return dsp;
        }

        ResonancePlugin(FMOD.System forSystem, LogLevel _logLevel)
        {
            this.system = forSystem;
            this.logLevel = _logLevel;

            /*
             * Load Resonance plugin
             * On platforms which support it and binary is provided for, load dynamically
             * On iOS/tvOS plugin is statically linked, and enabled via fmodplugins.cpp (which has to be imported from FMOD Unity Integration and called manually from native side)
             */
            if (Application.platform != RuntimePlatform.IPhonePlayer)
            {
                string pluginName = string.Empty;
                // load from a particular folder in Editor
                // TODO: hardcoded FMOD in Editor
                // plugins in player are in 'Plugins/arch' in 2019.3.10 | 'Plugins' in 2017 and 2018 / 2.00.08 

                // folder structure in FMOD package changed around 2.02 (? probably)
                var pluginsEditorPathPrefix = FMOD.VERSION.number > 0x00020200 ? "Plugins/FMOD/platforms" : "Plugins/FMOD/lib";
                var pluginsEditorPathInfix = FMOD.VERSION.number > 0x00020200 ? "lib/" : "";

                var pluginsPath = Application.isEditor ? Path.Combine(Application.dataPath, pluginsEditorPathPrefix) : Path.Combine(Application.dataPath, "Plugins");
                bool arch64 = Platform.Is64bitArchitecture();

                switch (Application.platform)
                {
                    case RuntimePlatform.WindowsEditor:
                        if (arch64)
                            pluginName = Path.Combine(Path.Combine(pluginsPath, string.Format("win/{0}x86_64", pluginsEditorPathInfix)), "resonanceaudio");
                        else
                            pluginName = Path.Combine(Path.Combine(pluginsPath, string.Format("win/{0}x86", pluginsEditorPathInfix)), "resonanceaudio");
                        break;

                    case RuntimePlatform.LinuxEditor:
                        if (arch64)
                            pluginName = Path.Combine(Path.Combine(pluginsPath, string.Format("linux/{0}x86_64", pluginsEditorPathInfix)), "resonanceaudio");
                        else
                            // linux x86 binary in 2.00.03 doesn't seem to be provided though.. 2.02 - still true
                            pluginName = Path.Combine(Path.Combine(pluginsPath, string.Format("linux/{0}x86", pluginsEditorPathInfix)), "resonanceaudio");
                        break;

                    case RuntimePlatform.OSXEditor:
                        pluginName = Path.Combine(Path.Combine(pluginsPath, string.Format("mac/{0}", pluginsEditorPathInfix)), "resonanceaudio.bundle");
                        break;

                    case RuntimePlatform.WindowsPlayer:
                    case RuntimePlatform.LinuxPlayer:
#if UNITY_2019_1_OR_NEWER
                        if (arch64)
                            pluginName = Path.Combine(Path.Combine(pluginsPath, "x86_64"), "resonanceaudio");
                        else
                            pluginName = Path.Combine(Path.Combine(pluginsPath, "x86"), "resonanceaudio");
#else
                        // original behaviour
                        if (arch64)
                            pluginName = Path.Combine(pluginsPath, "resonanceaudio");
                        else
                            pluginName = Path.Combine(pluginsPath, "resonanceaudio");
#endif
                        break;

                    case RuntimePlatform.OSXPlayer:
                        pluginName = Path.Combine(pluginsPath, "resonanceaudio.bundle");
                        break;

                    case RuntimePlatform.Android:
                        /*
                         * load library with fully qualified name from hinted folder
                         */
                        pluginsPath = Path.Combine(Application.dataPath, "lib");

                        result = system.setPluginPath(pluginsPath);
                        ERRCHECK(result, "system.setPluginPath");

                        pluginName = "libresonanceaudio.so";
                        break;

                    default:
                        throw new NotSupportedException("Platform not supported.");
                }

                this.LOG(LogLevel.DEBUG, "Loading '{0}'", pluginName);

                result = system.loadPlugin(pluginName, out this.resonancePlugin_handle);
                ERRCHECK(result, string.Format("system.loadPlugin at {0}", pluginName));

                /*
                 * Create DSPs from all nested plugins, test enumerate && info for parameters
                 */
                int numNestedPlugins;
                result = system.getNumNestedPlugins(this.resonancePlugin_handle, out numNestedPlugins);
                ERRCHECK(result, "system.getNumNestedPlugins");

                this.LOG(LogLevel.DEBUG, "Got {0} nested plugins", numNestedPlugins);
            }
        }
        /// <summary>
        /// Remove and release all DSPs and unload plugin
        /// </summary>
        void Release()
        {
            // this takes few updates to release the plugin properly w/ loop on main thread but seems to not be too excessive
            // potentially limit # of updates
            do
            {
                result = system.update(); /* Process the DSP queue */
                ERRCHECK(result, "system.update", false);

                result = system.unloadPlugin(resonancePlugin_handle);
                if (result == FMOD.RESULT.ERR_DSP_INUSE)
                {
                    var t = 10;
                    this.LOG(LogLevel.INFO, "Waiting {0} ms for DSPs to be released ...", t);
                    System.Threading.Thread.Sleep(t);
                }

            } while (result == FMOD.RESULT.ERR_DSP_INUSE);
        }
        #endregion
        // ========================================================================================================================================
        #region ResonanceListener
        public void ResonanceListener_SetGain(float gain, FMOD.DSP ofDSP)
        {
            result = ofDSP.setParameterFloat(ResonanceListener_paramID_Gain, gain);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceListener_GetGain(FMOD.DSP ofDSP)
        {
            float fvalue;
            result = ofDSP.getParameterFloat(ResonanceListener_paramID_Gain, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceListener_SetRoomProperties(FMOD.DSP ofDSP)
        {
            // TODO: finish room

            // Set the room properties to a null room, which will effectively disable the room effects.
            result = ofDSP.setParameterData(ResonanceListener_paramID_RoomProperties, IntPtr.Zero.ToBytes(0));
            ERRCHECK(result, "dsp.setParameterData", false);
        }
        #endregion

        // ========================================================================================================================================
        #region ResonanceSoundfield
        public void ResonanceSoundfield_SetGain(float gain, FMOD.DSP ofDSP)
        {
            result = ofDSP.setParameterFloat(ResonanceSoundfield_paramID_Gain, gain);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceSoundfield_GetGain(FMOD.DSP ofDSP)
        {
            float fvalue;
            result = ofDSP.getParameterFloat(ResonanceSoundfield_paramID_Gain, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceSoundfield_Set3DAttributes(Vector3 relative_position, Vector3 relative_velocity, Vector3 relative_forward, Vector3 relative_up
            , Vector3 absolute_position, Vector3 absolute_velocity, Vector3 absolute_forward, Vector3 absolute_up
            , FMOD.DSP ofDSP
            )
        {
            FMOD.DSP_PARAMETER_3DATTRIBUTES_MULTI attributes = new FMOD.DSP_PARAMETER_3DATTRIBUTES_MULTI();
            attributes.numlisteners = 1;
            attributes.relative = new FMOD.ATTRIBUTES_3D[1];
            attributes.relative[0].position = relative_position.ToFMODVector();
            attributes.relative[0].velocity = relative_velocity.ToFMODVector();
            attributes.relative[0].forward = relative_forward.ToFMODVector();
            attributes.relative[0].up = relative_up.ToFMODVector();
            attributes.weight = new float[1];
            attributes.weight[0] = 1f;

            attributes.absolute.position = absolute_position.ToFMODVector();
            attributes.absolute.velocity = absolute_velocity.ToFMODVector();
            attributes.absolute.forward = absolute_forward.ToFMODVector();
            attributes.absolute.up = absolute_up.ToFMODVector();

            // copy struct to ptr to array
            // plugin can't access class' managed member - provide data on stack
            // TODO: see if there's better way to marshal this
            int attributes_size = Marshal.SizeOf(attributes);
            IntPtr attributes_ptr = Marshal.AllocHGlobal(attributes_size);

            Marshal.StructureToPtr(attributes, attributes_ptr, true);
            byte[] attributes_arr = attributes_ptr.ToBytes(attributes_size);

            result = ofDSP.setParameterData(ResonanceSoundfield_paramID_3DAttributes, attributes_arr);
            ERRCHECK(result, "dsp.setParameterData", false);

            Marshal.DestroyStructure(attributes_ptr, typeof(FMOD.DSP_PARAMETER_3DATTRIBUTES_MULTI));

            Marshal.FreeHGlobal(attributes_ptr);
        }

        public void ResonanceSoundfield_SetOverallGain(float linear_gain, float linear_gain_additive
            , FMOD.DSP ofDSP
            )
        {
            FMOD.DSP_PARAMETER_OVERALLGAIN @params = new FMOD.DSP_PARAMETER_OVERALLGAIN();
            @params.linear_gain = linear_gain;
            @params.linear_gain_additive = linear_gain_additive;

            // copy struct to ptr to array
            // plugin can't access class' managed member - provide data on stack
            // TODO: see if there's better way to marshal this
            int attributes_size = Marshal.SizeOf(@params);
            IntPtr attributes_ptr = Marshal.AllocHGlobal(attributes_size);

            Marshal.StructureToPtr(@params, attributes_ptr, true);
            byte[] attributes_arr = attributes_ptr.ToBytes(attributes_size);

            result = ofDSP.setParameterData(ResonanceSoundfield_paramID_OverallGain, attributes_arr);
            ERRCHECK(result, "dsp.setParameterData", false);

            Marshal.DestroyStructure(attributes_ptr, typeof(FMOD.DSP_PARAMETER_OVERALLGAIN));

            Marshal.FreeHGlobal(attributes_ptr);
        }
        #endregion

        // ========================================================================================================================================
        #region ResonanceSource
        public void ResonanceSource_SetGain(float gain, FMOD.DSP ofDSP)
        {
            result = ofDSP.setParameterFloat(ResonanceSource_paramID_Gain, gain);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceSource_GetGain(FMOD.DSP ofDSP)
        {
            float fvalue;
            result = ofDSP.getParameterFloat(ResonanceSource_paramID_Gain, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceSource_SetSpread(float spread, FMOD.DSP ofDSP)
        {
            result = ofDSP.setParameterFloat(ResonanceSource_paramID_Spread, spread);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceSource_GetSpread(FMOD.DSP ofDSP)
        {
            float fvalue;
            result = ofDSP.getParameterFloat(ResonanceSource_paramID_Spread, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceSource_SetDistanceRolloff(DistanceRolloff distanceRolloff, FMOD.DSP ofDSP)
        {
            result = ofDSP.setParameterInt(ResonanceSource_paramID_DistRolloff, (int)distanceRolloff);
            ERRCHECK(result, "dsp.setParameterInt", false);
        }

        public DistanceRolloff ResonanceSource_GetDistanceRolloff(FMOD.DSP ofDSP)
        {
            int ivalue;
            result = ofDSP.getParameterInt(ResonanceSource_paramID_DistRolloff, out ivalue);
            ERRCHECK(result, "dsp.getParameterInt", false);

            return (DistanceRolloff)ivalue;
        }

        public void ResonanceSource_SetOcclusion(float occlusion, FMOD.DSP ofDSP)
        {
            result = ofDSP.setParameterFloat(ResonanceSource_paramID_Occlusion, occlusion);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceSource_GetOcclusion(FMOD.DSP ofDSP)
        {
            float fvalue;
            result = ofDSP.getParameterFloat(ResonanceSource_paramID_Occlusion, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceSource_SetDirectivity(float directivity, FMOD.DSP ofDSP)
        {
            result = ofDSP.setParameterFloat(ResonanceSource_paramID_Directivity, directivity);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceSource_GetDirectivity(FMOD.DSP ofDSP)
        {
            float fvalue;
            result = ofDSP.getParameterFloat(ResonanceSource_paramID_Directivity, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceSource_SetDirectivitySharpness(float directivitySharpness, FMOD.DSP ofDSP)
        {
            result = ofDSP.setParameterFloat(ResonanceSource_paramID_DirSharpness, directivitySharpness);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceSource_GetDirectivitySharpness(FMOD.DSP ofDSP)
        {
            float fvalue;
            result = ofDSP.getParameterFloat(ResonanceSource_paramID_DirSharpness, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }

        public void ResonanceSource_SetAttenuationRange(float min, float max, FMOD.DSP ofDSP)
        {
            FMOD.DSP_PARAMETER_ATTENUATION_RANGE @params = new FMOD.DSP_PARAMETER_ATTENUATION_RANGE();
            @params.min = min;
            @params.max = max;

            // copy struct to ptr to array
            // plugin can't access class' managed member - provide data on stack
            // TODO: see if there's better way to marshal this
            int attributes_size = Marshal.SizeOf(@params);
            IntPtr attributes_ptr = Marshal.AllocHGlobal(attributes_size);

            Marshal.StructureToPtr(@params, attributes_ptr, true);
            byte[] attributes_arr = attributes_ptr.ToBytes(attributes_size);

            result = ofDSP.setParameterData(ResonanceSource_paramID_AttenuationRange, attributes_arr);
            ERRCHECK(result, "dsp.setParameterData", false);

            Marshal.DestroyStructure(attributes_ptr, typeof(FMOD.DSP_PARAMETER_ATTENUATION_RANGE));

            Marshal.FreeHGlobal(attributes_ptr);
        }

        public void ResonanceSource_Set3DAttributes(Vector3 relative_position, Vector3 relative_velocity, Vector3 relative_forward, Vector3 relative_up
            , Vector3 absolute_position, Vector3 absolute_velocity, Vector3 absolute_forward, Vector3 absolute_up
            , FMOD.DSP ofDSP)
        {
            FMOD.DSP_PARAMETER_3DATTRIBUTES attributes = new FMOD.DSP_PARAMETER_3DATTRIBUTES();

            attributes.relative.position = relative_position.ToFMODVector();
            attributes.relative.velocity = relative_velocity.ToFMODVector();
            attributes.relative.forward = relative_forward.ToFMODVector();
            attributes.relative.up = relative_up.ToFMODVector();

            attributes.absolute.position = absolute_position.ToFMODVector();
            attributes.absolute.velocity = absolute_velocity.ToFMODVector();
            attributes.absolute.forward = absolute_forward.ToFMODVector();
            attributes.absolute.up = absolute_up.ToFMODVector();

            // copy struct to ptr to array
            // plugin can't access class' managed member - provide data on stack
            // TODO: see if there's better way to marshal this
            int attributes_size = Marshal.SizeOf(attributes);
            IntPtr attributes_ptr = Marshal.AllocHGlobal(attributes_size);

            Marshal.StructureToPtr(attributes, attributes_ptr, true);
            byte[] attributes_arr = attributes_ptr.ToBytes(attributes_size);

            result = ofDSP.setParameterData(ResonanceSource_paramID_3DAttributes, attributes_arr);
            ERRCHECK(result, "dsp.setParameterData", false);

            Marshal.DestroyStructure(attributes_ptr, typeof(FMOD.DSP_PARAMETER_3DATTRIBUTES));

            Marshal.FreeHGlobal(attributes_ptr);
        }

        public void ResonanceSource_SetBypassRoom(bool bypassRoom, FMOD.DSP ofDSP)
        {
            result = ofDSP.setParameterBool(ResonanceSource_paramID_BypassRoom, bypassRoom);
            ERRCHECK(result, "dsp.setParameterBool", false);
        }

        public bool ResonanceSource_GetBypassRoom(FMOD.DSP ofDSP)
        {
            bool bvalue;
            result = ofDSP.getParameterBool(ResonanceSource_paramID_BypassRoom, out bvalue);
            ERRCHECK(result, "dsp.getParameterBool", false);

            return bvalue;
        }
        public void ResonanceSource_SetNearFieldFX(bool on, FMOD.DSP ofDSP)
        {
            result = ofDSP.setParameterBool(ResonanceSource_paramID_NearFieldFX, on);
            ERRCHECK(result, "dsp.setParameterBool", false);
        }

        public bool ResonanceSource_GetNearFieldFX(FMOD.DSP ofDSP)
        {
            bool bvalue;
            result = ofDSP.getParameterBool(ResonanceSource_paramID_NearFieldFX, out bvalue);
            ERRCHECK(result, "dsp.getParameterBool", false);

            return bvalue;
        }

        public void ResonanceSource_SetNearFieldGain(float gain, FMOD.DSP ofDSP)
        {
            result = ofDSP.setParameterFloat(ResonanceSource_paramID_NearFieldGain, gain);
            ERRCHECK(result, "dsp.setParameterFloat", false);
        }

        public float ResonanceSource_GetNearFieldGain(FMOD.DSP ofDSP)
        {
            float fvalue;
            result = ofDSP.getParameterFloat(ResonanceSource_paramID_NearFieldGain, out fvalue);
            ERRCHECK(result, "dsp.getParameterFloat", false);

            return fvalue;
        }
        public void ResonanceSource_SetOverallGain(float linear_gain, float linear_gain_additive, FMOD.DSP ofDSP)
        {
            FMOD.DSP_PARAMETER_OVERALLGAIN @params = new FMOD.DSP_PARAMETER_OVERALLGAIN();
            @params.linear_gain = linear_gain;
            @params.linear_gain_additive = linear_gain_additive;

            // copy struct to ptr to array
            // plugin can't access class' managed member - provide data on stack
            // TODO: see if there's better way to marshal this
            int attributes_size = Marshal.SizeOf(@params);
            IntPtr attributes_ptr = Marshal.AllocHGlobal(attributes_size);

            Marshal.StructureToPtr(@params, attributes_ptr, true);
            byte[] attributes_arr = attributes_ptr.ToBytes(attributes_size);

            result = ofDSP.setParameterData(ResonanceSource_paramID_OverallGain, attributes_arr);
            ERRCHECK(result, "dsp.setParameterData", false);

            Marshal.DestroyStructure(attributes_ptr, typeof(FMOD.DSP_PARAMETER_OVERALLGAIN));

            Marshal.FreeHGlobal(attributes_ptr);
        }
        #endregion
    }
}