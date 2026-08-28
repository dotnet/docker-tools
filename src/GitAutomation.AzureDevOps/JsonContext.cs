// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.DotNet.GitAutomation.AzureDevOps;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AzureDevOpsRepository))]
[JsonSerializable(typeof(AzureDevOpsPullRequest))]
[JsonSerializable(typeof(AzureDevOpsCommit))]
[JsonSerializable(typeof(AzureDevOpsCreatePullRequest))]
[JsonSerializable(typeof(AzureDevOpsUpdatePullRequest))]
[JsonSerializable(typeof(ArrayResponse<AzureDevOpsPullRequest>))]
[JsonSerializable(typeof(ArrayResponse<AzureDevOpsCommit>))]
internal sealed partial class JsonContext : JsonSerializerContext;
