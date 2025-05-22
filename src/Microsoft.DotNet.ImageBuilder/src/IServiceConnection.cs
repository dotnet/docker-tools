// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.ImageBuilder.Commands;

public interface IServiceConnection
{
    string TenantId { get; init; }
    string ClientId { get; init; }
    string ServiceConnectionId { get; init; }
}
