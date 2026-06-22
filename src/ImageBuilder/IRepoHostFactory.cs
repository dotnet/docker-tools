// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.DotNet.Automation;
using Microsoft.DotNet.ImageBuilder.Commands;

namespace Microsoft.DotNet.ImageBuilder;

public interface IRepoHostFactory
{
    /// <summary>
    /// Creates an <see cref="IRepoHost"/> for the GitHub repo described by
    /// <paramref name="gitOptions"/>, resolving GitHub App credentials to a
    /// token if necessary.
    /// </summary>
    Task<IRepoHost> CreateRepoHostAsync(GitOptions gitOptions, bool isDryRun);
}
