// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class WaitForReadmePublishOptions : Options
    {
        protected override string CommandHelp => "Waits for readmes to complete publishing to Docker Hub";

        public string CommitDigest { get; set; }

        public TimeSpan WaitTimeout { get; set; }

        public TimeSpan RequeryDelay { get; set; }

        public ServicePrincipalOptions ServicePrincipalOptions { get; } = new ServicePrincipalOptions();

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            string commitDigest = null;
            syntax.DefineParameter("commit-digest", ref commitDigest, "Git commit digest of the readme changes");
            CommitDigest = commitDigest;

            ServicePrincipalOptions.DefineParameters(syntax);
        }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            TimeSpan waitTimeout = TimeSpan.FromHours(1);
            syntax.DefineOption("timeout", ref waitTimeout, val => TimeSpan.Parse(val),
                $"Maximum time to wait for readme publishing (default: {waitTimeout})");
            WaitTimeout = waitTimeout;

            TimeSpan requeryDelay = TimeSpan.FromSeconds(10);
            syntax.DefineOption("requery-delay", ref requeryDelay, val => TimeSpan.Parse(val),
                $"Amount of time to wait before requerying the status of the commit (default: {requeryDelay})");
            RequeryDelay = requeryDelay;
        }
    }
}
