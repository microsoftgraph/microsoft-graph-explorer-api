﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace GraphExplorerSamplesService
{
    public class DiskFileUtility : IFileUtility
    {
        /// <summary>
        /// Reads the contents of a provided file on disk.
        /// </summary>
        /// <param name="filePathSource">The directory path name of the file on disk.</param>
        /// <returns>The contents of the file.</returns>
        public async Task<string> ReadFromFile(string filePathSource)
        {            
            using (StreamReader streamReader = new StreamReader(filePathSource))
            {
                return await streamReader.ReadToEndAsync();
            }            
        }
    }
}
