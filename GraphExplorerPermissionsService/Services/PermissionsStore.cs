// ------------------------------------------------------------------------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.  See License in the project root for license information.
// ------------------------------------------------------------------------------------------------------------------------------------------------------

using FileService.Common;
using FileService.Interfaces;
using GraphExplorerPermissionsService.Interfaces;
using GraphExplorerPermissionsService.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tavis.UriTemplates;

namespace GraphExplorerPermissionsService
{
    public class PermissionsStore : IPermissionsStore
    {
        private UriTemplateTable _urlTemplateTable;
        private IDictionary<int, object> _scopesListTable;
        private IDictionary<string, ScopeInformation> _delegatedScopesInfoTable;
        private IDictionary<string, ScopeInformation> _applicationScopesInfoTable;
        private readonly IMemoryCache _permissionsCache;
        private readonly IFileUtility _fileUtility;
        private readonly string _permissionsContainerName;
        private readonly List<string> _permissionsBlobNames;
        private readonly string _scopesInformation;
        private readonly int _defaultRefreshTimeInHours; // life span of the in-memory cache
        private const string DefaultLocale = "en-US"; // default locale language
        private readonly object _permissionsLock = new object();
        private static bool _permissionsRefreshed = false;

        public PermissionsStore(IFileUtility fileUtility, IConfiguration configuration, IMemoryCache permissionsCache)
        {
            _defaultRefreshTimeInHours = FileServiceHelper.GetFileCacheRefreshTime(configuration["FileCacheRefreshTimeInHours"]);
            _permissionsCache = permissionsCache;
            _fileUtility = fileUtility;
            _permissionsContainerName = configuration["AzureBlobStorage:Containers:Permissions"];
            _permissionsBlobNames = configuration.GetSection("AzureBlobStorage:Blobs:Permissions:Names").Get<List<string>>();
            _scopesInformation = configuration["AzureBlobStorage:Blobs:Permissions:Descriptions"];
        }

        /// <summary>
        /// Populates the template table with the request urls and the scopes table with the permission scopes.
        /// </summary>
        private void SeedPermissionsTables()
        {
            _urlTemplateTable = new UriTemplateTable();
            _scopesListTable = new Dictionary<int, object>();

            HashSet<string> uniqueRequestUrlsTable = new HashSet<string>();
            int count = 0;

            foreach (string permissionFilePath in _permissionsBlobNames)
            {
                string relativePermissionPath = FileServiceHelper.GetLocalizedFilePathSource(_permissionsContainerName, permissionFilePath);
                string jsonString = _fileUtility.ReadFromFile(relativePermissionPath).GetAwaiter().GetResult();

                if (!string.IsNullOrEmpty(jsonString))
                {
                    JObject permissionsObject = JObject.Parse(jsonString);

                    if (permissionsObject.Count < 1)
                    {
                        throw new InvalidOperationException($"The permissions data sources cannot be empty." +
                            $"Check the source file or check whether the file path is properly set. File path: " +
                            $"{relativePermissionPath}");
                    }

                    JToken apiPermissions = permissionsObject.First.First;

                    foreach (JProperty property in apiPermissions)
                    {
                        // Remove any '(...)' from the request url and set to lowercase for uniformity
                        string requestUrl = Regex.Replace(property.Name.ToLower(), @"\(.*?\)", string.Empty);

                        if (uniqueRequestUrlsTable.Add(requestUrl))
                        {
                            count++;

                            // Add the request url
                            _urlTemplateTable.Add(count.ToString(), new UriTemplate(requestUrl));

                            // Add the permission scopes
                            _scopesListTable.Add(count, property.Value);
                        }
                    }

                    _permissionsRefreshed = true;
                }
            }
        }

        /// <summary>
        /// Populates the delegated and application scopes information table caches.
        /// </summary>
        private async Task SeedScopesInfoTables(string locale = DefaultLocale)
        {
            ScopesInformationList scopesInformationList = await _permissionsCache.GetOrCreateAsync($"ScopesInfoList_{locale}", async cacheEntry =>
            {
                _delegatedScopesInfoTable = new Dictionary<string, ScopeInformation>();
                _applicationScopesInfoTable = new Dictionary<string, ScopeInformation>();

                string relativeScopesInfoPath = FileServiceHelper.GetLocalizedFilePathSource(_permissionsContainerName, _scopesInformation, locale);
                string scopesInfoJson = await _fileUtility.ReadFromFile(relativeScopesInfoPath);

                if (string.IsNullOrEmpty(scopesInfoJson))
                {
                    return null;
                }

                scopesInformationList = JsonConvert.DeserializeObject<ScopesInformationList>(scopesInfoJson);

                foreach (ScopeInformation delegatedScopeInfo in scopesInformationList.DelegatedScopesList)
                {
                    _delegatedScopesInfoTable.Add(delegatedScopeInfo.ScopeName, delegatedScopeInfo);
                }

                foreach (ScopeInformation applicationScopeInfo in scopesInformationList.ApplicationScopesList)
                {
                    _applicationScopesInfoTable.Add(applicationScopeInfo.ScopeName, applicationScopeInfo);
                }

                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_defaultRefreshTimeInHours);

                return scopesInformationList;
            });
        }

        /// <summary>
        /// Determines whether the permissions tables need to be refreshed with new data based on the elapsed time
        /// duration since the previous refresh.
        /// </summary>
        /// <returns>true or false based on whether the elapsed time duration is greater or less than the specified
        /// refresh time duration.</returns>
        private bool RefreshPermissionsTables()
        {
            bool refresh = false;
            bool cacheState = _permissionsCache.GetOrCreate("PermissionsTablesState", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_defaultRefreshTimeInHours);
                _permissionsRefreshed = false;
                return refresh = true;
            });

            return refresh;
        }

        /// <summary>
        /// Retrieves permissions scopes.
        /// </summary>
        /// <param name="scopeType">The type of scope to be retrieved for the target request url.</param>
        /// <param name="requestUrl">The target request url whose scopes are to be retrieved.</param>
        /// <param name="method">The target http verb of the request url whose scopes are to be retrieved.</param>
        /// <param name="locale">The language code for the preferred localized file.</param>
        /// <returns>A list of scopes for the target request url given a http verb and type of scope.</returns>
        public async Task<List<ScopeInformation>> GetScopesAsync(string scopeType = "DelegatedWork", string locale = DefaultLocale,
                                                string requestUrl = null, string method = null)
        {
            try
            {
                if (RefreshPermissionsTables())
                {
                    /* Permissions tables are not localized, so no need to keep different localized cached copies.
                       Refresh tables only after the specified time duration has elapsed or no cached copy exists. */
                    lock (_permissionsLock)
                    {
                        // Ensure permissions tables are seeded by only one executing thread,
                        // once per refresh cycle.
                        if (!_permissionsRefreshed)
                        {
                            SeedPermissionsTables();
                        }
                    }
                }

                /* Ensure that the requested localized copy of permissions descriptions
                   is available in the cache. */
                await SeedScopesInfoTables(locale);

                string[] scopes = null;

                if (string.IsNullOrEmpty(requestUrl))  // fetch all permissions
                {
                    var listOfScopes = _scopesListTable.Values.ToArray();
                    List<string> permissionsList = new List<string>();

                    foreach (var scope in listOfScopes)
                    {
                        var result = (JArray)scope;

                        string[] permissions = result.FirstOrDefault()?
                        .SelectToken(scopeType)?
                        .Select(s => (string)s)
                        .ToArray();

                        if (permissions != null)
                        {
                            permissionsList.AddRange(permissions);
                        }

                    }
                    if (permissionsList.Count > 0)
                    {
                        scopes = permissionsList.Distinct().ToArray();
                    }
                }
                else // fetch permissions for a given request url and method
                {
                    if (string.IsNullOrEmpty(method))
                    {
                        throw new ArgumentNullException(nameof(method), "The HTTP method value cannot be null or empty.");
                    }

                    requestUrl = Regex.Replace(requestUrl, @"\?.*", string.Empty); // remove any query params
                    requestUrl = Regex.Replace(requestUrl, @"\(.*?\)", string.Empty); // remove any '(...)' resource modifiers

                    // Check if requestUrl is contained in our Url Template table
                    TemplateMatch resultMatch = _urlTemplateTable.Match(new Uri(requestUrl.ToLower(), UriKind.RelativeOrAbsolute));

                    if (resultMatch == null)
                    {
                        return null;
                    }

                    JArray resultValue = new JArray();
                    resultValue = (JArray)_scopesListTable[int.Parse(resultMatch.Key)];

                    scopes = resultValue.FirstOrDefault(x => x.Value<string>("HttpVerb") == method)?
                        .SelectToken(scopeType)?
                        .Select(s => (string)s)
                        .ToArray();
                }

                if (scopes != null)
                {
                    List<ScopeInformation> scopesList = new List<ScopeInformation>();

                    foreach (string scopeName in scopes)
                    {
                        ScopeInformation scopeInfo = null;
                        if (scopeType.Contains("Delegated"))
                        {
                            if (_delegatedScopesInfoTable.ContainsKey(scopeName))
                            {
                                scopeInfo = _delegatedScopesInfoTable[scopeName];
                            }
                        }
                        else // Application scopes
                        {
                            if (_applicationScopesInfoTable.ContainsKey(scopeName))
                            {
                                scopeInfo = _applicationScopesInfoTable[scopeName];
                            }
                        }

                        if (scopeInfo == null)
                        {
                            scopesList.Add(new ScopeInformation
                            {
                                ScopeName = scopeName
                            });
                        }
                        else
                        {
                            scopesList.Add(scopeInfo);
                        }
                    }

                    return scopesList;
                }

                return null;
            }
            catch (ArgumentNullException exception)
            {
                throw exception;
            }
            catch (ArgumentException)
            {
                return null; // equivalent to no match for the given requestUrl
            }
        }
    }
}
