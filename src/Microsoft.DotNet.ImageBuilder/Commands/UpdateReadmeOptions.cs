// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class UpdateReadmeOptions : Options
    {
        protected override string CommandHelp => "Updates the readme on the Docker Registries";
        protected override string CommandName => "updateReadme";

        public UpdateReadmeOptions() : base()
        {
        }
    }
}
