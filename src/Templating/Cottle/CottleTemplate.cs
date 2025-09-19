// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Cottle;
using Microsoft.DotNet.DockerTools.Templating.Abstractions;

namespace Microsoft.DotNet.DockerTools.Templating.Cottle;

public sealed class CottleTemplate(IDocument document) : ICompiledTemplate<IContext>
{
    private readonly IDocument _document = document;

    public string Render(IContext context, bool trim = false, string indent = "")
    {
        var content = _document.Render(context);

        if (trim)
        {
            content = content.Trim();
        }

        if (!string.IsNullOrEmpty(indent))
        {
            content = content.Replace("\n", "\n" + indent);
        }

        return content;
    }
}
