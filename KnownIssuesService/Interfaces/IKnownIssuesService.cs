// ------------------------------------------------------------------------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.  See License in the project root for license information.
// ------------------------------------------------------------------------------------------------------------------------------------------------------

using KnownIssuesService.Models;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KnownIssuesService.Interfaces
{
    /// <summary>
    /// Defines an interface that helps in fetching a list of known issues in Azure Devops
    /// </summary>
    public interface IKnownIssuesService
    {
        Task<List<KnownIssuesContract>> QueryBugs();
        Task<WorkItemQueryResult> GetQueryByWiqlAsync();
        Task<List<WorkItem>> GetWorkItemsQueryAsync(int[] ids, WorkItemQueryResult result);
    }
}
