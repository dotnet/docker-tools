#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class LockHelper
    {
        public static T DoubleCheckedLock<T>(object lockObj, ref T value, Func<T, bool> checkLockCondition, Func<T> getValue)
        {
            if (checkLockCondition(value))
            {
                lock (lockObj)
                {
                    if (checkLockCondition(value))
                    {
                        value = getValue();
                    }
                }
            }

            return value;
        }

        public static TValue DoubleCheckedLockLookup<TKey, TValue>(
            object lockObj, IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> getValue, Func<TValue, bool> addToDictionary = null)
        {
            if (!dictionary.TryGetValue(key, out TValue value))
            {
                lock (lockObj)
                {
                    if (!dictionary.TryGetValue(key, out value))
                    {
                        value = getValue();
                        if (addToDictionary is null || addToDictionary(value))
                        {
                            dictionary.Add(key, value);
                        }
                    }
                }
            }

            return value;
        }

        public static async Task<T> DoubleCheckedLockAsync<T>(this SemaphoreSlim semaphore, Func<T> getValue, Func<T, bool> checkLockCondition, Func<CancellationToken, Task> lockedAction, CancellationToken cancellationToken)
        {
            T value = getValue();
            if (checkLockCondition(value))
            {
                await semaphore.LockAsync(async ct =>
                {
                    value = getValue();
                    if (checkLockCondition(value))
                    {
                        await lockedAction(ct);
                        value = getValue();
                    }
                }, cancellationToken);
            }

            return value;
        }

        public static async Task<TValue> DoubleCheckedLockLookupAsync<TKey, TValue>(this SemaphoreSlim semaphore, IDictionary<TKey, TValue> dictionary, TKey key, Func<CancellationToken, Task<TValue>> getValue, CancellationToken cancellationToken, Func<TValue, bool> addToDictionary = null)
        {
            if (!dictionary.TryGetValue(key, out TValue value))
            {
                await semaphore.LockAsync(async ct =>
                {
                    if (!dictionary.TryGetValue(key, out value))
                    {
                        value = await getValue(ct);

                        if (addToDictionary is null || addToDictionary(value))
                        {
                            dictionary.Add(key, value);
                        }
                    }
                }, cancellationToken);
            }

            return value;
        }

        public static async Task LockAsync(this SemaphoreSlim semaphore, Func<CancellationToken, Task> func, CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                await func(cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
