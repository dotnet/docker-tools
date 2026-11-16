// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.TemplateTool;

internal sealed record Ok<T>(T Value);

internal sealed record Error<TError>(TError Value);

internal readonly union Result<T, TError>(Ok<T>, Error<TError>);

internal static class ResultExtensions
{
    extension<T, TError>(Result<T, TError> result)
    {
        /// <summary>
        /// Continues with <paramref name="f"/> if the result succeeded. Otherwise, passes its error through.
        /// </summary>
        public Result<U, TError> Bind<U>(Func<T, Result<U, TError>> f) => result switch
        {
            Ok<T> ok => f(ok.Value),
            Error<TError> error => error,
        };

        /// <summary>
        /// Transforms the value if the result succeeded. Otherwise, passes its error through.
        /// </summary>
        public Result<U, TError> Map<U>(Func<T, U> f) => result switch
        {
            Ok<T> ok => new Ok<U>(f(ok.Value)),
            Error<TError> error => error,
        };

        /// <summary>
        /// Transforms the error if the result failed. Otherwise, passes its value through.
        /// </summary>
        public Result<T, UError> MapError<UError>(Func<TError, UError> f) => result switch
        {
            Ok<T> ok => ok,
            Error<TError> error => new Error<UError>(f(error.Value)),
        };
    }

    extension<T, TError>(IEnumerable<Result<T, TError>> results)
    {
        /// <summary>
        /// Splits the results into the successful values and the errors, keeping the order of each.
        /// </summary>
        public (List<T> Ok, List<TError> Failed) Partition()
        {
            List<T> ok = [];
            List<TError> failed = [];
            foreach (Result<T, TError> result in results)
            {
                switch (result)
                {
                    case Ok<T> value:
                        ok.Add(value.Value);
                        break;
                    case Error<TError> error:
                        failed.Add(error.Value);
                        break;
                }
            }

            return (ok, failed);
        }

        /// <summary>
        /// Succeeds with every value if all the results succeeded. Otherwise, fails with every error.
        /// </summary>
        public Result<T[], TError[]> Sequence()
        {
            (List<T> ok, List<TError> failed) = results.Partition();
            return failed.Count > 0
                ? new Error<TError[]>([.. failed])
                : new Ok<T[]>([.. ok]);
        }
    }
}
