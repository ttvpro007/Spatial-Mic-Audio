// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using System;
using System.IO;
using UnityEngine;

namespace AudioStreamSupport
{
    /// <summary>
    /// RuntimeSettings currently holding only user customizable cache directory path
    /// SO based on Resources asset
    /// </summary>
    // asset creation menu commented out for release to not clutter the Editor UI
    // [CreateAssetMenu(fileName = "AudioStreamRuntimeSettings", menuName = "AudioStream Runtime Settings", order = 1200)]
    public class RuntimeSettings : ScriptableObject
    {
        [Tooltip("Download cache location\r\nStores original streamed media\r\nLeave empty to use the default [Application.persistentDataPath] location")]
        [SerializeField] string _downloadCachePath = null;
        public static string downloadCachePath;
        [Tooltip("Temporary directory to store (mainly) samples for AudioClip.\r\nFiles can get quite large.\r\nLeave empty to use the default [Application.temporaryCachePath] location")]
        [SerializeField] string _temporaryDirectoryPath = null;
        public static string temporaryDirectoryPath;
        /// <summary>
        /// Load this SO from Resources and transfer instance fields 2 statics
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        static void LoadRuntimeSettings()
        {
            // cache in Application.persistentDataPath has to be separate since this directory contains also e.g. app logs
            // dir has to always exist
            var dlCacheDirectory = Path.Combine(Application.persistentDataPath, "AudioStream_DownloadCache");
            if (!Directory.Exists(dlCacheDirectory))
                Directory.CreateDirectory(dlCacheDirectory);

            var inst = Resources.Load<RuntimeSettings>("AudioStreamRuntimeSettings");
            if (inst)
            {
                if (string.IsNullOrEmpty(inst._downloadCachePath))
                    RuntimeSettings.downloadCachePath = dlCacheDirectory;
                else
                {
                    if (!System.IO.Directory.Exists(inst._downloadCachePath))
                    {
                        Debug.LogWarningFormat("Download cache directory doesn't exist, creating..");

                        try
                        {
                            System.IO.Directory.CreateDirectory(inst._downloadCachePath);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogErrorFormat("Failed: {0} {1} {2}", ex.Message, Environment.NewLine, ex.InnerException != null ? ex.InnerException.Message : String.Empty);
                        }
                    }

                    if (!System.IO.Directory.Exists(inst._downloadCachePath))
                    {
                        Debug.LogWarningFormat("Download cache directory '{0}' set in RuntimeSettings does not exit - will use the Application default ['{1}'] instead.", inst._downloadCachePath, dlCacheDirectory);
                        RuntimeSettings.downloadCachePath = dlCacheDirectory;
                    }
                    else
                        RuntimeSettings.downloadCachePath = inst._downloadCachePath;
                }


                if (string.IsNullOrEmpty(inst._temporaryDirectoryPath))
                    RuntimeSettings.temporaryDirectoryPath = Application.temporaryCachePath;
                else
                {
                    if (!System.IO.Directory.Exists(inst._temporaryDirectoryPath))
                    {
                        Debug.LogWarningFormat("Temp directory doesn't exist, creating..");

                        try
                        {
                            System.IO.Directory.CreateDirectory(inst._temporaryDirectoryPath);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogErrorFormat("Failed: {0} {1} {2}", ex.Message, Environment.NewLine, ex.InnerException != null ? ex.InnerException.Message : String.Empty);
                        }
                    }

                    if (!System.IO.Directory.Exists(inst._temporaryDirectoryPath))
                    {
                        Debug.LogWarningFormat("Temp directory '{0}' set in RuntimeSettings does not exit - will use the Application temp default ['{1}'] instead.", inst._temporaryDirectoryPath, Application.temporaryCachePath);
                        RuntimeSettings.temporaryDirectoryPath = Application.temporaryCachePath;
                    }
                    else
                        RuntimeSettings.temporaryDirectoryPath = inst._temporaryDirectoryPath;
                }
            }
            else
            {
                // is missing in build ->
                RuntimeSettings.downloadCachePath = dlCacheDirectory;
                RuntimeSettings.temporaryDirectoryPath = Application.temporaryCachePath;
            }
        }
    }
}