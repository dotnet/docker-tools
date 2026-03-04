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

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Client used for querying the REST API of container registries.
/// </summary>
public class RegistryApiClient : IRegistryManifestClient
{
    private const string DockerContentDigestHeader = "Docker-Content-Digest";

    private const string DockerManifestSchema2 = "application/vnd.docker.distribution.manifest.v2+json";
    private const string DockerManifestList = "application/vnd.docker.distribution.manifest.list.v2+json";
    private const string OciManifestSchema1 = "application/vnd.oci.image.manifest.v1+json";
    private const string OciManifestList1 = "application/vnd.oci.image.index.v1+json";

    /// <summary>
    /// Media types accepted when querying for container image manifests.
    /// </summary>
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

    /// <summary>
    /// Gets the base URI of the registry (e.g., <c>https://myregistry.azurecr.io</c>).
    /// </summary>
    public Uri BaseUri { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="RegistryApiClient"/>.
    /// </summary>
    /// <param name="registry">The hostname of the container registry.</param>
    /// <param name="repo">The repository name within the registry.</param>
    /// <param name="httpClient">HTTP client configured for registry OAuth handshaking.</param>
    /// <param name="credentials">Optional basic-auth credentials for the registry.</param>
    public RegistryApiClient(string registry, string repo, RegistryHttpClient httpClient, RegistryCredentials? credentials)
    {
        BaseUri = new Uri($"https://{registry}");
        _repo = repo;
        _httpClient = httpClient;
        _credentials = credentials;
    }

    /// <inheritdoc/>
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

    /// <summary>
    /// Creates an HTTP GET request message with the accepted manifest media types.
    /// </summary>
    private static HttpRequestMessage CreateGetRequestMessage(Uri requestUri, HttpMethod method)
    {
        HttpRequestMessage request = new(method, requestUri);
        request.Headers.Accept.AddRange(
            s_manifestMediaTypes.Select(mediaType => new MediaTypeWithQualityHeaderValue(mediaType)));
        return request;
    }

    /// <summary>
    /// Sends an HTTP request to the registry, attaching basic-auth credentials if available.
    /// Throws <see cref="HttpRequestException"/> with the response status code on failure.
    /// </summary>
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
                $"Response status code does not indicate success: {response.StatusCode}. Reason: '{response.ReasonPhrase}'. Error content:{Environment.NewLine}{errorContent}",
                inner: null,
                response.StatusCode);
        }
        response.EnsureSuccessStatusCode();

        return response;
    }

    /// <summary>
    /// Builds the manifest API URI for the given repository and tag or digest reference.
    /// </summary>
    private Uri GetManifestUri(string repositoryName, string tagOrDigest) =>
        new(BaseUri.AbsoluteUri + $"v2/{repositoryName}/manifests/{tagOrDigest}");
}
