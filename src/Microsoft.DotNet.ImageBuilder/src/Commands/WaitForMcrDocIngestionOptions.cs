// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class WaitForMcrDocIngestionOptions : Options
    {
        protected override string CommandHelp => "Waits for docs to complete ingestion into Docker Hub";

        public string CommitDigest { get; set; }

        public TimeSpan WaitTimeout { get; set; }

        public TimeSpan RequeryDelay { get; set; }

        public ServicePrincipalOptions ServicePrincipal { get; } = new ServicePrincipalOptions();

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            string commitDigest = null;
            syntax.DefineParameter("commit-digest", ref commitDigest, "Git commit digest of the readme changes");
            CommitDigest = commitDigest;

            ServicePrincipal.DefineParameters(syntax);
        }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            TimeSpan waitTimeout = TimeSpan.FromMinutes(5);
            syntax.DefineOption("timeout", ref waitTimeout,
                val => string.IsNullOrEmpty(val) ? waitTimeout : TimeSpan.Parse(val),
                $"Maximum time to wait for doc ingestion (default: {waitTimeout})");
            WaitTimeout = waitTimeout;

            TimeSpan requeryDelay = TimeSpan.FromSeconds(10);
            syntax.DefineOption("requery-delay", ref requeryDelay,
                val => string.IsNullOrEmpty(val) ? requeryDelay : TimeSpan.Parse(val),
                $"Amount of time to wait before requerying the status of the commit (default: {requeryDelay})");
            RequeryDelay = requeryDelay;
        }
    }
}
