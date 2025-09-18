// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Cottle;

namespace Microsoft.DotNet.DockerTools.Templating.Cottle;

public static class CottleContextExtensions
{
    extension(IContext context)
    {
        public IContext Add(Dictionary<string, string> variables)
        {
            var variablesDictionary = variables.ToCottleDictionary();
            var newContext = Context.CreateBuiltin(variablesDictionary);
            return Context.CreateCascade(newContext, context);
        }
    }

    extension(IReadOnlyDictionary<string, string> stringDictionary)
    {
        public Dictionary<Value, Value> ToCottleDictionary()
        {
            return stringDictionary.ToDictionary(
                kv => (Value)kv.Key,
                kv => (Value)kv.Value
            );
        }
    }
}
