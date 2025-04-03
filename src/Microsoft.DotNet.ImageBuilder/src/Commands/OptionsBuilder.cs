// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.DotNet.ImageBuilder.Commands;

using System.CommandLine;
using System.Collections.Generic;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

public abstract class OptionsBuilder<TBuilder> where TBuilder : OptionsBuilder<TBuilder>
{
    private readonly List<Option> _options = [];
    private readonly List<Argument> _arguments = [];

    public IEnumerable<Option> GetCliOptions() => _options;

    public IEnumerable<Argument> GetCliArguments() => _arguments;

    protected TBuilder AddSymbol<T>(string alias, string propertyName, bool isRequired, T? defaultValue, string description)
    {
        if (isRequired)
        {
            _arguments.Add(new Argument<T>(propertyName, description));
        }
        else
        {
            _options.Add(
                CreateOption(alias, propertyName, description, defaultValue is null ? default! : defaultValue));
        }

        return (TBuilder)this;
    }
}
