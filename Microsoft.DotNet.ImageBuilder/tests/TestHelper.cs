// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public static class TestHelper
    {
        public static TempFolderContext UseTempFolder()
        {
            return new TempFolderContext();
        }
    }

    public class TempFolderContext : IDisposable
    {
        public TempFolderContext()
        {
            do
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    Guid.NewGuid().ToString());
            }
            while (Directory.Exists(Path));

            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, true);
        }
    }
}
