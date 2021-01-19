// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishImageInfoOptions : ImageInfoOptions, IGitOptionsHost
    {
        public GitOptions GitOptions { get; set; } = new GitOptions();
        public AzdoOptions AzdoOptions { get; set; } = new AzdoOptions();
    }

    public class PublishImageInfoOptionsBuilder : ImageInfoOptionsBuilder
    {
        private readonly AzdoOptionsBuilder _azdoOptionsBuilder = new AzdoOptionsBuilder();
        private readonly GitOptionsBuilder _gitOptionsBuilder = new GitOptionsBuilder();

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(_gitOptionsBuilder.GetCliOptions())
                .Concat(_azdoOptionsBuilder.GetCliOptions());

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_gitOptionsBuilder.GetCliArguments())
                .Concat(_azdoOptionsBuilder.GetCliArguments());
    }
}
#nullable disable
