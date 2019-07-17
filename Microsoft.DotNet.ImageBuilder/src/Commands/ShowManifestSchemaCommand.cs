﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Schema.Generation;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ShowManifestSchemaCommand : Command<ShowManifestSchemaOptions>
    {
        public override Task ExecuteAsync()
        {
            JSchemaGenerator generator = new JSchemaGenerator();
            generator.DefaultRequired = Required.DisallowNull;
            generator.GenerationProviders.Add(new StringEnumGenerationProvider());

            JSchema schema = generator.Generate(typeof(Manifest));

            Logger.WriteMessage(schema.ToString());

            return Task.CompletedTask;
        }
    }
}
