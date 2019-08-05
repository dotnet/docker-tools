// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class MatrixInfo
    {
        public string Name { get; set; }
        public List<LegInfo> Legs { get; } = new List<LegInfo>();

        public IEnumerable<LegInfo> OrderedLegs { get => Legs.OrderBy(leg => leg.Name); }
    }
}
