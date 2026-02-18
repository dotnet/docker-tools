// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Schema.Generation;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ShowManifestSchemaCommand : Command<Options, CliOptionsBuilder>
    {
        private readonly ILogger<ShowManifestSchemaCommand> _logger;

        public ShowManifestSchemaCommand(ILogger<ShowManifestSchemaCommand> logger)
        {
            _logger = logger;
        }

        protected override string Description => "Outputs manifest file schema";

        public override Task ExecuteAsync()
        {
            JSchemaGenerator generator = new JSchemaGenerator
            {
                DefaultRequired = Required.DisallowNull
            };
            generator.GenerationProviders.Add(new StringEnumGenerationProvider());

            JSchema schema = generator.Generate(typeof(Manifest));

            _logger.LogInformation(schema.ToString());

            return Task.CompletedTask;
        }
    }
}
