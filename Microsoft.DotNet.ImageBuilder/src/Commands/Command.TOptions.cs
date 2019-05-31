// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.ManifestModel;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class Command<TOptions> : ICommand
        where TOptions : Options, new()
    {
        public TOptions Options { get; private set; }

        Options ICommand.Options => this.Options;

        public Command()
        {
            Options = new TOptions();
        }

        public abstract Task ExecuteAsync();
    }
}
