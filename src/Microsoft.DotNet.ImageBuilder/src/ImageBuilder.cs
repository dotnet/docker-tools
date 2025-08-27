// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Reflection;
using ICommand = Microsoft.DotNet.ImageBuilder.Commands.ICommand;

namespace Microsoft.DotNet.ImageBuilder;

public static class ImageBuilder
{
    public static IEnumerable<ICommand> Commands => Container.Value.GetExportedValues<ICommand>();

    private static Lazy<CompositionContainer> Container { get; } = new(() =>
        {
            string dllLocation = Assembly.GetExecutingAssembly().Location;
            DirectoryCatalog catalog = new(Path.GetDirectoryName(dllLocation), Path.GetFileName(dllLocation));
            return new CompositionContainer(catalog, CompositionOptions.DisableSilentRejection);
        }
    );
}
