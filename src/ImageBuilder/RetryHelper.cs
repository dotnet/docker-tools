// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class RetryHelper
    {
        public const int WaitFactor = 5;
        public const int MaxRetries = 5;

        public static AsyncRetryPolicy GetWaitAndRetryPolicy<TException>(ILogger logger, int medianFirstRetryDelaySeconds = WaitFactor)
            where TException : Exception =>
            Policy
                .Handle<TException>()
                .WaitAndRetryAsync(
                    Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(medianFirstRetryDelaySeconds), MaxRetries),
                    GetOnRetryDelegate(MaxRetries, logger));

        public static Action<DelegateResult<T>, TimeSpan, int, Context> GetOnRetryDelegate<T>(
            int maxRetries, ILogger logger) =>
            (delegateResult, timeToNextRetry, retryCount, context) =>
                LogRetryMessage(logger, timeToNextRetry, retryCount, maxRetries);

        public static Action<Exception, TimeSpan, int, Context> GetOnRetryDelegate(
            int maxRetries, ILogger logger) =>
            (exception, timeToNextRetry, retryCount, context) =>
            {
                logger.LogError(exception.ToString());
                LogRetryMessage(logger, timeToNextRetry, retryCount, maxRetries);
            };

        private static void LogRetryMessage(ILogger logger, TimeSpan timeToNextRetry, int retryCount, int maxRetries) =>
            logger.LogInformation(
                $"Retry {retryCount}/{maxRetries}, retrying in {timeToNextRetry.TotalSeconds} seconds...");
    }
}
