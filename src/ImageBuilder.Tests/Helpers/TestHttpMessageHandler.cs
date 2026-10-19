#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers;

/// <summary>
/// A custom <see cref="HttpMessageHandler"/> that bypass networks calls and returns back custom responses.
/// </summary>
public class TestHttpMessageHandler(IDictionary<string, HttpResponseMessage> responses) : HttpMessageHandler
{
    private readonly IDictionary<string, HttpResponseMessage> _responses = responses ?? throw new ArgumentNullException(nameof(responses));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(_responses[request.RequestUri.ToString()]);
}
