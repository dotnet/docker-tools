// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Formats logging messages for Azure pipelines.
/// </summary>
/// <remarks>
/// See https://learn.microsoft.com/azure/devops/pipelines/scripts/logging-commands
/// </remarks>
public static class PipelineHelper
{
    public static string FormatOutputVariable(string variableName, string value) =>
        $"##vso[task.setvariable variable={variableName};isoutput=true]{value}";

    public static string FormatErrorCommand(string message) => $"##[error]{message}";

    public static string FormatWarningCommand(string message) => $"##[warning]{message}";

    public static string SetResult(PipelineResult result) => result switch
    {
        PipelineResult.Succeeded => SetResult("Succeeded"),
        PipelineResult.SucceededWithIssues => SetResult("SucceededWithIssues"),
        PipelineResult.Failed => SetResult("Failed"),
        _ => ""
    };

    private static string SetResult(string resultText) => $"##vso[task.complete result={resultText}]";
}

/// <summary>
/// Represents the result of a pipeline operation.
/// </summary>
/// <remarks>
/// See https://learn.microsoft.com/azure/devops/pipelines/scripts/logging-commands#properties-2
/// </remarks>
public enum PipelineResult
{
    Succeeded,
    SucceededWithIssues,
    Failed
}
