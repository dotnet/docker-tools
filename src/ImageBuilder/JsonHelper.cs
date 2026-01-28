// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class JsonHelper
    {
        public static JsonSerializerSettings JsonSerializerSettings => new()
        {
            ContractResolver = new CustomContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        public static string SerializeObject(object? value)
        {
            return JsonConvert.SerializeObject(value, JsonSerializerSettings);
        }

        private class CustomContractResolver : DefaultContractResolver
        {
            public CustomContractResolver()
            {
                NamingStrategy = new CamelCaseNamingStrategy();
            }

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);

                // Required properties should always be serialized (even if default/empty)
                // Let JSON.NET's Required validation handle null checking
                if (property.Required == Required.Always)
                {
                    property.NullValueHandling = NullValueHandling.Include;
                    property.DefaultValueHandling = DefaultValueHandling.Include;
                }
                else
                {
                    // Skip empty lists for non-required properties
                    Predicate<object>? originalShouldSerialize = property.ShouldSerialize;
                    property.ShouldSerialize = targetObj =>
                    {
                        if (originalShouldSerialize is not null && !originalShouldSerialize(targetObj))
                        {
                            return false;
                        }

                        return !IsEmptyList(property, targetObj);
                    };
                }

                return property;
            }

            private static bool IsEmptyList(JsonProperty property, object targetObj)
            {
                var propertyValue = property.ValueProvider?.GetValue(targetObj);
                return propertyValue is IList list && list.Count == 0;
            }
        }
    }
}
