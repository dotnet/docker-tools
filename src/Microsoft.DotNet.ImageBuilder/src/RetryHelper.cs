// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Polly;

namespace Microsoft.DotNet.ImageBuilder
{
    public delegate TimeSpan SleepDurationProvider(int retryAttempt);

    public static class RetryHelper
    {
        private const int waitFactor = 5;
        public const int MaxRetries = 5;

        private static readonly Random jitterer = new Random();

        public static SleepDurationProvider ExponentialSleepDurationProvider =
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(waitFactor, retryAttempt - 1));

        public static Func<int, TimeSpan> ExponentialSleepDurationProviderFunc =
            ExponentialSleepDurationProvider.ToFunc();

        public static SleepDurationProvider AddJitter(this SleepDurationProvider sleepDurationProvider, TimeSpan maxJitterTime = default)
        {
            TimeSpan jitterOffset = maxJitterTime != default ?
                TimeSpan.FromMilliseconds(jitterer.Next(0, (int)maxJitterTime.TotalMilliseconds)) : TimeSpan.Zero;
            return retryAttempt => sleepDurationProvider(retryAttempt) + jitterOffset;
        }

        public static SleepDurationProvider AddOffset(
            this SleepDurationProvider sleepDurationProvider, TimeSpan timeOffset = default) =>
                retryAttempt => sleepDurationProvider(retryAttempt) + timeOffset;

        public static Func<int, TimeSpan> ToFunc(this SleepDurationProvider sleepDurationProvider) =>
            retryAttempt => sleepDurationProvider(retryAttempt);

        public static Action<DelegateResult<T>, TimeSpan, int, Context> GetOnRetryDelegate<T>(
            int maxRetries, ILoggerService loggerService)
        {
            return (delegateResult, timeSpan, retryCount, context) => loggerService.WriteMessage(
                $"Retry {retryCount}/{maxRetries}, retrying in {timeSpan.TotalSeconds} seconds...");
        }
    }
}
