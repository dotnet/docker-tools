// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Moq;
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
            Mock<ILoggerService> loggerServiceMock = new Mock<ILoggerService>();

            ShowManifestSchemaCommand command = new ShowManifestSchemaCommand(loggerServiceMock.Object);

            await command.ExecuteAsync();

            loggerServiceMock.Verify(o => o.WriteMessage(It.Is<string>(str =>
                JsonConvert.DeserializeObject<JObject>(str) != null
            )));
        }
    }
}
