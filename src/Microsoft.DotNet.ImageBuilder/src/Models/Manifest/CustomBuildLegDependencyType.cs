// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

namespace Microsoft.DotNet.ImageBuilder.Models.Manifest
{
    [Description(
        "The type of dependency an image has for a specific scenario."
        )]
    public enum CustomBuildLegDependencyType
    {
        [Description(
            "Indicates the dependency is considered to be integral to the depending image." +
            "This means the dependent image will not have its own dependency graph considered for build leg " +
            "generation. An example of this is when a custom build leg dependency is defined from sdk to " +
            "aspnet; in that case, aspnet and sdk will be included in a leg together but the sdk will not " +
            "have its own leg generated."
            )]
        Integral,

        [Description(
            "Indicates the dependency is considered to be a supplemental companion to the depending image." +
            "This means the dependent image will have its own dependency graph considered for build leg " +
            "generation. An example of this is when a custom build leg dependency is defined to " +
            "include an SDK image supported on a particular architecture in order to test a runtime OS " +
            "that doesn't its own SDK on that architecture (Buster ARM SDK to test Alpine ARM runtime); " +
            "in that case, the SDK will be included in a leg together with the runtime and the SDK will " +
            "still have its own leg."
            )]
        Supplemental
    }
}
