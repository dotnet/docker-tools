// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class ExecuteHelper
    {
        public static void Execute(
            string fileName,
            string args,
            bool isDryRun,
            string errorMessage = null,
            string executeMessageOverride = null)
        {
            Execute(new ProcessStartInfo(fileName, args), isDryRun, errorMessage, executeMessageOverride);
        }

        public static Process Execute(
            ProcessStartInfo info,
            bool isDryRun,
            string errorMessage = null,
            string executeMessageOverride = null)
        {
            return Execute(info, ExecuteProcess, isDryRun, errorMessage, executeMessageOverride);
        }

        public static void ExecuteWithRetry(
            string fileName,
            string args,
            bool isDryRun,
            string errorMessage = null,
            string executeMessageOverride = null)
        {
            Execute(
                new ProcessStartInfo(fileName, args),
                info => ExecuteWithRetry(info, ExecuteProcess),
                isDryRun,
                errorMessage,
                executeMessageOverride
            );
        }

        public static Process Execute(
            ProcessStartInfo info,
            Func<ProcessStartInfo, Process> executor,
            bool isDryRun,
            string errorMessage = null,
            string executeMessageOverride = null)
        {
            Process process = null;

            if (executeMessageOverride == null)
            {
                executeMessageOverride = $"{info.FileName} {info.Arguments}";
            }

            Logger.WriteSubheading($"EXECUTING: {executeMessageOverride}");
            if (!isDryRun)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                process = executor(info);

                stopwatch.Stop();
                Logger.WriteSubheading($"EXECUTION ELAPSED TIME: {stopwatch.Elapsed}");

                if (process.ExitCode != 0)
                {
                    string exceptionMsg = errorMessage ?? $"Failed to execute {info.FileName} {info.Arguments}";
                    throw new InvalidOperationException(exceptionMsg);
                }
            }

            return process;
        }

        private static Process ExecuteProcess(ProcessStartInfo info)
        {
            Process process = Process.Start(info);
            process.WaitForExit();
            return process;
        }

        private static Process ExecuteWithRetry(ProcessStartInfo info, Func<ProcessStartInfo, Process> executor)
        {
            const int maxRetries = 5;
            const int waitFactor = 5;

            int retryCount = 0;

            Process process = executor(info);
            while (process.ExitCode != 0)
            {
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    break;
                }

                int waitTime = Convert.ToInt32(Math.Pow(waitFactor, retryCount - 1));
                Logger.WriteMessage($"Retry {retryCount}/{maxRetries}, retrying in {waitTime} seconds...");
                Thread.Sleep(waitTime * 1000);
                process = executor(info);
            }

            return process;
        }
    }
}
