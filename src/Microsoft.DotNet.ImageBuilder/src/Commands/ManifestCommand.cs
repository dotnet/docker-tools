﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class ManifestCommand<TOptions, TSymbolsBuilder> : Command<TOptions, TSymbolsBuilder>, IManifestCommand
        where TOptions : ManifestOptions, new()
        where TSymbolsBuilder : ManifestSymbolsBuilder, new()
    {
        public ManifestInfo Manifest { get; private set; }

        public void LoadManifest()
        {
            if (Manifest is null)
            {
                Manifest = ManifestInfo.Load(Options);
            }
        }

        protected override void Initialize(TOptions options)
        {
            base.Initialize(options);
            LoadManifest();
        }
    }
}
