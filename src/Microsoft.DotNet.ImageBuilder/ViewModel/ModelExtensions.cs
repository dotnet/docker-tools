// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public static class ModelExtensions
    {
        public static string SubstituteTagVariables(this Manifest manifest, string tag)
        {
            if (manifest.TagVariables != null)
            {
                foreach (KeyValuePair<string, string> kvp in manifest.TagVariables)
                {
                    tag = tag.Replace($"$({kvp.Key})", kvp.Value);
                }
            }

            return tag;
        }
    }
}
