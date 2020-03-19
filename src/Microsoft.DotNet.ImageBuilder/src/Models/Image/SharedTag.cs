// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.DotNet.ImageBuilder.Models.Image
{
    public class SharedTag : IComparable<SharedTag>
    {
        public string Name { get; set; }

        public DateTime Created { get; set; }

        public int CompareTo([AllowNull] SharedTag other)
        {
            if (other is null)
            {
                return 1;
            }

            return Name.CompareTo(other.Name);
        }
    }
}
