// ------------------------------------------------------------------------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.  See License in the project root for license information.
// ------------------------------------------------------------------------------------------------------------------------------------------------------

using KnownIssuesService.Interfaces;
using KnownIssuesService.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace KnownIssuesService.Test
{
    public class KnownIssuesServiceShould
    {
        private readonly IKnownIssuesService _knownIssuesService;
        private readonly IConfigurationRoot _configuration;
        private readonly WorkItemTrackingHttpClientMock _workItemTrackingHttpClientMock;
        private readonly WorkItemTrackingHttpClient _workItemTrackingHttpClient;
        private readonly Mock<Wiql> _wiqlTest;

        public KnownIssuesServiceShould()
        {
            _wiqlTest = new Mock<Wiql>();
            _configuration ??= new ConfigurationBuilder()
                                    .AddJsonFile("appsettingstest.json")
                                    .Build();
            _workItemTrackingHttpClientMock = new WorkItemTrackingHttpClientMock();
            _workItemTrackingHttpClient = _workItemTrackingHttpClientMock.MockWorkItemTrackingHttpClient(_wiqlTest);
            _knownIssuesService = new Services.KnownIssuesService(_workItemTrackingHttpClient, _wiqlTest.Object);
        }

        [Fact]
        public async Task GetQueryByWiqlAsync_Test()
        {
            //Arrange
            int expectedNoOfWorkItems = 5;

            //Act
            WorkItemQueryResult workItemQueryResult = await _knownIssuesService.GetQueryByWiqlAsync();
            int actualNoOfWorkItems = workItemQueryResult.WorkItems.ToList().Count;

            //Assert
            Assert.Equal(expectedNoOfWorkItems, actualNoOfWorkItems);
        }

        [Fact]
        public async Task GetWorkItemsQueryAsync_Test()
        {
            //Arrange
            int expectedNoOfWorkItems = 4;

            //Act
            WorkItemQueryResult workItemQueryResult = await _knownIssuesService.GetQueryByWiqlAsync();
            var ids = workItemQueryResult.WorkItems.Select(item => item.Id).ToArray();
            List<WorkItem> items = await _knownIssuesService.GetWorkItemsQueryAsync(ids, workItemQueryResult);

            //Assert
            Assert.Equal(expectedNoOfWorkItems, items.Count);
        }

        [Fact]
        public async Task QueryBugs_Test()
        {
            //Arrange
            int expectedNoOfWorkItems = 4;
            KnownIssuesContract contract = new KnownIssuesContract()
            {
                Title = "Issue B",
                State = "Active",
                WorkLoadArea = "Notifications"
            };
            //Act
            List<KnownIssuesContract> items = await _knownIssuesService.QueryBugs();

            //Assert
            Assert.Equal(expectedNoOfWorkItems, items.Count);
            Assert.Equal(contract.Title, items[1].Title);
            Assert.Equal(contract.State, items[1].State);
            Assert.Equal(contract.WorkLoadArea, items[1].WorkLoadArea);
        }
    }
}
