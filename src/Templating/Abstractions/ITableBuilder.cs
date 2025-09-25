// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.DockerTools.Templating.Abstractions;

internal interface ITableBuilder
{
    ITableBuilder WithColumnHeadings(params IEnumerable<string> headings);
    void AddRow(params IEnumerable<string> row);
    string ToString();
}
