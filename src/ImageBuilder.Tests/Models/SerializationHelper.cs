// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models;

/// <summary>
/// Helper methods for testing JSON serialization and deserialization of manifest models.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
public static class SerializationHelper
{
    private static readonly JsonSerializerSettings s_serializerSettings = new()
    {
        ContractResolver = JsonHelper.JsonSerializerSettings.ContractResolver,
        Formatting = Formatting.Indented,
        NullValueHandling = JsonHelper.JsonSerializerSettings.NullValueHandling,
        DefaultValueHandling = JsonHelper.JsonSerializerSettings.DefaultValueHandling,
        Converters = { new StringEnumConverter() }
    };

    /// <summary>
    /// Asserts that both serialization and deserialization produce the expected results.
    /// Verifies that the object serializes to the expected JSON string, and that the JSON
    /// string deserializes back to an equivalent object.
    /// Not exactly the same as a round-trip test.
    /// </summary>
    /// <typeparam name="T">The type of object to test.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="expectedJson">The expected JSON string.</param>
    /// <param name="assertEqual">A function that asserts equality between expected and actual objects.</param>
    public static void AssertBidirectional<T>(T obj, string expectedJson, Action<T, T> assertEqual)
    {
        AssertSerialization(obj, expectedJson);
        AssertDeserialization(expectedJson, obj, assertEqual);
    }

    /// <summary>
    /// Asserts that serializing the given object produces the expected JSON string.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="expectedJson">The expected JSON string.</param>
    public static void AssertSerialization<T>(T obj, string expectedJson)
    {
        var actualJson = Serialize(obj);
        var normalizedActualJson = NormalizeJson(actualJson);
        var normalizedExpectedJson = NormalizeJson(expectedJson);
        normalizedActualJson.ShouldBeEquivalentTo(normalizedExpectedJson);
    }

    /// <summary>
    /// Asserts that deserializing the given JSON produces an object equal to the expected object.
    /// Uses the provided equality comparer function.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="expected">The expected object.</param>
    /// <param name="assertEqual">A function that asserts equality between expected and actual objects.</param>
    public static void AssertDeserialization<T>(string json, T expected, Action<T, T> assertEqual)
    {
        T actual = Deserialize<T>(json);
        assertEqual(expected, actual);
    }

    /// <summary>
    /// Asserts that an object can be round-tripped through serialization and deserialization.
    /// </summary>
    /// <typeparam name="T">The type of object to test.</typeparam>
    /// <param name="obj">The object to test.</param>
    /// <param name="assertEqual">A function that asserts equality between expected and actual objects.</param>
    public static void AssertRoundTrip<T>(T obj, Action<T, T> assertEqual)
    {
        string json = Serialize(obj);
        T deserialized = Deserialize<T>(json);
        assertEqual(obj, deserialized);
    }

    /// <summary>
    /// Asserts that deserializing the given JSON throws a <see cref="JsonSerializationException"/>
    /// because a required property is missing or null.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize to.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="propertyName">The name of the required property (use nameof()).</param>
    public static void AssertDeserializationFails<T>(string json, string propertyName)
    {
        JsonSerializationException exception = Assert.Throws<JsonSerializationException>(
            () => JsonConvert.DeserializeObject<T>(json));

        // Newtonsoft.Json error messages use the format: "Required property 'PropertyName' ..."
        exception.Message.ShouldContain($"Required property '{propertyName}'");
    }

    /// <summary>
    /// Asserts that serializing the given object throws a <see cref="JsonSerializationException"/>
    /// because a required property is null.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="propertyName">The name of the required property (use nameof()).</param>
    public static void AssertSerializationFails<T>(T obj, string propertyName)
    {
        JsonSerializationException exception = Assert.Throws<JsonSerializationException>(
            () => JsonConvert.SerializeObject(obj, s_serializerSettings));

        // Newtonsoft.Json error messages use the format: "Cannot write a null value for property 'name'..."
        // Note: property name is lowercase in serialization error messages
        exception.Message.ShouldContain($"property '{propertyName.ToLowerInvariant()}'");
    }

    /// <summary>
    /// Normalizes a JSON string by removing trailing whitespace and normalizing line endings.
    /// </summary>
    private static string NormalizeJson(string json)
    {
        return json.Trim().Replace("\r\n", "\n");
    }

    /// <summary>
    /// Serializes an object to JSON using test settings (with StringEnumConverter).
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>The JSON string representation.</returns>
    private static string Serialize<T>(T obj)
    {
        return JsonConvert.SerializeObject(obj, s_serializerSettings);
    }

    /// <summary>
    /// Deserializes a JSON string to an object using test settings (with StringEnumConverter).
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize to.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="InvalidOperationException">Thrown when deserialization returns null.</exception>
    private static T Deserialize<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json, s_serializerSettings)
            ?? throw new InvalidOperationException($"Deserialization of {typeof(T).Name} returned null.");
    }
}
