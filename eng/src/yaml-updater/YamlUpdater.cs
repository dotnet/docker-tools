// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FilePusher;
using Newtonsoft.Json;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace YamlUpdater
{
    public class YamlUpdater
    {
        public static Task Main(string[] args)
        {
            RootCommand command = new(
                "Updates the node value in a YAML file and creates a pull request for the change");
            foreach (Symbol symbol in Options.GetCliOptions())
            {
                command.Add(symbol);
            }

            command.Handler = CommandHandler.Create<Options>(ExecuteAsync);

            return command.InvokeAsync(args);
        }

        private static async Task ExecuteAsync(Options options)
        {
            // Hookup a TraceListener to capture details from Microsoft.DotNet.VersionTools
            TextWriterTraceListener textWriterTraceListener = new(Console.Out);
            AzDoSafeTraceListenerWrapper safeTraceListener = new(textWriterTraceListener);
            Trace.Listeners.Add(safeTraceListener);

            string configJson = File.ReadAllText(options.ConfigPath);
            FilePusher.Models.Config config = JsonConvert.DeserializeObject<FilePusher.Models.Config>(configJson) 
                ?? throw new InvalidOperationException("Failed to deserialize configuration file.");

            UpdateYamlFile(options, config);

            FilePusher.Options filePusherOptions = new()
            {
                GitAuthToken = options.GitAuthToken,
                GitEmail = options.GitEmail,
                GitUser = options.GitUser
            };

            await FilePusher.FilePusher.PushFilesAsync(filePusherOptions, config);
        }

        private static void UpdateYamlFile(Options options, FilePusher.Models.Config config)
        {
            YamlStream yamlStream = new();
            using (StreamReader streamReader = new(config.SourcePath))
            {
                yamlStream.Load(streamReader);
            }

            if (yamlStream.Documents.Count > 1)
            {
                throw new NotSupportedException("Multi-document YAML files are not supported.");
            }

            YamlDocument doc = yamlStream.Documents[0];
            YamlNode currentNode = doc.RootNode;

            string[] queryParts = options.NodeQueryPath.Split('/');
            for (int i = 0; i < queryParts.Length; i++)
            {
                currentNode = currentNode[queryParts[i]];
            }

            if (currentNode is YamlScalarNode scalarNode)
            {
                scalarNode.Value = options.NewValue;
            }
            else
            {
                throw new NotSupportedException("Last node in the path must be a scalar value.");
            }

            StringBuilder stringBuilder = new();
            using StringWriter writer = new(stringBuilder);
            yamlStream.Save(new CustomEmitter(writer), assignAnchors: false);

            string newContent = stringBuilder.ToString();

            Console.WriteLine(
                $"Writing the following content to file '{config.SourcePath}':{Environment.NewLine}{newContent}");

            File.WriteAllText(config.SourcePath, stringBuilder.ToString());
        }

        private class CustomEmitter : IEmitter
        {
            private readonly Emitter _inner;

            public CustomEmitter(TextWriter textWriter)
            {
                _inner = new Emitter(textWriter);
            }

            public void Emit(ParsingEvent @event)
            {
                if (@event is DocumentEnd)
                {
                    // Prevents the "..." document end characters from being added to the end of the file
                    _inner.Emit(new DocumentEnd(isImplicit: true));
                }
                else
                {
                    _inner.Emit(@event);
                }
            }
        }
    }
}
