// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Models.Annotations
{
    public class AcrEventEntry
    {
        // TODO: at the moment we only use 'TimeGenerated' and 'Digest'
        // We should likely delete the other properties.

        public DateTime TimeGenerated { get; set; }
        public string OperationName { get; set; }
        public string Repository { get; set; }
        public string Tag { get; set; }
        public string Digest { get; set; }

        public AcrEventEntry()
        {
        }
    }
}
