// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Threading.Tasks;
using Octokit;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class BlobsClientExtensions
    {
        public static async Task<string> GetFileContentAsync(
            this IBlobsClient blobsClient, string repoOwner, string repoName, string fileSha)
        {
            Blob fileBlob = await blobsClient.Get(repoOwner, repoName, fileSha);

            switch (fileBlob.Encoding.Value)
            {
                case EncodingType.Utf8:
                    return fileBlob.Content;
                case EncodingType.Base64:
                    byte[] bytes = Convert.FromBase64String(fileBlob.Content);
                    return Encoding.UTF8.GetString(bytes);
                default:
                    throw new NotSupportedException(
                       $"The blob for file SHA '{fileSha}' in repo '{repoOwner}/{repoName}' uses an unsupported encoding: {fileBlob.Encoding}");
            }
        }
    }
}
