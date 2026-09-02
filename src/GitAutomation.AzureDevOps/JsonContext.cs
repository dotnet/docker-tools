// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.DotNet.GitAutomation.AzureDevOps;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Repository))]
[JsonSerializable(typeof(PullRequestSearchResult))]
[JsonSerializable(typeof(PullRequest))]
[JsonSerializable(typeof(Commit))]
[JsonSerializable(typeof(CreatePullRequest))]
[JsonSerializable(typeof(UpdatePullRequest))]
[JsonSerializable(typeof(ArrayResponse<PullRequestSearchResult>))]
[JsonSerializable(typeof(ArrayResponse<PullRequest>))]
[JsonSerializable(typeof(ArrayResponse<Commit>))]
internal sealed partial class JsonContext : JsonSerializerContext;
