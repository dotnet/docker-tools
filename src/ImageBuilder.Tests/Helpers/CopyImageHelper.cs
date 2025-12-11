// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Moq;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers;

internal static class CopyImageHelper
{
    // ICopyImageService is now mocked directly using Mock.Of<ICopyImageService>()
    // since we no longer have a factory
}
