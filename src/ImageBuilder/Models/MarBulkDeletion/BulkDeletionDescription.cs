// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Models.MarBulkDeletion;

internal record BulkDeletionDescription
{
    public string RegistryType { get; init; } = "public";
    public List<string> Digests { get; init;} = [];
}
