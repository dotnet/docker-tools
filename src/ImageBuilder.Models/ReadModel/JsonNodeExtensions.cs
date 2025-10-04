// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;

namespace Microsoft.DotNet.ImageBuilder.ReadModel;

internal static class JsonNodeExtensions
{
    extension(JsonNode baseNode)
    {
        // Based on https://gist.github.com/cajuncoding/bf78bdcf790782090d231590cbc2438f
        public JsonNode Merge(JsonNode incomingNode)
        {
            switch (baseNode)
            {
                case JsonObject baseObject when incomingNode is JsonObject incomingObject:
                    MergeObjects(baseObject, incomingObject);
                    break;
                case JsonArray baseArray when incomingNode is JsonArray incomingArray:
                    MergeArrays(baseArray, incomingArray);
                    break;
                default:
                    throw new ArgumentException(
                        $"The JsonNode type [{baseNode.GetType().Name}] is incompatible for"
                        + $" merging with the target/base type {incomingNode.GetType().Name}."
                        + " Merging requires the types to be the same."
                    );
            }

            return baseNode;
        }
    }

    private static void MergeObjects(JsonObject baseObject, JsonObject incomingObject)
    {
        // Clear object so that the child elements no longer have a parent
        var incomingObjectSnapshot = incomingObject.ToArray();
        incomingObject.Clear();

        foreach (KeyValuePair<string, JsonNode?> incomingProperty in incomingObjectSnapshot)
        {
            var baseObjectValue = baseObject[incomingProperty.Key];
            baseObject[incomingProperty.Key] = baseObjectValue switch
            {
                // If both are JsonObjects, merge them recursively
                JsonObject baseChildObject when incomingProperty.Value is JsonObject incomingChildObject =>
                    baseChildObject.Merge(incomingChildObject),

                // If both are JsonArrays, merge them recursively
                JsonArray baseChildArray when incomingProperty.Value is JsonArray incomingChildArray =>
                    baseChildArray.Merge(incomingChildArray),

                // If the base property and incoming property are of different
                // types, or if the base property does not exist, overwrite
                // with the incoming property.
                _ => incomingProperty.Value
            };
        }
    }

    private static void MergeArrays(JsonArray baseArray, JsonArray incomingArray)
    {
        // Clear array so that the child elements no longer have a parent
        var incomingArraySnapshot = incomingArray.ToArray();
        incomingArray.Clear();

        foreach (JsonNode? incomingElement in incomingArraySnapshot)
        {
            baseArray.Add(incomingElement);
        }
    }
}
