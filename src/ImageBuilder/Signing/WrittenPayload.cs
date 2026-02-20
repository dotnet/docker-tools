// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// A signing request paired with its unsigned payload file written to disk.
/// </summary>
/// <param name="Request">The original signing request.</param>
/// <param name="PayloadFile">The payload file written to disk, to be signed by ESRP.</param>
internal sealed record WrittenPayload(
    ImageSigningRequest Request,
    string PayloadFilePath);
