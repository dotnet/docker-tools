// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers;

internal static class EnumerableExtensions
{
    public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> enumerable) =>
        new AsyncEnumerable<T>(enumerable);

    private class AsyncEnumerable<T>(IEnumerable<T> enumerable) : IAsyncEnumerable<T>
    {
        private readonly IEnumerable<T> _enumerable = enumerable;

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new AsyncEnumerator<T>(_enumerable.GetEnumerator());

    }

    private class AsyncEnumerator<T>(IEnumerator<T> enumerator) : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _enumerator = enumerator;

        public T Current => _enumerator.Current;

        public ValueTask DisposeAsync()
        {
            _enumerator.Dispose();
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(_enumerator.MoveNext());
    }
}
