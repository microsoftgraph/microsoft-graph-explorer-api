// ------------------------------------------------------------------------------------------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.  See License in the project root for license information.
// ------------------------------------------------------------------------------------------------------------------------------------------------------

using Microsoft.OpenApi.Models;
using System.Collections.Concurrent;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace OpenAPIService.Test
{
    /// <summary>
    /// Defines a Mock class that creates an OpenAPI document from a CSDL file.
    /// </summary>
    public static class OpenAPIDocumentCreatorMock
    {
        private static readonly ConcurrentDictionary<string, OpenApiDocument> _OpenApiDocuments = new ConcurrentDictionary<string, OpenApiDocument>();

        /// <summary>
        /// Gets an OpenAPI document version of Microsoft Graph based on CSDL document
        /// from a dictionary cache or gets a new instance.
        /// </summary>
        /// <param name="graphDocPath">The file path of the Microsoft Graph metadata doc.</param>
        /// <param name="forceRefresh">Don't read from in-memory cache.</param>
        /// <returns>Instance of an OpenApiDocument</returns>
        public static OpenApiDocument GetGraphOpenApiDocument(string graphDocPath, string referenceDocPath = null)
        {
            OpenApiDocument source = CreateOpenApiDocument(graphDocPath, referenceDocPath);
            _OpenApiDocuments[graphDocPath] = source;
            return source;
        }

        /// <summary>
        /// Creates an OpenAPI document from a CSDL document.
        /// </summary>
        /// <param name="graphDocPath">The file path of the CSDL document location.</param>
        /// <param name="styleOptions">Optional parameter that defines the style
        /// options to be used in formatting the OpenAPI document.</param>
        /// <returns>Instance of an OpenApiDocument</returns>
        private static OpenApiDocument CreateOpenApiDocument(string graphDocPath, string referenceDocPath = null)
        {
            if (string.IsNullOrEmpty(graphDocPath))
            {
                return null;
            }

            using StreamReader streamReader = new StreamReader(graphDocPath);
            Stream csdl = streamReader.BaseStream;

            XmlReader xmlReader = null;
            if (!string.IsNullOrEmpty(referenceDocPath))
            {
                // Mock out referencing a model XML document targeted by another model XML document
                using StreamReader refStreamReader = new StreamReader(referenceDocPath);
                var refCsdl = refStreamReader.ReadToEndAsync().GetAwaiter().GetResult();
                xmlReader = XElement.Parse(refCsdl).CreateReader();
            }

            OpenApiDocument document = OpenApiService.ConvertCsdlToOpenApi(csdl, xmlReader);

            return document;
        }
    }
}
