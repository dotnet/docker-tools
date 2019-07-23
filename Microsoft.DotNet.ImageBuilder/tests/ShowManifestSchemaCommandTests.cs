// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class ShowManifestSchemaCommandTests
    {
        /// <summary>
        /// Simple verification that the output of the command can be deserialized as JSON.
        /// </summary>
        [Fact]
        public async Task ShowManifestSchemaCommand_Execute()
        {
            ShowManifestSchemaCommand command = new ShowManifestSchemaCommand();

            string output;
            using (StringWriter stringWriter = new StringWriter())
            {
                Console.SetOut(stringWriter);
                await command.ExecuteAsync();

                output = stringWriter.ToString();
            }

            JObject schema = JsonConvert.DeserializeObject<JObject>(output);
            Assert.NotNull(schema);
        }
    }
}
