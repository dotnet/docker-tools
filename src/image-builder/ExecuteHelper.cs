using System;
using System.Diagnostics;
using System.Threading;

namespace ImageBuilder
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
                (info) => ExecuteWithRetry(info, ExecuteProcess),
                isDryRun,
                errorMessage, 
                executeMessageOverride
            );
        }

        private static Process Execute(
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

            Console.WriteLine($"-- EXECUTING: {executeMessageOverride}");
            if (!isDryRun)
            {
                process = executor(info);
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
                Console.WriteLine($"Retry {retryCount}/{maxRetries}, retrying in {waitTime} seconds...");
                Thread.Sleep(waitTime * 1000);
                process = executor(info);
            }

            return process;
        }
    }
}
