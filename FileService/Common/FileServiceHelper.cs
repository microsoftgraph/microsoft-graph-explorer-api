﻿// ------------------------------------------------------------------------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.  See License in the project root for license information.
// ------------------------------------------------------------------------------------------------------------------------------------------------------

using System;
using System.Globalization;

namespace FileService.Common
{
    /// <summary>
    /// Defines a static class that contains helper methods that handle common file operations.
    /// </summary>
    public static class FileServiceHelper
    {
        /// <summary>
        /// Check whether the input string is null or empty.
        /// </summary>
        /// <param name="value">The input string.</param>
        /// <param name="parameterName">The input parameter name.</param>
        internal static void CheckArgumentNullOrEmpty(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(parameterName, "Value cannot be null or empty.");
            }

            return;
        }

        /// <summary>
        /// Retrieves the directory and file names from a given a file path source.
        /// </summary>
        /// <param name="filePathSource">The complete file path.</param>
        /// <param name="directoryName">The expected directory name.</param>
        /// <param name="fileName">The expected file name.</param>
        internal static void RetrieveFilePathSourceValues(string filePathSource, out string directoryName, out string fileName)
        {
            directoryName = null;
            fileName = null;

            if (filePathSource.IndexOf(FileServiceConstants.DirectorySeparator) > 0)
            {
                // File path source format --> directoryName\\fileName
                string[] storageValues = filePathSource.Split(FileServiceConstants.DirectorySeparator);
                directoryName = storageValues[0];
                fileName = storageValues[1];
            }
        }

        /// <summary>
        /// Gets the full path identifier name for a localized file.
        /// </summary>
        /// <param name="containerName">The container holding the desired localized file.</param>
        /// <param name="defaultBlobName">The name of the default file.</param>
        /// <param name="langCode">The language code of the desired file. If empty or null, this default to 'en-US'.</param>
        /// <returns>A string path of the fully qualified file name including the container name prepended to the resolved localized file name.</returns>
        public static string GetLocalizedFilePathSource(string containerName, string defaultBlobName, string langCode = null)
        {            
            CheckArgumentNullOrEmpty(containerName, nameof(containerName));
            CheckArgumentNullOrEmpty(defaultBlobName, nameof(defaultBlobName));

            string localeCode;

            // This switch statement helps filter for only the supported locale languages
            switch (langCode.ToLower(CultureInfo.InvariantCulture))
            {
                case "fr-fr":
                    localeCode = "fr-FR";
                    break;
                case "es-es":
                    localeCode = "es-ES";
                    break;
                case "de-de":
                    localeCode = "de-DE";
                    break;
                case "ja-jp":
                    localeCode = "ja-JP";
                    break;
                case "pt-br":
                    localeCode = "pt-BR";
                    break;
                case "ru-ru":
                    localeCode = "ru-RU";
                    break;
                case "zh-cn":
                    localeCode = "zh-CN";
                    break;
                default:
                    localeCode = "en-US";
                    break;
            }

            if (defaultBlobName.IndexOf('.') > 0 && localeCode != "en-US")
            {
                /* All localized files have a consistent structure, e.g. sample-queries_fr-FR.json 
                   except for 'en-Us' --> sample-queries.json */

                string[] blobNameParts = defaultBlobName.Split('.');
                defaultBlobName = $"{blobNameParts[0]}_{localeCode}.{blobNameParts[1]}";
            }

            return $"{containerName}{FileServiceConstants.DirectorySeparator}{defaultBlobName}";
        }
    }
}
