// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public abstract class Command<TOptions> : ICommand where TOptions : Options, new()
{
    public TOptions Options { get; private set; }

    protected abstract string Description { get; }

    public Command()
    {
        Options = new TOptions();
    }

    public Command GetCliCommand()
    {
        TOptions options = new();

        return CommandAction.Create(
            name: this.GetCommandName(),
            description: Description,
            options: options,
            run: cancellationToken =>
            {
                Initialize(options);
                return ExecuteAsync(cancellationToken);
            });
    }

    protected virtual void Initialize(TOptions options)
    {
        Options = options;
    }

    public abstract Task ExecuteAsync(CancellationToken cancellationToken);
}
