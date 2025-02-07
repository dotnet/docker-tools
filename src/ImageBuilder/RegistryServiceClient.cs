// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.DotNet.DockerTools.ImageBuilder;

#nullable enable
/// <summary>
/// Client used for querying the REST API of container registries.
/// </summary>
public class RegistryServiceClient : IRegistryContentClient
{
    private const string DockerContentDigestHeader = "Docker-Content-Digest";

    private const string DockerManifestSchema2 = "application/vnd.docker.distribution.manifest.v2+json";
    private const string DockerManifestList = "application/vnd.docker.distribution.manifest.list.v2+json";
    private const string OciManifestSchema1 = "application/vnd.oci.image.manifest.v1+json";
    private const string OciManifestList1 = "application/vnd.oci.image.index.v1+json";
    private static readonly string[] s_manifestMediaTypes =
    [
        DockerManifestSchema2,
        DockerManifestList,
        OciManifestSchema1,
        OciManifestList1
    ];

    private readonly RegistryCredentials? _credentials;
    private readonly string _repo;
    private readonly RegistryHttpClient _httpClient;

    public Uri BaseUri { get; }

    public RegistryServiceClient(string registry, string repo, RegistryHttpClient httpClient, RegistryCredentials? credentials)
    {
        BaseUri = new Uri($"https://{registry}");
        _repo = repo;
        _httpClient = httpClient;
        _credentials = credentials;
    }

    public async Task<ManifestQueryResult> GetManifestAsync(string tagOrDigest)
    {
        HttpResponseMessage response = await SendRequestAsync(
            CreateGetRequestMessage(GetManifestUri(_repo, tagOrDigest), HttpMethod.Get));
        string contentDigest = response.Headers.GetValues(DockerContentDigestHeader).First();

        string content = await response.Content.ReadAsStringAsync();
        return new ManifestQueryResult(
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
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}:{1}",
                    _credentials.Username,
                    _credentials.Password).ToCharArray())));
        }

        HttpResponseMessage response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                $"Response status code does not indicate success: {response.StatusCode}. Reason: '{response.ReasonPhrase}'. Error content:{Environment.NewLine}{errorContent}");
        }
        response.EnsureSuccessStatusCode();

        return response;
    }

    private Uri GetManifestUri(string repositoryName, string tagOrDigest) =>
        new(BaseUri.AbsoluteUri + $"v2/{repositoryName}/manifests/{tagOrDigest}");
}
#nullable disable
