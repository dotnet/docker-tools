// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class UpdateReadmeCommand : Command<UpdateReadmeOptions>
    {
        public UpdateReadmeCommand() : base()
        {
        }

        public override async Task ExecuteAsync()
        {
            Utilities.WriteHeading("UPDATING READMES");
            foreach (RepoInfo repo in Manifest.Repos)
            {
                // Docker Hub/Cloud API is not documented thus it is subject to change.  This is the only option
                // until a supported API exists.
                HttpRequestMessage request = new HttpRequestMessage(
                    new HttpMethod("PATCH"),
                    new Uri($"https://cloud.docker.com/v2/repositories/{repo.Name}/"));

                string credentials = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes($"{Options.Username}:{Options.Password}"));
                request.Headers.Add("Authorization", $"Basic {credentials}");

                JObject jsonContent = new JObject(new JProperty("full_description", new JValue(repo.GetReadmeContent())));
                request.Content = new StringContent(jsonContent.ToString(), Encoding.UTF8, "application/json");

                if (!Options.IsDryRun)
                {
                    HttpResponseMessage response = await new HttpClient().SendAsync(request);
                    Console.WriteLine($"-- RESPONSE:{Environment.NewLine}{response}");
                    response.EnsureSuccessStatusCode();
                }
            }
        }
    }
}
