#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder;

public class AsyncLockedValue<T> : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly bool _disposeSemaphore;
    private T _value;

    public AsyncLockedValue(T value = default, SemaphoreSlim semaphore = null)
    {
        _value = value;

        if (semaphore is null)
        {
            _semaphore = new SemaphoreSlim(1);
            _disposeSemaphore = true;
        }
        else
        {
            _semaphore = semaphore;
        }
    }

    public void Dispose()
    {
        if (_disposeSemaphore)
        {
            _semaphore.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    public async Task<T> GetValueAsync(Func<CancellationToken, Task<T>> valueInitializer, CancellationToken cancellationToken)
    {
        return await _semaphore.DoubleCheckedLockAsync(
            () => _value,
            val => val is null,
            async ct => _value = await valueInitializer(ct),
            cancellationToken);
    }

    public async Task<T> ResetValueAsync(CancellationToken cancellationToken, Func<CancellationToken, Task<T>> valueInitializer = null)
    {
        await _semaphore.LockAsync(
            async ct => _value = valueInitializer is null ? default : await valueInitializer(ct),
            cancellationToken);

        return _value;
    }
}
