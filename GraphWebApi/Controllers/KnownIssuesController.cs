// ------------------------------------------------------------------------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.  See License in the project root for license information.
// ------------------------------------------------------------------------------------------------------------------------------------------------------

using KnownIssuesService.Interfaces;
using KnownIssuesService.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GraphWebApi.Controllers
{
    [ApiController]
    public class KnownIssuesController : ControllerBase
    {
        private readonly IKnownIssuesService _knownIssueService;

        public KnownIssuesController(IKnownIssuesService knownIssueService)
        {
            _knownIssueService = knownIssueService;
        }

        // GET: api/<KnownIssuesController>
        [Route("api/[controller]")]
        [Route("knownissues")]
        [HttpGet]
        public async Task<IEnumerable<KnownIssuesContract>> Get()
        {
            var results = await _knownIssueService.QueryBugs();
            return results;
        }
    }
}
