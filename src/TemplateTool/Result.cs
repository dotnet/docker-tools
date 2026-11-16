// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.TemplateTool;

internal sealed record Ok<T>(T Value);

internal sealed record Error<E>(E Value);

internal readonly union Result<T, E>(Ok<T>, Error<E>);

internal static class ResultExtensions
{
    extension<T, E>(Result<T, E> result)
    {
        /// <summary>
        /// Continues with <paramref name="f"/> if the result succeeded. Otherwise, passes its error through.
        /// </summary>
        public Result<U, E> Bind<U>(Func<T, Result<U, E>> f) => result switch
        {
            Ok<T> ok => f(ok.Value),
            Error<E> error => error,
        };

        /// <summary>
        /// Transforms the value if the result succeeded. Otherwise, passes its error through.
        /// </summary>
        public Result<U, E> Map<U>(Func<T, U> f) => result switch
        {
            Ok<T> ok => new Ok<U>(f(ok.Value)),
            Error<E> error => error,
        };

        /// <summary>
        /// Transforms the error if the result failed. Otherwise, passes its value through.
        /// </summary>
        public Result<T, F> MapError<F>(Func<E, F> f) => result switch
        {
            Ok<T> ok => ok,
            Error<E> error => new Error<F>(f(error.Value)),
        };
    }

    extension<T, E>(IEnumerable<Result<T, E>> results)
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
                    case Ok<T> value:
                        ok.Add(value.Value);
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
                : new Ok<T[]>([.. ok]);
        }
    }
}
