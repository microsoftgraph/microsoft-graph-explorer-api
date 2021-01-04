// ------------------------------------------------------------------------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.  See License in the project root for license information.
// ------------------------------------------------------------------------------------------------------------------------------------------------------

using Microsoft.OData.Edm.Csdl;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.OpenApi.Services;
using Microsoft.OpenApi.Writers;
using Microsoft.OpenApi.OData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Text;
using OpenAPIService.Common;
using UriMatchingService;
using System.Xml;

namespace OpenAPIService
{
    public enum OpenApiStyle
    {
        PowerShell,
        PowerPlatform,
        Plain,
        GEAutocomplete
    }

    public class OpenApiService
    {
        private static readonly ConcurrentDictionary<Uri, OpenApiDocument> _OpenApiDocuments = new ConcurrentDictionary<Uri, OpenApiDocument>();
        private static UriTemplateMatcher _uriTemplateTable = new UriTemplateMatcher();
        private static IDictionary<int, OpenApiOperation[]> _openApiOperationsTable = new Dictionary<int, OpenApiOperation[]>();

        /// <summary>
        /// Create partial document based on provided predicate
        /// </summary>
        public static OpenApiDocument CreateFilteredDocument(OpenApiDocument source, string title, string graphVersion, Func<OpenApiOperation, bool> predicate)
        {

            var subset = new OpenApiDocument
            {
                Info = new OpenApiInfo()
                {
                    Title = title,
                    Version = graphVersion
                },

                Components = new OpenApiComponents()
            };
            var aadv2Scheme = new OpenApiSecurityScheme()
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows()
                {
                    AuthorizationCode = new OpenApiOAuthFlow()
                    {
                        AuthorizationUrl = new Uri(Constants.GraphConstants.GraphAuthorizationUrl),
                        TokenUrl = new Uri(Constants.GraphConstants.GraphTokenUrl)
                    }
                },
                Reference = new OpenApiReference() { Id = "azureaadv2", Type = ReferenceType.SecurityScheme },
                UnresolvedReference = false
            };
            subset.Components.SecuritySchemes.Add("azureaadv2", aadv2Scheme);

            subset.SecurityRequirements.Add(new OpenApiSecurityRequirement() { { aadv2Scheme, new string[] { } } });

            subset.Servers.Add(new OpenApiServer() { Description = "Core", Url = string.Format(Constants.GraphConstants.GraphUrl, graphVersion) });

            var results = FindOperations(source, predicate);
            foreach (var result in results)
            {
                OpenApiPathItem pathItem;
                string pathKey = FormatPathFunctions(result.CurrentKeys.Path, result.Operation.Parameters);

                if (subset.Paths == null)
                {
                    subset.Paths = new OpenApiPaths();
                    pathItem = new OpenApiPathItem();
                    subset.Paths.Add(pathKey, pathItem);
                }
                else
                {
                    if (!subset.Paths.TryGetValue(pathKey, out pathItem))
                    {
                        pathItem = new OpenApiPathItem();
                        subset.Paths.Add(pathKey, pathItem);
                    }
                }

                pathItem.Operations.Add((OperationType)result.CurrentKeys.Operation, result.Operation);
            }

            if (subset.Paths == null)
            {
                throw new ArgumentException("No paths found for the supplied parameters.");
            }

            CopyReferences(subset);

            return subset;
        }

        /// <summary>
        /// Create predicate function based on passed query parameters
        /// </summary>
        /// <param name="operationIds">Comma delimited list of operationIds or * for all operations.</param>
        /// <param name="tags">Comma delimited list of tags or a single regex.</param>
        /// <param name="url">Url path to match with Operation Ids.</param>
        /// <param name="graphVersion">Version of Microsoft Graph.</param>
        /// <param name="forceRefresh">Don't read from in-memory cache.</param>
        /// <returns>A predicate</returns>
        public static async Task<Func<OpenApiOperation, bool>> CreatePredicate(string operationIds, string tags, string url,
            OpenApiDocument source, bool forceRefresh = false)
        {
            if (url != null && (operationIds != null || tags != null))
            {
                throw new InvalidOperationException("Cannot filter by url and either operationIds and tags at the same time.");
            }
            else if (operationIds != null && tags != null)
            {
                throw new InvalidOperationException("Cannot filter by operationIds and tags at the same time.");
            }

            Func<OpenApiOperation, bool> predicate;
            if (operationIds != null)
            {
                if (operationIds == "*")
                {
                    predicate = (o) => true;  // All operations
                }
                else
                {
                    var operationIdsArray = operationIds.Split(',');
                    predicate = (o) => operationIdsArray.Contains(o.OperationId);
                }
            }
            else if (tags != null)
            {
                var tagsArray = tags.Split(',');
                if (tagsArray.Length == 1)
                {
                    var regex = new Regex(tagsArray[0]);

                    predicate = (o) => o.Tags.Any(t => regex.IsMatch(t.Name));
                }
                else
                {
                    predicate = (o) => o.Tags.Any(t => tagsArray.Contains(t.Name));
                }
            }
            else if (url != null)
            {
                /* Extract the respective Operation Id(s) that match the provided url path */

                if (!_openApiOperationsTable.Any() || forceRefresh)
                {
                    _uriTemplateTable = new UriTemplateMatcher();
                    _openApiOperationsTable = new Dictionary<int, OpenApiOperation[]>();

                    await PopulateReferenceTablesAync(source);
                }

                url = url.Replace('-', '_');

                TemplateMatch resultMatch = _uriTemplateTable.Match(new Uri(url.ToLower(), UriKind.RelativeOrAbsolute));

                if (resultMatch == null)
                {
                    throw new ArgumentException("The url supplied could not be found.");
                }

                /* Fetch the corresponding Operations Id(s) for the matched url */

                OpenApiOperation[] openApiOps = _openApiOperationsTable[int.Parse(resultMatch.Key)];
                string[] operationIdsArray = openApiOps.Select(x => x.OperationId).ToArray();

                predicate = (o) => operationIdsArray.Contains(o.OperationId);
            }
            else
            {
                throw new InvalidOperationException("Either operationIds, tags or url need to be specified.");
            }

            return predicate;
        }

        /// <summary>
        /// Populates the _uriTemplateTable with the Graph url paths and the _openApiOperationsTable
        /// with the respective OpenApiOperations for these urls paths.
        /// </summary>
        /// <param name="source">The OpenAPI document.</param>
        /// <returns>A task.</returns>
        private static async Task PopulateReferenceTablesAync(OpenApiDocument source)
        {
            HashSet<string> uniqueUrlsTable = new HashSet<string>(); // to ensure unique url path entries in the UriTemplate table

            int count = 0;

            foreach (var path in source.Paths)
            {
                if (uniqueUrlsTable.Add(path.Key))
                {
                    count++;

                    string urlPath = path.Key.Replace('-', '_');
                    _uriTemplateTable.Add(count.ToString(), urlPath.ToLower());

                    OpenApiOperation[] operations = path.Value.Operations.Values.ToArray();
                    _openApiOperationsTable.Add(count, operations);
                }
            }
        }

        /// <summary>
        /// Create a representation of the OpenApiDocument to return from an API
        /// </summary>
        /// <param name="subset">OpenAPI document.</param>
        /// <param name="styleOptions">The modal object containing the required styling options.</param>
        /// <returns>A memory stream.</returns>
        public static MemoryStream SerializeOpenApiDocument(OpenApiDocument subset, OpenApiStyleOptions styleOptions)
        {
            var stream = new MemoryStream();
            var sr = new StreamWriter(stream);
            OpenApiWriterBase writer;

            if (styleOptions.OpenApiFormat == Constants.OpenApiConstants.Format_Yaml)
            {
                if (styleOptions.InlineLocalReferences)
                {
                    writer = new OpenApiYamlWriter(sr,
                        new OpenApiWriterSettings { ReferenceInline = ReferenceInlineSetting.InlineLocalReferences });
                }
                else
                {
                    writer = new OpenApiYamlWriter(sr);
                }
            }
            else // json
            {
                if (styleOptions.InlineLocalReferences)
                {
                    writer = new OpenApiJsonWriter(sr,
                        new OpenApiWriterSettings { ReferenceInline = ReferenceInlineSetting.InlineLocalReferences });
                }
                else
                {
                    writer = new OpenApiJsonWriter(sr);
                }
            }

            if (styleOptions.OpenApiVersion == Constants.OpenApiConstants.OpenApiVersion_2)
            {
                subset.SerializeAsV2(writer);
            }
            else
            {
                subset.SerializeAsV3(writer);
            }
            sr.Flush();
            stream.Position = 0;
            return stream;
        }

        /// <summary>
        /// Gets an OpenAPI document version of Microsoft Graph based on CSDL document
        /// from a dictionary cache or gets a new instance.
        /// </summary>
        /// <param name="graphUri">The uri of the Microsoft Graph metadata doc.</param>
        /// <param name="forceRefresh">Don't read from in-memory cache.</param>
        /// <returns>A task of the value of an OpenAPI document.</returns>
        public static async Task<OpenApiDocument> GetGraphOpenApiDocumentAsync(string graphUri, bool forceRefresh)
        {
            var csdlHref = new Uri(graphUri);
            if (!forceRefresh && _OpenApiDocuments.TryGetValue(csdlHref, out OpenApiDocument doc))
            {
                return doc;
            }

            OpenApiDocument source = await CreateOpenApiDocumentAsync(csdlHref);
            _OpenApiDocuments[csdlHref] = source;
            return source;
        }

        /// <summary>
        /// Update the OpenAPI document based on the style option
        /// </summary>
        /// <param name="style">The OpenApiStyle value.</param>
        /// <param name="subsetOpenApiDocument">The subset of an OpenAPI document.</param>
        /// <returns>An OpenAPI doc with the respective style applied.</returns>
        public static OpenApiDocument ApplyStyle(OpenApiStyle style, OpenApiDocument subsetOpenApiDocument)
        {
            if (style == OpenApiStyle.GEAutocomplete)
            {
                // Clone doc before making changes
                subsetOpenApiDocument = Clone(subsetOpenApiDocument);

                // The Content property and its schema $refs are unnecessary for autocomplete
                RemoveContent(subsetOpenApiDocument);
            }
            else if (style == OpenApiStyle.PowerShell || style == OpenApiStyle.PowerPlatform)
            {
                /* For Powershell and PowerPlatform Styles */

                // Clone doc before making changes
                subsetOpenApiDocument = Clone(subsetOpenApiDocument);

                // Remove AnyOf
                var anyOfRemover = new AnyOfRemover();
                var walker = new OpenApiWalker(anyOfRemover);
                walker.Walk(subsetOpenApiDocument);

                if (style == OpenApiStyle.PowerShell)
                {
                    // Format the OperationId for Powershell cmdlet names generation
                    var powershellFormatter = new PowershellFormatter();
                    walker = new OpenApiWalker(powershellFormatter);
                    walker.Walk(subsetOpenApiDocument);

                    var version = subsetOpenApiDocument.Info.Version;
                    if (!new Regex("v\\d\\.\\d").Match(version).Success)
                    {
                        subsetOpenApiDocument.Info.Version = "v1.0-" + version;
                    }

                    // Remove the root path to make AutoREST happy
                    subsetOpenApiDocument.Paths.Remove("/");
                }
            }

            if (subsetOpenApiDocument.Paths == null ||
                !subsetOpenApiDocument.Paths.Any())
            {
                throw new ArgumentException ("No paths found for the supplied parameters.");
            }

            return subsetOpenApiDocument;
        }

        private static OpenApiDocument Clone(OpenApiDocument subsetOpenApiDocument)
        {
            var stream = new MemoryStream();
            var writer = new OpenApiYamlWriter(new StreamWriter(stream));
            subsetOpenApiDocument.SerializeAsV3(writer);
            writer.Flush();
            stream.Position = 0;
            var reader = new OpenApiStreamReader();
            return reader.Read(stream, out _);
        }

        private static async Task<OpenApiDocument> CreateOpenApiDocumentAsync(Uri csdlHref)
        {
            var httpClient = CreateHttpClient();

            Stream csdl = await httpClient.GetStreamAsync(csdlHref.OriginalString);

            OpenApiDocument document = ConvertCsdlToOpenApi(csdl);

            return document;
        }

        /// <summary>
        /// Converts CSDL to OpenAPI
        /// </summary>
        /// <param name="csdl">The CSDL stream.</param>
        /// <returns>An OpenAPI document.</returns>
        public static OpenApiDocument ConvertCsdlToOpenApi(Stream csdl, XmlReader xmlReader = null)
        {
            // Function to load referenced model xml
            XmlReader getReferencedModelReaderFunc(Uri uri)
            {
                if (xmlReader != null)
                {
                    return xmlReader;
                }
                else
                {
                    if (uri == null)
                    {
                        return null;
                    }
                    else
                    {
                        var httpClient = new HttpClient();
                        var referenceCsdl = httpClient.GetStringAsync(uri.OriginalString).GetAwaiter().GetResult();
                        return XElement.Parse(referenceCsdl).CreateReader();
                    }
                }
            }

            var edmModel = CsdlReader.Parse(XElement.Load(csdl).CreateReader(), getReferencedModelReaderFunc);

            var settings = new OpenApiConvertSettings()
            {
                EnableKeyAsSegment = true,
                EnableOperationId = true,
                PrefixEntityTypeNameBeforeKey = true,
                TagDepth = 2,
                EnablePagination = true,
                EnableDiscriminatorValue = false,
                EnableDerivedTypesReferencesForRequestBody = false,
                EnableDerivedTypesReferencesForResponses = false,
                ShowRootPath = true,
                ShowLinks = true
            };
            OpenApiDocument document = edmModel.ConvertToOpenApi(settings);

            document = FixReferences(document);
            return document;
        }

        private static OpenApiDocument FixReferences(OpenApiDocument document)
        {
            // This method is only needed because the output of ConvertToOpenApi isn't quite a valid OpenApiDocument instance.
            // So we write it out, and read it back in again to fix it up.

            var sb = new StringBuilder();
            document.SerializeAsV3(new OpenApiYamlWriter(new StringWriter(sb)));
            var doc = new OpenApiStringReader().Read(sb.ToString(), out _);

            return doc;
        }

        private static IList<SearchResult> FindOperations(OpenApiDocument graphOpenApi, Func<OpenApiOperation, bool> predicate)
        {
            var search = new OperationSearch(predicate);
            var walker = new OpenApiWalker(search);
            walker.Walk(graphOpenApi);
            return search.SearchResults;
        }

        private static HttpClient CreateHttpClient()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var httpClient = new HttpClient(new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip
            });
            httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("graphslice", "1.0"));
            return httpClient;
        }

        private static void CopyReferences(OpenApiDocument target)
        {
            bool morestuff;
            do
            {
                var copy = new CopyReferences(target);
                var walker = new OpenApiWalker(copy);
                walker.Walk(target);

                morestuff = AddReferences(copy.Components, target.Components);

            } while (morestuff);
        }

        private static bool AddReferences(OpenApiComponents newComponents, OpenApiComponents target)
        {
            var moreStuff = false;
            foreach (var item in newComponents.Schemas)
            {
                if (!target.Schemas.ContainsKey(item.Key))
                {
                    moreStuff = true;
                    target.Schemas.Add(item);
                }
            }

            foreach (var item in newComponents.Parameters)
            {
                if (!target.Parameters.ContainsKey(item.Key))
                {
                    moreStuff = true;
                    target.Parameters.Add(item);
                }
            }

            foreach (var item in newComponents.Responses)
            {
                if (!target.Responses.ContainsKey(item.Key))
                {
                    moreStuff = true;
                    target.Responses.Add(item);
                }
            }
            return moreStuff;
        }

        private static void RemoveContent(OpenApiDocument target)
        {
            ContentRemover contentRemover = new ContentRemover();
            OpenApiWalker walker = new OpenApiWalker(contentRemover);
            walker.Walk(target);
        }

        /// <summary>
        /// Formats path functions, where present, by surrounding placeholder values of string data types
        /// with single quotation marks.
        /// </summary>
        /// <param name="pathKey">The path key in which the function placeholder(s) need to be formatted
        /// with single quotation marks.</param>
        /// <returns>The path key with its function placeholder(s) of string data types, where applicable,
        /// formatted with single quotation marks.</returns>
        private static string FormatPathFunctions(string pathKey, IList<OpenApiParameter> parameters)
        {
            var parameterTypes = new Dictionary<string, string>();
            foreach (var parameter in parameters)
            {
                /* The Type and Format properties describe the data type of the function parameters.
                 * For string data types the Format property is usually undefined.
                 */
                if (string.IsNullOrEmpty(parameter.Schema.Format) &&
                    !string.IsNullOrEmpty(parameter.Schema.Type))
                {
                    parameterTypes.Add(parameter.Name, parameter.Schema.Type);
                }
            }

            /* Example:
             * Actual ---->  /reports/microsoft.graph.getTeamsUserActivityCounts(period={period})
             * Expected -->  /reports/microsoft.graph.getTeamsUserActivityCounts(period='{period}')
             */
            string pattern = @"(=\{.*?\})";
            string evaluator(Match match)
            {
                string output = match.ToString(); // e.g. ---> ={period}
                string paramName = $"{output.Substring(2, output.Length - 3)}"; // e.g. ---> period

                if (parameterTypes.TryGetValue(paramName, out string type))
                {
                    if (type.Equals("string", StringComparison.OrdinalIgnoreCase))
                    {
                        // Only format function parameters with string data types
                        output = $"='{{{paramName}}}'";
                        return output;
                    }
                }
                return output;
            }
            return Regex.Replace(pathKey, pattern, evaluator);
        }
    }
}
