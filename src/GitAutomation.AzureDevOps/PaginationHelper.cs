// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Microsoft.DotNet.GitAutomation.AzureDevOps;

internal static class PaginationHelper
{
    private const string ContinuationTokenHeader = "x-ms-continuationtoken";

    public static async IAsyncEnumerable<T> GetAllPages<T>(
        this HttpClient httpClient,
        Func<string?, string> getPageUrl,
        JsonTypeInfo<ArrayResponse<T>> responseType,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? continuationToken = null;

        do
        {
            string nextPageUri = getPageUrl(continuationToken);
            using HttpResponseMessage response = await httpClient.GetAsync(nextPageUri, cancellationToken);

            response.EnsureSuccessStatusCode();

            Stream responseContent = await response.Content.ReadAsStreamAsync(cancellationToken);

            ArrayResponse<T> page = await JsonSerializer.DeserializeAsync(
                responseContent,
                responseType,
                cancellationToken)
                    ?? throw new InvalidOperationException("Azure DevOps returned null for a paged response.");

            foreach (T value in page.Value)
                yield return value;

            continuationToken = response.Headers.TryGetValues(ContinuationTokenHeader, out IEnumerable<string>? values)
                ? values.FirstOrDefault()
                : null;
        }
        while (continuationToken is not null);
    }
}
