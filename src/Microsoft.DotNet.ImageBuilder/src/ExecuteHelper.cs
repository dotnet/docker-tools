// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class ExecuteHelper
    {
        public static string Execute(
            string fileName,
            string args,
            bool isDryRun,
            string errorMessage = null,
            string executeMessageOverride = null)
        {
            return Execute(new ProcessStartInfo(fileName, args), isDryRun, errorMessage, executeMessageOverride);
        }

        public static string Execute(
            ProcessStartInfo info,
            bool isDryRun,
            string errorMessage = null,
            string executeMessageOverride = null)
        {
            return Execute(info, info => ExecuteProcess(info), isDryRun, errorMessage, executeMessageOverride);
        }

        public static void ExecuteWithRetry(
            string fileName,
            string args,
            bool isDryRun,
            string errorMessage = null,
            string executeMessageOverride = null)
        {
            ExecuteWithRetry(
                new ProcessStartInfo(fileName, args),
                isDryRun: isDryRun,
                errorMessage: errorMessage,
                executeMessageOverride: executeMessageOverride
            );
        }

        public static string ExecuteWithRetry(
            ProcessStartInfo info,
            Action<Process> processStartedCallback = null,
            bool isDryRun = false,
            string errorMessage = null,
            string executeMessageOverride = null)
        {
            return Execute(
                info,
                startInfo => ExecuteWithRetry(startInfo, info => ExecuteProcess(info, processStartedCallback)),
                isDryRun,
                errorMessage,
                executeMessageOverride
            );
        }

        private static string Execute(
            ProcessStartInfo info,
            Func<ProcessStartInfo, ProcessResult> executor,
            bool isDryRun,
            string errorMessage = null,
            string executeMessageOverride = null)
        {
            info.RedirectStandardError = true;

            ProcessResult processResult = null;

            if (executeMessageOverride == null)
            {
                executeMessageOverride = $"{info.FileName} {info.Arguments}";
            }

            Logger.WriteSubheading($"EXECUTING: {executeMessageOverride}");
            if (!isDryRun)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                processResult = executor(info);

                stopwatch.Stop();
                Logger.WriteSubheading($"EXECUTION ELAPSED TIME: {stopwatch.Elapsed}");

                if (processResult.Process.ExitCode != 0)
                {
                    string exceptionMsg = errorMessage ?? $"Failed to execute {info.FileName} {info.Arguments}";
                    exceptionMsg += $"{Environment.NewLine}{Environment.NewLine}{processResult.StandardError}";

                    throw new InvalidOperationException(exceptionMsg);
                }
            }

            return processResult.StandardOutput;
        }

        private static ProcessResult ExecuteProcess(ProcessStartInfo info, Action<Process> processStartedCallback = null)
        {
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;

            Process process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = info
            };

            DataReceivedEventHandler getDataReceivedHandler(StringBuilder stringBuilder, TextWriter outputWriter)
            {
                return new DataReceivedEventHandler((sender, e) =>
                {
                    string line = e.Data;
                    if (line != null)
                    {
                        stringBuilder.AppendLine(line);
                        outputWriter.WriteLine(line);
                    }
                });
            }

            StringBuilder stdOutput = new StringBuilder();
            process.OutputDataReceived += getDataReceivedHandler(stdOutput, Console.Out);

            StringBuilder stdError = new StringBuilder();
            process.ErrorDataReceived += getDataReceivedHandler(stdError, Console.Error);

            process.Start();
            processStartedCallback?.Invoke(process);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            return new ProcessResult(process, stdOutput.ToString().Trim(), stdError.ToString().Trim());
        }

        private static ProcessResult ExecuteWithRetry(ProcessStartInfo info, Func<ProcessStartInfo, ProcessResult> executor)
        {
            const int maxRetries = 5;
            const int waitFactor = 5;

            int retryCount = 0;

            ProcessResult processResult = executor(info);
            while (processResult.Process.ExitCode != 0)
            {
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    break;
                }

                int waitTime = Convert.ToInt32(Math.Pow(waitFactor, retryCount - 1));
                Logger.WriteMessage($"Retry {retryCount}/{maxRetries}, retrying in {waitTime} seconds...");
                Thread.Sleep(waitTime * 1000);
                processResult = executor(info);
            }

            return processResult;
        }

        private class ProcessResult
        {
            public ProcessResult(Process process, string standardOutput, string standardError)
            {
                Process = process;
                StandardOutput = standardOutput;
                StandardError = standardError;
            }

            public Process Process { get; }
            public string StandardOutput { get; }
            public string StandardError { get; }
        }
    }
}
