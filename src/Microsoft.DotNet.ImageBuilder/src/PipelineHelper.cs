﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder
{
    public static class PipelineHelper
    {
        public static string FormatOutputVariable(string variableName, string value) =>
            $"##vso[task.setvariable variable={variableName};isoutput=true]{value}";

        public static string FormatErrorCommand(string message) => $"##[error]{message}";
        public static string FormatWarningCommand(string message) => $"##[warning]{message}";
    }
}
