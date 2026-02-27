#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class ManifestCommand<TOptions, TOptionsBuilder> : Command<TOptions, TOptionsBuilder>, IManifestCommand
        where TOptions : ManifestOptions, new()
        where TOptionsBuilder : ManifestOptionsBuilder, new()
    {
        private readonly IManifestJsonService _manifestJsonService;

        protected ManifestCommand(IManifestJsonService manifestJsonService)
        {
            _manifestJsonService = manifestJsonService ?? throw new ArgumentNullException(nameof(manifestJsonService));
        }

        public ManifestInfo Manifest { get; private set; }

        public virtual void LoadManifest()
        {
            if (Manifest is null)
            {
                Manifest = _manifestJsonService.Load(Options);
            }
        }

        protected override void Initialize(TOptions options)
        {
            base.Initialize(options);
            LoadManifest();
        }
    }
}
