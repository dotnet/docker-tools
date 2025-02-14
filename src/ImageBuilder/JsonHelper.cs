// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.DotNet.DockerTools.ImageBuilder
{
    public static class JsonHelper
    {
        public static string SerializeObject(object value)
        {
            JsonSerializerSettings settings = new()
            {
                ContractResolver = new CustomContractResolver(),
                Formatting = Formatting.Indented
            };

            return JsonConvert.SerializeObject(value, settings);
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

                Predicate<object> capturedPredicate = property.ShouldSerialize;
                property.ShouldSerialize = val => (capturedPredicate is null || capturedPredicate(val)) && !IsEmptyList(property, val);
                return property;
            }

            private static bool IsEmptyList(JsonProperty property, object targetObj)
            {
                object propVal = property.ValueProvider.GetValue(targetObj);
                return propVal is IList list && list.Count == 0;
            }
        }
    }
}
