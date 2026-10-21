// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public interface ICommand
{
    Command GetCliCommand();
}
