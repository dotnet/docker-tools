// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.DotNet.DockerTools.ImageBuilder.Models.Manifest;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Schema.Generation;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class ShowManifestSchemaCommand : Command<Options, CliOptionsBuilder>
    {
        private readonly ILoggerService _loggerService;

        [ImportingConstructor]
        public ShowManifestSchemaCommand(ILoggerService loggerService)
        {
            _loggerService = loggerService;
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

            _loggerService.WriteMessage(schema.ToString());

            return Task.CompletedTask;
        }
    }
}
