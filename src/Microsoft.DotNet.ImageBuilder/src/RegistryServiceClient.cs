// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rest;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
/// <summary>
/// Client used for querying the REST API of container registries.
/// </summary>
internal class RegistryServiceClient : ServiceClient<RegistryServiceClient>
{
    private const string DockerContentDigestHeader = "Docker-Content-Digest";

    private const string DockerManifestSchema2 = "application/vnd.docker.distribution.manifest.v2+json";
    private const string DockerManifestList = "application/vnd.docker.distribution.manifest.list.v2+json";
    private const string OciManifestSchema1 = "application/vnd.oci.image.manifest.v1+json";
    private const string OciManifestList1 = "application/vnd.oci.image.index.v1+json";
    private static readonly string[] s_manifestMediaTypes = new[]
    {
        DockerManifestSchema2,
        DockerManifestList,
        OciManifestSchema1,
        OciManifestList1
    };

    private readonly BasicAuthenticationCredentials? _credentials;

    public Uri BaseUri { get; }

    public RegistryServiceClient(string registry, RegistryHttpClient httpClient, BasicAuthenticationCredentials? credentials)
        : base(httpClient, disposeHttpClient: false)
    {
        BaseUri = new Uri($"https://{registry}");
        credentials?.InitializeServiceClient(this);
        _credentials = credentials;
    }

    public async Task<ManifestResult> GetManifestAsync(string repo, string tagOrDigest)
    {
        HttpResponseMessage response = await SendRequestAsync(
            CreateGetRequestMessage(GetManifestUri(repo, tagOrDigest), HttpMethod.Get));
        string contentDigest = response.Headers.GetValues(DockerContentDigestHeader).First();

        string content = await response.Content.ReadAsStringAsync();
        return new ManifestResult(
            contentDigest,
            (JsonObject)(JsonNode.Parse(content) ?? throw new InvalidOperationException($"Invalid JSON result: {content}")));
    }

    private static HttpRequestMessage CreateGetRequestMessage(Uri requestUri, HttpMethod method)
    {
        HttpRequestMessage request = new(method, requestUri);
        request.Headers.Accept.AddRange(
            s_manifestMediaTypes.Select(mediaType => new MediaTypeWithQualityHeaderValue(mediaType)));
        return request;
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request)
    {
        if (_credentials != null)
        {
            // This allows the credentials instance to update the request's Authorization header
            await _credentials.ProcessHttpRequestAsync(request, CancellationToken.None);
        }

        HttpResponseMessage response = await HttpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            if (response.Content is null)
            {
                throw new InvalidOperationException($"Response content is null.");
            }

            string? requestContent = null;
            if (request.Content is not null)
            {
                requestContent = await request.Content.ReadAsStringAsync();
            }

            string errorContent = await response.Content.ReadAsStringAsync();

            throw new HttpOperationException(
                $"Response status code does not indicate success: {response.StatusCode}. Reason: '{response.ReasonPhrase}'. Error content:{Environment.NewLine}{errorContent}")
            {
                Body = errorContent,
                Request = new HttpRequestMessageWrapper(request, requestContent),
                Response = new HttpResponseMessageWrapper(response, errorContent)
            };
        }
        response.EnsureSuccessStatusCode();

        return response;
    }

    private Uri GetManifestUri(string repositoryName, string tagOrDigest) =>
        new(BaseUri.AbsoluteUri + $"v2/{repositoryName}/manifests/{tagOrDigest}");
}
#nullable disable
