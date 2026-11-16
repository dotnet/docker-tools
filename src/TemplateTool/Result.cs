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

        /// <summary>
        /// Transforms the value if the result succeeded. Otherwise, passes its error through.
        /// </summary>
        public Result<TOut, E> Map<TOut>(Func<TIn, TOut> f) where TOut : notnull => result switch
        {
            TIn value => f(value),
            Error<E> error => error,
        };
    }

    extension<T, EIn>(Result<T, EIn> result) where T : notnull
    {
        /// <summary>
        /// Transforms the error if the result failed. Otherwise, passes its value through.
        /// </summary>
        public Result<T, EOut> MapError<EOut>(Func<EIn, EOut> f) => result switch
        {
            T value => value,
            Error<EIn> error => new Error<EOut>(f(error.Value)),
        };
    }

    extension<T, E>(IEnumerable<Result<T, E>> results) where T : notnull
    {
        /// <summary>
        /// Splits the results into the successful values and the errors, keeping the order of each.
        /// </summary>
        public (List<T> Ok, List<E> Failed) Partition()
        {
            List<T> ok = [];
            List<E> failed = [];
            foreach (Result<T, E> result in results)
            {
                switch (result)
                {
                    case T value:
                        ok.Add(value);
                        break;
                    case Error<E> error:
                        failed.Add(error.Value);
                        break;
                }
            }

            return (ok, failed);
        }

        /// <summary>
        /// Succeeds with every value if all the results succeeded. Otherwise, fails with every error.
        /// </summary>
        public Result<T[], E[]> Sequence()
        {
            (List<T> ok, List<E> failed) = results.Partition();
            return failed.Count > 0
                ? new Error<E[]>([.. failed])
                : ok.ToArray();
        }
    }
}
