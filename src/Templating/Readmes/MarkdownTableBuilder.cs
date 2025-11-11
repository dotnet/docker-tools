// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.DotNet.DockerTools.Templating.Abstractions;
using Microsoft.DotNet.DockerTools.Templating.Shared;
using Microsoft.DotNet.ImageBuilder.ReadModel;

namespace Microsoft.DotNet.DockerTools.Templating.Readmes;

internal sealed class MarkdownTableBuilder : ITableBuilder
{
    private readonly List<string> _headings = [];
    private readonly List<IEnumerable<string>> _rows = [];

    public ITableBuilder WithColumnHeadings(params IEnumerable<string> headings)
    {
        _headings.Clear();
        _headings.AddRange(headings);
        return this;
    }

    public void AddRow(params IEnumerable<string> row) => _rows.Add(row);

    public override string ToString()
    {
        var table = new StringBuilder();

        if (_headings.Count > 0)
        {
            AppendRow(table, _headings);
            table.AppendLine();

            AppendRow(table, _headings.Select(_ => "---"));
            table.AppendLine();
        }

        foreach (var row in _rows.WithIndex())
        {
            AppendRow(table, row.Item);

            // Put lines between rows but not after the last row
            if (row.Index != _rows.Count - 1) table.AppendLine();
        }

        return table.ToString();
    }

    private static StringBuilder AppendRow(StringBuilder stringBuilder, IEnumerable<string> cells) =>
        stringBuilder.Append("| ").AppendJoin(" | ", cells).Append(" |");
}
