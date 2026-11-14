// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.TemplateTool;

/// <summary>
/// The errors that stopped a <see cref="Result{T}"/> from succeeding.
/// </summary>
internal sealed record Failure(IReadOnlyList<string> Errors)
{
    public Failure(string error) : this([error])
    {
    }
}

/// <summary>
/// Either a successful value or a <see cref="Failure"/>.
/// </summary>
internal readonly union Result<T>(T, Failure) where T : notnull;

internal static class ResultExtensions
{
    /// <summary>
    /// Continues with <paramref name="next"/> if <paramref name="result"/> succeeded. Otherwise, passes its
    /// failure through.
    /// </summary>
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> next)
        where TIn : notnull
        where TOut : notnull
        => result switch
        {
            TIn value => next(value),
            Failure failure => failure,
        };

    /// <summary>
    /// Transforms the value if <paramref name="result"/> succeeded. Otherwise, passes its failure through.
    /// </summary>
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map)
        where TIn : notnull
        where TOut : notnull
        => result switch
        {
            TIn value => map(value),
            Failure failure => failure,
        };

    /// <summary>
    /// Transforms each error if <paramref name="result"/> failed. Otherwise, passes its value through.
    /// </summary>
    public static Result<T> MapErrors<T>(this Result<T> result, Func<string, string> map)
        where T : notnull
        => result switch
        {
            T value => value,
            Failure failure => new Failure([.. failure.Errors.Select(map)]),
        };

    /// <summary>
    /// Succeeds with every value if all <paramref name="results"/> succeeded. Otherwise, fails with every
    /// distinct error.
    /// </summary>
    public static Result<T[]> Sequence<T>(this IEnumerable<Result<T>> results)
        where T : notnull
    {
        (List<T> ok, List<Failure> failed) = results.Partition();

        string[] errors =
        [
            .. failed
                .SelectMany(failure => failure.Errors)
                .Distinct(),
        ];

        return errors.Length > 0
            ? new Failure(errors)
            : ok.ToArray();
    }

    /// <summary>
    /// Splits <paramref name="results"/> into the successful values and the failures, keeping the order of each.
    /// </summary>
    public static (List<T> Ok, List<Failure> Failed) Partition<T>(this IEnumerable<Result<T>> results)
        where T : notnull
    {
        List<T> ok = [];
        List<Failure> failed = [];
        foreach (Result<T> result in results)
        {
            switch (result)
            {
                case T value:
                    ok.Add(value);
                    break;
                case Failure failure:
                    failed.Add(failure);
                    break;
            }
        }

        return (ok, failed);
    }
}
