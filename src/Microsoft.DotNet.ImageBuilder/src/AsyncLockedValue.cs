// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder
{
    public class AsyncLockedValue<T> : IDisposable
    {
        private readonly SemaphoreSlim semaphore;
        private readonly bool disposeSemaphore;
        private T value;

        public AsyncLockedValue(T value = default, SemaphoreSlim semaphore = null)
        {
            this.value = value;

            if (semaphore is null)
            {
                this.semaphore = new SemaphoreSlim(1);
                disposeSemaphore = true;
            }
            else
            {
                this.semaphore = semaphore;
            }
        }

        public void Dispose()
        {
            if (this.disposeSemaphore)
            {
                this.semaphore.Dispose();
            }
        }

        public async Task<T> GetValueAsync(Func<Task<T>> valueInitializer)
        {
            return await this.semaphore.DoubleCheckedLockAsync<T>(
                () => this.value,
                val => val is null,
                async () => this.value = await valueInitializer());
        }

        public async Task<T> ResetValueAsync(Func<Task<T>> valueInitializer = null)
        {
            await this.semaphore.LockAsync(async () =>
            {
                if (valueInitializer is null)
                {
                    this.value = default;
                }
                else
                {
                    this.value = await valueInitializer();
                }
            });

            return this.value;
        }
    }
}
