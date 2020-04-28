// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Polly;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class RetryHelper
    {
        private const int waitFactor = 5;
        public const int MaxRetries = 5;

        public static readonly Func<int, TimeSpan> SleepDurationProvider =
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(waitFactor, retryAttempt - 1));

        public static Action<DelegateResult<T>, TimeSpan, int, Context> GetRetryDelegate<T>(
            int maxRetries, ILoggerService loggerService)
        {
            return (delegateResult, timeSpan, retryCount, context) => loggerService.WriteMessage(
                $"Retry {retryCount}/{maxRetries}, retrying in {timeSpan.TotalSeconds} seconds...");
        }
    }
}
