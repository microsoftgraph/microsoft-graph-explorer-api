// ------------------------------------------------------------------------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.  See License in the project root for license information.
// ------------------------------------------------------------------------------------------------------------------------------------------------------

using Microsoft.ApplicationInsights;
using System.Collections.Generic;

namespace UtilityService
{
    /// <summary>
    /// Provides commonly used reusable constants.
    /// </summary>
    public static class UtilityConstants
    {
        public const string TelemetryPropertyKey_SanitizeIgnore = "SanitizeIgnore";
        public const string TelemetryPropertyKey_Permissions = "Permissions";
        public const string TelemetryPropertyKey_Samples = "Samples";
        public const string TelemetryPropertyKey_Changes = "Changes";
        public const string TelemetryPropertyKey_OpenApi = "OpenApi";
        public const string TelemetryPropertyKey_Snippets = "Snippets";

        /// <summary>
		/// Contains the name of the Known Issues Azure DevOps Organisation
		/// </summary>
		public const string knownIssuesOrganisation = "Known Issues (staging)";
    }
}