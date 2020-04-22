// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class IngestKustoImageInfoOptions : ImageInfoOptions, IFilterableOptions
    {
        protected override string CommandHelp => "Ingests image info data into Kusto";

        public ManifestFilterOptions FilterOptions { get; } = new ManifestFilterOptions();

        public string ClientID { get; set; }
        public string Cluster { get; set; }
        public string Database { get; set; }
        public string Secret { get; set; }
        public string Table { get; set; }
        public string Tenant { get; set; }

        public override void DefineOptions(ArgumentSyntax syntax)
        {
            base.DefineOptions(syntax);

            FilterOptions.DefineOptions(syntax);
        }

        public override void DefineParameters(ArgumentSyntax syntax)
        {
            base.DefineParameters(syntax);

            string cluster = null;
            syntax.DefineParameter("cluster", ref cluster, "The cluster to ingest the data to");
            Cluster = cluster;

            string database = null;
            syntax.DefineParameter("database", ref database, "The database to ingest the data to");
            Database = database;

            string table = null;
            syntax.DefineParameter("table", ref table, "The table to ingest the data to");
            Table = table;

            string clientID = null;
            syntax.DefineParameter("client-id", ref clientID, "The URL or name associated with the service principal");
            ClientID = clientID;

            string secret = null;
            syntax.DefineParameter("secret", ref secret, "The service principal password");
            Secret = secret;

            string tenant = null;
            syntax.DefineParameter("tenant", ref tenant, "The tenant associated with the service principal");
            Tenant = tenant;
        }
    }
}
