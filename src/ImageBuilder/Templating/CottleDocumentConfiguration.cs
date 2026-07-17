// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Cottle;

namespace Microsoft.DotNet.ImageBuilder.Templating;

/// <summary>
/// Creates the shared Cottle document configuration used by ImageBuilder template rendering.
/// </summary>
internal static class CottleDocumentConfiguration
{
    /// <summary>
    /// Creates a Cottle configuration that uses ImageBuilder's template delimiters and preserves
    /// whitespace exactly.
    /// </summary>
    public static DocumentConfiguration Create() =>
        new()
        {
            BlockBegin = "{{",
            BlockContinue = "^",
            BlockEnd = "}}",
            Escape = '@',
            Trimmer = DocumentConfiguration.TrimNothing
        };
}
