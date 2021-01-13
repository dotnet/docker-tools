// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public static class CommandExtensions
    {
        public static string GetCommandName(this ICommand command)
        {
            string commandName = command.GetType().Name.TrimEnd("Command");
            return char.ToLowerInvariant(commandName[0]) + commandName.Substring(1);
        }
    }
}
