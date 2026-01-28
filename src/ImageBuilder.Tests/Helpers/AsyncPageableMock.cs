#nullable disable
﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Azure;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers;

public class AsyncPageableMock<T>(IEnumerable<T> items) : AsyncPageable<T>
{
    private readonly IEnumerable<T> _items = items;

    public override IAsyncEnumerable<Page<T>> AsPages(string continuationToken = null, int? pageSizeHint = null) =>
        new PageMock<T>[] { new(_items) }.ToAsyncEnumerable();

    private class PageMock<TItem>(IEnumerable<TItem> items) : Page<TItem>
    {
        private readonly IEnumerable<TItem> _items = items;

        public override IReadOnlyList<TItem> Values => _items.ToList();

        public override string ContinuationToken => throw new NotImplementedException();

        public override Response GetRawResponse() => throw new NotImplementedException();
    }
}
