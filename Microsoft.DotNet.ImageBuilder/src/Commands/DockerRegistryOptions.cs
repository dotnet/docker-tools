// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class DockerRegistryOptions : Options
    {
        public string Password { get; set; }
        public string Server { get; set; }
        public string Username { get; set; }

        protected DockerRegistryOptions()
        {
        }

        public override void ParseCommandLine(ArgumentSyntax syntax)
        {
            base.ParseCommandLine(syntax);

            string password = null;
            Argument<string> passwordArg = syntax.DefineOption(
                "password",
                ref password,
                "Password for the Docker Registry the images are pushed to");
            Password = password;

            string server = null;
            syntax.DefineOption(
                "server",
                ref server,
                "Docker Registry server the images are pushed to (default is Docker Hub)");
            Server = server;

            string username = null;
            Argument<string> usernameArg = syntax.DefineOption(
                "username",
                ref username,
                "Username for the Docker Registry the images are pushed to");
            Username = username;

            if (password != null ^ username != null)
            {
                Logger.WriteError($"error: `{usernameArg.Name}` and `{passwordArg.Name}` must both be specified.");
                Environment.Exit(1);
            }
        }
    }
}
