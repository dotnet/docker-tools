// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.DockerTools.Templating.Abstractions;

public interface ITemplateEngine<TContext>
{
    /// <summary>
    /// Add global variables that will be available in all newly created
    /// template contexts.
    /// </summary>
    void AddGlobalVariables(IDictionary<string, string> variables);

    /// <summary>
    /// Create a new context for rendering a template.
    /// </summary>
    /// <param name="variables">
    /// Dictionary of variables to add to context. These variables will take
    /// precedence over any global variables already set in the engine.
    /// </param>
    /// <param name="templatePath">
    /// The path to the current template is needed to correctly resolve paths
    /// to sub-templates
    /// </param>
    TContext CreateContext(IDictionary<string, string> variables, string templatePath);

    /// <summary>
    /// Read a template from a file and compile it.
    /// </summary>
    ICompiledTemplate<TContext> ReadAndCompile(string path);
}
