// ------------------------------------------------------------------------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.  See License in the project root for license information.
// ------------------------------------------------------------------------------------------------------------------------------------------------------

using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;
using System.Threading;

namespace KnownIssuesService.Test
{
    /// <summary>
    /// This class creates a mocked instance of a WorkItemTracking HttpClient for tracking work items
    /// </summary>
    public class WorkItemTrackingHttpClientMock
    {
        private readonly Mock<WorkItemTrackingHttpClient> _workItemTrackHttpClient;
        private readonly WorkItemQueryResult _workItemQueryResult;
        private readonly WorkItemsStubData _workItemsStubData;

        public WorkItemTrackingHttpClientMock()
        {
            _workItemTrackHttpClient = new Mock<WorkItemTrackingHttpClient>(null, null);
            _workItemQueryResult = new WorkItemQueryResult();
            _workItemsStubData = new WorkItemsStubData();
            _workItemQueryResult.WorkItems = _workItemsStubData.GetWorkItemReferences();
        }

        /// <summary>
        /// Creates a fake WorkItemTracking http client using stub test data
        /// </summary>
        /// <param name="wiql">A mocked instance of a wiql object.</param>
        /// <returns> The mocked instance of a WorkItemTracking http client.</returns>
        public WorkItemTrackingHttpClient MockWorkItemTrackingHttpClient(Mock<Wiql> wiql)
        {
            var workItems = _workItemsStubData.GetWorkItems();

            _workItemTrackHttpClient.Setup(x => x.QueryByWiqlAsync(wiql.Object, null, 100, null, CancellationToken.None)).ReturnsAsync(_workItemQueryResult);
            _workItemTrackHttpClient.Setup(x => x.GetWorkItemsAsync(
                    new int[] { 9075, 9076, 9077, 9078, 9079 }, null, _workItemQueryResult.AsOf, null, null, null, CancellationToken.None)).ReturnsAsync(workItems);

            return _workItemTrackHttpClient.Object;
        }
    }
}
