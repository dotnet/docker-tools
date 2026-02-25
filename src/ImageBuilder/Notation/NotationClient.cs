// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.DotNet.ImageBuilder.Notation;

/// <inheritdoc/>
public class NotationClient : INotationClient
{
    private const string NotationExecutable = "notation";

    /// <inheritdoc/>
    public string Verify(string imageReference, bool isDryRun) =>
        ExecuteHelper.Execute(
            fileName: NotationExecutable,
            args: $"verify {imageReference}",
            isDryRun: isDryRun);

    /// <inheritdoc/>
    public void ImportTrustPolicy(string policyPath) =>
        ExecuteHelper.Execute(
            fileName: NotationExecutable,
            args: $"policy import {policyPath}",
            isDryRun: false);

    /// <inheritdoc/>
    public void AddCertificate(string storeType, string storeName, string certPath) =>
        ExecuteHelper.Execute(
            fileName: NotationExecutable,
            args: $"cert add --type {storeType} --store {storeName} {certPath}",
            isDryRun: false);

    /// <inheritdoc/>
    public void Login(string server, string username, string password)
    {
        var info = new ProcessStartInfo(NotationExecutable, $"login -u {username} --password-stdin {server}")
        {
            RedirectStandardInput = true
        };

        ExecuteHelper.ExecuteWithRetry(
            info,
            process =>
            {
                process.StandardInput.WriteLine(password);
                process.StandardInput.Close();
            },
            isDryRun: false,
            executeMessageOverride: $"notation login -u {username} --password-stdin {server}");
    }
}
