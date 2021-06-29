// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Polly;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class RetryHelper
    {
        public const int WaitFactor = 5;
        public const int MaxRetries = 5;

        public static Action<DelegateResult<T>, TimeSpan, int, Context> GetOnRetryDelegate<T>(
            int maxRetries, ILoggerService loggerService) =>
            (delegateResult, timeToNextRetry, retryCount, context) =>
                LogRetryMessage(loggerService, timeToNextRetry, retryCount, maxRetries);

        public static Action<Exception, TimeSpan, int, Context> GetOnRetryDelegate(
            int maxRetries, ILoggerService loggerService) =>
            (exception, timeToNextRetry, retryCount, context) =>
            {
                loggerService.WriteError(exception.ToString());
                LogRetryMessage(loggerService, timeToNextRetry, retryCount, maxRetries);
            };

        private static void LogRetryMessage(ILoggerService loggerService, TimeSpan timeToNextRetry, int retryCount, int maxRetries) =>
            loggerService.WriteMessage(
                $"Retry {retryCount}/{maxRetries}, retrying in {timeToNextRetry.TotalSeconds} seconds...");
    }
}
