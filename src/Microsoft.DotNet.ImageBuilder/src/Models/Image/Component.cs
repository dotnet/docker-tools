// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Models.Image
{
    public class Component : IComparable<Component>
    {
        public string Name {  get; }
        public string Version { get; }
        public string Type { get; }

        public Component(string type, string name, string version)
        {
            Type = type;
            Name = name;
            Version = version;
        }

        public int CompareTo([AllowNull] Component other)
        {
            if (other is null)
            {
                return 1;
            }

            return ToString().CompareTo(other.ToString());
        }

        public override string ToString() => $"{Type}:{Name}={Version}";
    }
}
#nullable disable
