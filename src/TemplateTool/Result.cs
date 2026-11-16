// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.TemplateTool;

internal sealed record Error<E>(E Value);

internal readonly union Result<T, E>(T, Error<E>) where T : notnull;

internal static class ResultExtensions
{
    extension<TIn, E>(Result<TIn, E> result) where TIn : notnull
    {
        /// <summary>
        /// Continues with <paramref name="f"/> if the result succeeded. Otherwise, passes its error through.
        /// </summary>
        public Result<TOut, E> Bind<TOut>(Func<TIn, Result<TOut, E>> f) where TOut : notnull => result switch
        {
            TIn value => f(value),
            Error<E> error => error,
        };
    }

    extension<T, E>(IEnumerable<Result<T, E>> results) where T : notnull
    {
        /// <summary>
        /// Succeeds with every value if all the results succeeded. Otherwise, fails with every error.
        /// </summary>
        public Result<T[], E[]> Sequence()
        {
            List<T> values = [];
            List<E> errors = [];
            foreach (Result<T, E> result in results)
            {
                switch (result)
                {
                    case T value:
                        values.Add(value);
                        break;
                    case Error<E> error:
                        errors.Add(error.Value);
                        break;
                }
            }

            return errors.Count > 0
                ? new Error<E[]>([.. errors])
                : values.ToArray();
        }
    }
}
