// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using System;
using System.IO;
using System.Linq;

namespace AudioStreamSupport
{
    public static class FileSystem
    {
        // ========================================================================================================================================
        #region DL cache
        /// <summary>
        /// Returns complete path of SHA512 unique hash of given (url + uniqueCacheId) in temp cache file path
        /// Appends 'extension' as file name extension
        /// </summary>
        /// <param name="fromUrl">Base url/filename</param>
        /// <param name="uniqueCacheId">Optional unique id which will be appended to url for having more than one cached downloads from a single source</param>
        /// <param name="extension"></param>
        /// <returns></returns>
        public static string TempFilePath(string fromUrl, string uniqueCacheId, string extension)
        {
            var fileName = FileSystem.EscapedBase64Hash(fromUrl + uniqueCacheId);
            return Path.Combine(RuntimeSettings.temporaryDirectoryPath, fileName + extension);
        }
        /// <summary>
        /// Returns complete filesystem path of url + extension in download cache directory
        /// Due to OS max. path length limit on Windows the result is truncated to first ~200 characters
        /// </summary>
        /// <param name="fromUrl"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        public static string DownloadCacheFilePath(string fromUrl, string extension)
        {
            var filename = FileSystem.ReplaceInvalidFilesystemCharacters(fromUrl, '_');
            var filepath = Path.Combine(RuntimeSettings.downloadCachePath, filename);
            var result = filepath.Substring(0, Math.Min(filepath.Length, 200));
            return result + extension;
        }
        public static string EscapedBase64Hash(string ofUri)
        {
            var byteArray = ofUri.ToCharArray().Select(s => (byte)s).ToArray<byte>();

            using (var sha = System.Security.Cryptography.SHA512.Create())
            {
                var hash = sha.ComputeHash(byteArray);

                return Uri.EscapeDataString(
                    Convert.ToBase64String(hash)
                    );
            }
        }
        #endregion
        // ========================================================================================================================================
        #region filesystem
        public static long DirectorySize(string path)
        {
            if (string.IsNullOrEmpty(path))
                return 0;

            var di = new System.IO.DirectoryInfo(path);
            if (!di.Exists)
                return 0;

            var result = di.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly).Sum(f => f.Length);
            return result;
        }
        /// <summary>
        /// just directly replaces any non filesystem character as considered by mono/.net by supplied one
        /// </summary>
        /// <param name="ofString"></param>
        /// <param name="withCharacter"></param>
        /// <returns></returns>
        public static string ReplaceInvalidFilesystemCharacters(string ofString, char withCharacter)
        {
            // "http://mydomain/_layouts/test/MyLinksEdit.aspx?auto=true&source=http://vtss-sp2010hh:8088/AdminReports/helloworld.aspx?pdfid=193&url=http://vtss-sp2010hh:8088/AdminReports/helloworld.aspx?pdfid=193%26pdfname=5.6%20Upgrade&title=5.6 Upgrade"
            var invalidFilenameChars = Path.GetInvalidFileNameChars();
            var invalidPathChars = Path.GetInvalidPathChars();

            var result = ofString.ToCharArray();
            for (var i = 0; i < result.Length; ++i)
            {
                var ch = result[i];
                if (invalidFilenameChars.Contains(ch)
                    || invalidPathChars.Contains(ch)
                    )
                    result[i] = withCharacter;
            }

            return new string(result);
        }
        #endregion
    }
}