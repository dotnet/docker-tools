// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Models.MarBulkDeletion;

#nullable enable
internal class BulkDeletionDescription
{
    public string RegistryType { get; set; } = "public";
    public List<string> Digests { get; set;} = [];
}
