// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class TaskHelper
    {
        /// <summary>
        /// Acts as an overload of <see cref="Task.WhenAll(IEnumerable{Task})"/> that adds timeout logic.
        /// </summary>
        public static async Task WhenAll(IEnumerable<Task> tasks, TimeSpan timeout)
        {
            Task delay = Task.Delay(timeout);
            Task completedTask = await Task.WhenAny(Task.WhenAll(tasks), delay);
            if (completedTask == delay)
            {
                throw new TimeoutException($"Timed out after waiting '{timeout}'.");
            }
        }
    }
}
