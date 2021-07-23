// ------------------------------------------------------------------------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.  See License in the project root for license information.
// ------------------------------------------------------------------------------------------------------------------------------------------------------

using KnownIssuesService.Interfaces;
using KnownIssuesService.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UtilityService;

namespace KnownIssuesService.Services
{
    public class KnownIssuesService : IKnownIssuesService
    {
		private readonly IConfiguration _configuration;
		private readonly string _accessToken;
		private readonly string _knownIssuesUri;
        private readonly WorkItemTrackingHttpClient _httpQueryClient;
        private readonly Wiql _workItemQuery;

		public KnownIssuesService(IConfiguration configuration)
		{
			_configuration = configuration;
			_accessToken = _configuration["KnownIssues:Token"];
			_knownIssuesUri = _configuration["KnownIssues:Uri"];
			_workItemQuery = QueryBuilder();
			_httpQueryClient = GetWorkItemTrackingHttpClient();
		}

		public KnownIssuesService(WorkItemTrackingHttpClient httpQueryClient, Wiql workItemQuery)
        {
			_httpQueryClient = httpQueryClient;
			_workItemQuery = workItemQuery;
		}

		/// <summary>
		/// Authenticates a process/service to a Visual Studio Service.
		/// </summary>
		/// <param name="personalaccesstoken">access token used for authenticating to a Visual Studio Service</param>
		/// <returns>an instance of the authenticated VS Service</returns>
		public VssBasicCredential Authenticate()
		{
			VssBasicCredential credentials = new VssBasicCredential(string.Empty, _accessToken);
			return credentials;
		}

		/// <summary>
		/// Initializes a WorkItemTracking Http Client instance with the required parameters i.e. credentials
		/// and the Azure DevOps Known Issues Organisation url
		/// </summary>
		/// <returns>an Instance of a WorkItemTrackingHttpClient</returns>
		public WorkItemTrackingHttpClient GetWorkItemTrackingHttpClient()
		{
			var credentials = Authenticate();
			WorkItemTrackingHttpClient workItemTrackingHttpClient = new WorkItemTrackingHttpClient(new Uri(_knownIssuesUri), credentials);

			return workItemTrackingHttpClient;
		}

		/// <summary>
		/// Creates a WorkItemQuery Result used for getting work item ids and their urls from 
		/// Azure DevOps Org
		/// </summary>
		/// <returns>WorkItem Query result</returns>
		public async Task<WorkItemQueryResult> GetQueryByWiqlAsync()
		{
			WorkItemQueryResult result = await _httpQueryClient.QueryByWiqlAsync(_workItemQuery, null, 100, null, CancellationToken.None).ConfigureAwait(false);

			return result;
		}

		/// <summary>
		/// Retrieves WorkItems data from Azure DevOps Org based on the passed in work item ids
		/// </summary>
		/// <param name="ids">Work Item Ids From Azure DevOps Organisation</param>
		/// <param name="result">WorkItemQuery result that contains the specific ids and their urls</param>
		/// <returns>a List of Work Items from Azure DevOps Org</returns>
		public async Task<List<WorkItem>> GetWorkItemsQueryAsync(int[] ids, WorkItemQueryResult result)
		{
			List<WorkItem> items = await _httpQueryClient.GetWorkItemsAsync(ids, null, result.AsOf, null, null, null, CancellationToken.None).ConfigureAwait(false);
			return items;
		}

		/// <summary>
		/// Create a Query builder for retrieving work items from an Azure DevOps Organisation
		/// </summary>
		/// <returns>a work item query builder containing the selection criteria</returns>
		public Wiql QueryBuilder()
		{
			// create a wiql object and build our query
			Wiql wiql = new Wiql()
			{
				Query = "Select [Id] " +
						"From WorkItems " +
						"Where [Work Item Type] = 'Bug' " +
						"And [System.TeamProject] = '" + UtilityConstants.knownIssuesOrganisation + "' " +
						"Order By [State] Asc, [Changed Date] Desc",
			};
			return wiql;
		}

		/// <summary>
		/// Function to Query the List of Known Issues from Azure DevOps Known Organization
		/// </summary>
		/// <returns>Known Issues Contract that contains json items that will be rendered on the browser</returns>
		public async Task<List<KnownIssuesContract>> QueryBugs()
		{
			List<KnownIssuesContract> knownIssuesList = new List<KnownIssuesContract>();
			try
			{
				WorkItemQueryResult result = await GetQueryByWiqlAsync();
				var ids = result.WorkItems.Select(item => item.Id).ToArray();

				if (ids.Length == 0)
				{
					return new List<KnownIssuesContract>();
				}

				// get work items for the ids found in query
				var test = result.AsOf;
				List<WorkItem> items = await GetWorkItemsQueryAsync(ids, result);

				foreach (var item in items)
				{
					KnownIssuesContract contract = new KnownIssuesContract();
					contract.Id = item?.Id;

					if (item.Fields.ContainsKey("System.State"))
					{
						contract.State = item.Fields["System.State"].ToString();
					}

					if (item.Fields.ContainsKey("System.Title"))
					{
						contract.Title = item.Fields["System.Title"].ToString();
					}

					if (item.Fields.ContainsKey("Custom.MSGraphM365Workload"))
					{
						contract.WorkLoadArea = item.Fields["Custom.MSGraphM365Workload"].ToString();
					}

					if (item.Fields.ContainsKey("System.Description"))
					{
						contract.Description = item.Fields["System.Description"].ToString();
					}

					if (item.Fields.ContainsKey("Custom.Workaround"))
					{
						contract.WorkAround = item.Fields["Custom.Workaround"].ToString();
					}

					if (item.Fields.ContainsKey("Custom.APIPathLink"))
					{
						contract.Link = item.Fields["Custom.APIPathLink"].ToString();
					}
					knownIssuesList.Add(contract);
				}

				return knownIssuesList;
			}
			catch (Exception ex)
			{
				throw ex;
			}
		}
	}
}
