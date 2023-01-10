using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AudioStreamEditor
{
    public static class AudioStreamEditor
    {
        // ========================================================================================================================================
        #region Demo StreamingAssets Editor support
        /// <summary>
        /// Will be called after Editor reload which is more than enough (e.g. after the asset import)
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod()]
        static void SetupStreamingAssets()
        {
            // check for file existence should be very quick
            var flagFilename = Path.Combine(Path.Combine(Application.streamingAssetsPath, "AudioStream"), "_audiostream_demo_assets_prepared");

            if (!File.Exists(flagFilename))
            {
                SetupStreamingAssetsIfNeeded();
                using (var f = File.Create(flagFilename)) { f.Close(); }
            }
        }
        /// <summary>
        /// Copies runtime demo assets into application StreamingAssets fdolder if they don't exist there already and the asset location hasn't moved
        /// (which should be the case after initial import; if not, the user who imported elsewhere should be able to fix it anyway)
        /// Might need assets refresh to show up in the Editor
        /// </summary>
        static void SetupStreamingAssetsIfNeeded()
        {
            // get the list of assets in 'AudioStream/StreamingAssets'
            List<string> asStreamingAssets = new List<string>();

            // search in all Assets, package could be imported anywhere..
            // TODO: there will be fun when asset store packages arrive..
            foreach (var s in UnityEditor.AssetDatabase.FindAssets("t:Object", new string[] { "Assets" }))
            {
                // convert object's GUID to asset path
                var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(s);

                // add the asset path
                if (assetPath.ToLowerInvariant().Contains("audiostream/streamingassets/"))
                    asStreamingAssets.Add(assetPath);
            }

            if (asStreamingAssets.Count > 0)
            {
                // list of files in 'StreamingAssets'
                // - directory must exist for the call to succeed..
                var dirname = Path.Combine(Application.streamingAssetsPath, "AudioStream");

                if (!Directory.Exists(dirname))
                    Directory.CreateDirectory(dirname);

                var streamingAssetsContent = Directory.GetFiles(dirname, "*.*");

                foreach (var asStreamingAsset in asStreamingAssets)
                    if (!streamingAssetsContent.Select(s => Path.GetFileName(s)).Contains(Path.GetFileName(asStreamingAsset)))
                    {
                        var src = asStreamingAsset;
                        var dst = Path.Combine(dirname, Path.GetFileName(asStreamingAsset));
                        Debug.LogWarningFormat("One time copy of AudioStream demo asset: {0} into project StreamingAssets: {1}", src, dst);
                        File.Copy(src, dst);
                    }
            }
        }
        #endregion
    }
}