// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Moq;
using Moq.Language;
using Moq.Language.Flow;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers
{
    public static class ReturnsExtensions
    {
        public static IReturnsResult<TMock> Returns<TMock, TResult>(this IReturns<TMock, TResult> returns, Func<int, TResult> valueFunction)
            where TMock : class
        {
            int callCount = 0;
            return returns.Returns(() => valueFunction(++callCount));
        }

        public static IReturnsResult<TMock> ReturnsAsync<TMock, TResult>(this IReturns<TMock, Task<TResult>> returns, Func<int, TResult> valueFunction)
            where TMock : class
        {
            int callCount = 0;
            return returns.ReturnsAsync(() => valueFunction(++callCount));
        }
    }
}
