// ------------------------------------------------------------------------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.  See License in the project root for license information.
// ------------------------------------------------------------------------------------------------------------------------------------------------------

using System;

namespace KnownIssuesService.Models
{
    /// <summary>
    /// A Model that will hold the known issues data for front-end rendering
    /// </summary>
    public class KnownIssuesContract
    {
        /// <summary>
        /// Reference Id for every known issue
        /// </summary>
        public int? Id { get; set; }

        /// <summary>
        /// Known Issues Title from AZDO
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Known Issues Description from AZDO
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Microsoft Graph Workload Area
        /// </summary>
        public string WorkLoadArea { get; set; }

        /// <summary>
        /// Describes a possible way to solve the specific known issue
        /// </summary>
        public string WorkAround { get; set; }

        /// <summary>
        /// A Link to the Known Issues Specific Resource Documentation
        /// </summary>
        public string Link { get; set; }

        /// <summary>
        /// Date The Issue was Raised
        /// </summary>
        public DateTime CreatedDateTime { get; set; }

        /// <summary>
        /// Last Change on the Issue  Work Item
        /// </summary>
        public DateTime LastUpdatedDateTime { get; set; }

        /// <summary>
        /// Known Issues Status i.e New, Active,Resolved
        /// </summary>
        public string State { get; set; }
    }
}
