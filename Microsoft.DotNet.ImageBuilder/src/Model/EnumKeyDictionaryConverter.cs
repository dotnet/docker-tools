// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Model
{
    internal class EnumKeyDictionaryConverter<TEnum> : JsonConverter
    {
        public override bool CanWrite { get; } = false;

        public EnumKeyDictionaryConverter() : base()
        {
        }

        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            Type valueType = objectType.GetGenericArguments()[1];
            Type intermediateDictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
            IDictionary intermediateDictionary = (IDictionary)Activator.CreateInstance(intermediateDictionaryType);
            serializer.Populate(reader, intermediateDictionary);

            Type finalDictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(TEnum), valueType);
            IDictionary finalDictionary = (IDictionary)Activator.CreateInstance(finalDictionaryType);
            foreach (DictionaryEntry pair in intermediateDictionary)
            {
                TEnum key = (TEnum)Enum.Parse(typeof(TEnum), pair.Key.ToString(), true);
                finalDictionary.Add(key, pair.Value);
            }

            return finalDictionary;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }
}
