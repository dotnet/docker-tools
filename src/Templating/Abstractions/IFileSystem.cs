// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.DockerTools.Templating.Abstractions;

public interface IFileSystem
{
    string ReadAllText(string path);
    void WriteAllText(string path, string content);
}
