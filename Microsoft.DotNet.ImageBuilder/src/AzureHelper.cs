// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.ImageBuilder
{
    public class AzureHelper : IDisposable
    {
        private const string AzureCliImage = "microsoft/azure-cli:2.0.54";
        private string _sessionId;

        public static AzureHelper Create(string username, string password, string tenant, bool isDryRun)
        {
            AzureHelper helper = new AzureHelper();

            DockerHelper.PullImage(AzureCliImage, isDryRun);
            helper._sessionId = $"azuresession-{Guid.NewGuid().ToString()}";
            helper.ExecuteAzCommand(
                $"login --service-principal -u {username} -p {password} -t {tenant}",
                isDryRun,
                $"login --service-principal -u {username} -p ********** -t {tenant}");

            return helper;
        }

        public void Dispose()
        {
            ExecuteHelper.Execute("docker", $"volume rm -f {_sessionId}", false);
        }

        public void ExecuteAzCommand(string command, bool isDryRun, string commandMessageOverride = null)
        {
            commandMessageOverride = commandMessageOverride ?? command;

            ExecuteHelper.Execute(
                "docker",
                $"run --rm -v {_sessionId}:/root {AzureCliImage} az {command}",
                isDryRun,
                executeMessageOverride: $"docker run -it --rm -v {_sessionId}:/root {AzureCliImage} az {commandMessageOverride}");
        }
    }
}
