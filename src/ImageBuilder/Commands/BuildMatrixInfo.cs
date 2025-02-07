// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    public class BuildMatrixInfo
    {
        public string Name { get; set; }
        public List<BuildLegInfo> Legs { get; } = new List<BuildLegInfo>();

        public IEnumerable<BuildLegInfo> OrderedLegs { get => Legs.OrderBy(leg => leg.Name); }
    }
}
