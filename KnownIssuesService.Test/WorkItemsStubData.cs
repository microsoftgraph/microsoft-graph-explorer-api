// ------------------------------------------------------------------------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.  See License in the project root for license information.
// ------------------------------------------------------------------------------------------------------------------------------------------------------

using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System.Collections.Generic;

namespace KnownIssuesService.Test
{
    /// <summary>
    /// Contains work item stub data for testing purposes
    /// </summary>
    public class WorkItemsStubData
    {
        public List<WorkItemReference> GetWorkItemReferences()
        {
            return new List<WorkItemReference>()
            {
                new WorkItemReference(){  Id = 9075 , Url = "https://microsoftgraph.visualstudio.com/_apis/wit/workItems/9075"},
                new WorkItemReference(){  Id = 9076 , Url = "https://microsoftgraph.visualstudio.com/_apis/wit/workItems/9076"},
                new WorkItemReference(){  Id = 9077 , Url = "https://microsoftgraph.visualstudio.com/_apis/wit/workItems/9077"},
                new WorkItemReference(){  Id = 9078 , Url = "https://microsoftgraph.visualstudio.com/_apis/wit/workItems/9078"},
                new WorkItemReference(){  Id = 9079 , Url = "https://microsoftgraph.visualstudio.com/_apis/wit/workItems/9079"}
            };
        }

        public List<WorkItem> GetWorkItems()
        {
            return new List<WorkItem>()
            {
                new WorkItem(){  Id = 9075 , Url = "https://microsoftgraph.visualstudio.com/_apis/wit/workItems/9075",
                    Fields = new Dictionary<string, object>() {
                       {"System.State","Active"},
                       {"System.Title","Issue A"},
                       {"Custom.MSGraphM365Workload","Calendar"},
                       {"Custom.Workaround","Test"}
                    }
                },
                new WorkItem(){  Id = 9076 , Url = "https://microsoftgraph.visualstudio.com/_apis/wit/workItems/9076",
                  Fields = new Dictionary<string, object>() {
                       {"System.State","Active"},
                       {"System.Title","Issue B"},
                       {"Custom.MSGraphM365Workload","Notifications"},
                       {"Custom.Workaround","Test"}
                    }

                },
                new WorkItem(){  Id = 9077 , Url = "https://microsoftgraph.visualstudio.com/_apis/wit/workItems/9077",
                  Fields = new Dictionary<string, object>() {
                       {"System.State","Resolved"},
                       {"System.Title","Issue K"},
                       {"Custom.MSGraphM365Workload","Mail"},
                       {"Custom.Workaround","Limit number of requests"}
                    }
                },
                new WorkItem(){  Id = 9078 , Url = "https://microsoftgraph.visualstudio.com/_apis/wit/workItems/9078",
                 Fields = new Dictionary<string, object>() {
                       {"System.State","New"},
                       {"System.Title","Issue F"},
                       {"Custom.MSGraphM365Workload","Mail"},
                       {"Custom.Workaround","Limit number of requests"}
                    }
                }
            };
        }
    }
}
