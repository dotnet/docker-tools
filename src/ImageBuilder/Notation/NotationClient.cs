// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
}
