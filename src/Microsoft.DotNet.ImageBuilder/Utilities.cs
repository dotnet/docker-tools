// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class Utilities
    {
        public static string SubstituteVariables(IDictionary<string, string> variables, string expression)
        {
            if (variables != null)
            {
                foreach (KeyValuePair<string, string> kvp in variables)
                {
                    expression = expression.Replace($"$({kvp.Key})", kvp.Value);
                }
            }

            return expression;
        }
    }
}