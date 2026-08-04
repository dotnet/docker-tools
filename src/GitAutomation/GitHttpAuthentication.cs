// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.DotNet.GitAutomation;

internal static class GitHttpAuthentication
{
    public const string EnvironmentVariable = "GIT_AUTOMATION_AUTHORIZATION";

    public static string[] GetArguments() =>
        [$"--config-env=http.extraHeader={EnvironmentVariable}"];

    public static IReadOnlyDictionary<string, string> GetEnvironmentVariables(
        string authorization) =>
        new Dictionary<string, string>
        {
            [EnvironmentVariable] = $"AUTHORIZATION: {authorization}",
        };

    public static string CreateBasicAuthorization(string username, string password) =>
        $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"))}";
}
