// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Cottle;
using Microsoft.DotNet.DockerTools.Templating.Abstractions;

namespace Microsoft.DotNet.DockerTools.Templating.Cottle;

public sealed class CottleTemplateEngine : ITemplateEngine<IContext>
{
    private static readonly DocumentConfiguration s_config = new()
    {
        BlockBegin = "{{",
        BlockContinue = "^",
        BlockEnd = "}}",
        Escape = '@',
        Trimmer = DocumentConfiguration.TrimNothing
    };

    public ICompiledTemplate<IContext> Compile(string template)
    {
        var documentResult = Document.CreateDefault(template, s_config);
        var document = documentResult.DocumentOrThrow;
        var compiledTemplate = new CottleTemplate(document);
        return compiledTemplate;
    }
}
