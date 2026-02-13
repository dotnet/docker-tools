// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Commands;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
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
            Mock<ILogger<ShowManifestSchemaCommand>> loggerServiceMock = new Mock<ILogger<ShowManifestSchemaCommand>>();

            ShowManifestSchemaCommand command = new ShowManifestSchemaCommand(loggerServiceMock.Object);

            await command.ExecuteAsync();

            bool hasSchemaJsonLog = loggerServiceMock.Invocations.Any(invocation =>
            {
                if (invocation.Method.Name != nameof(ILogger.Log))
                {
                    return false;
                }

                string? message = invocation.Arguments[2]?.ToString();
                return message is not null && JsonConvert.DeserializeObject<JObject>(message) != null;
            });

            hasSchemaJsonLog.ShouldBeTrue();
        }
    }
}
