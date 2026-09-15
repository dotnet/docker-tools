// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class TaskHelper
    {
        /// <summary>
        /// Acts as an overload of <see cref="Task.WhenAll(IEnumerable{Task})"/> that adds timeout logic.
        /// </summary>
        public static async Task WhenAll(IEnumerable<Task> tasks, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Task allTasks = Task.WhenAll(tasks);
            Task delay = Task.Delay(timeout, cancellationToken);
            Task completedTask = await Task.WhenAny(allTasks, delay);
            if (completedTask == delay)
            {
                await delay;
                throw new TimeoutException($"Timed out after waiting '{timeout}'.");
            }

            await allTasks;
        }

        /// <summary>
        /// Acts as an overload of <see cref="Task.WhenAll{TResult}(IEnumerable{Task{TResult}})"/> that adds timeout logic.
        /// </summary>
        public static async Task<TResult[]> WhenAll<TResult>(IEnumerable<Task<TResult>> tasks, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Task<TResult[]> allTasks = Task.WhenAll(tasks);
            Task delay = Task.Delay(timeout, cancellationToken);
            Task completedTask = await Task.WhenAny(allTasks, delay);
            if (completedTask == delay)
            {
                await delay;
                throw new TimeoutException($"Timed out after waiting '{timeout}'.");
            }

            return await allTasks;
        }
    }
}
