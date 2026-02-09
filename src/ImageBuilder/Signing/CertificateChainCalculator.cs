// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Formats.Cbor;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Calculates certificate chain thumbprints from COSE signature envelopes.
/// </summary>
public static class CertificateChainCalculator
{
    /// <summary>
    /// COSE_Sign1 tag value per RFC 8152.
    /// </summary>
    private const CborTag CoseSign1Tag = (CborTag)18;

    /// <summary>
    /// COSE x5chain header key per RFC 9360.
    /// </summary>
    private const int CoseX5ChainKey = 33;

    /// <summary>
    /// Calculates SHA256 thumbprints for each certificate in the x5chain from a COSE_Sign1 envelope.
    /// </summary>
    /// <param name="signedPayloadPath">Path to the COSE signature envelope file.</param>
    /// <returns>JSON array of hex-encoded SHA256 thumbprints (e.g., ["abc123...", "def456..."]).</returns>
    public static string CalculateCertificateChainThumbprints(string signedPayloadPath)
    {
        var fileBytes = File.ReadAllBytes(signedPayloadPath);
        var reader = new CborReader(fileBytes);

        // Read and verify COSE_Sign1 tag
        var tag = reader.ReadTag();
        if (tag != CoseSign1Tag)
        {
            throw new InvalidOperationException($"Expected COSE_Sign1 tag ({(int)CoseSign1Tag}), got {(int)tag}.");
        }

        // COSE_Sign1 structure is an array: [protected, unprotected, payload, signature]
        var arrayLength = reader.ReadStartArray();
        if (arrayLength < 2)
        {
            throw new InvalidOperationException("Invalid COSE_Sign1 structure.");
        }

        // Skip protected header (index 0)
        reader.SkipValue();

        // Read unprotected header (index 1) - this is a map
        var x5chainBytes = ReadX5ChainFromMap(reader);

        var thumbprints = new List<string>();
        foreach (var certBytes in x5chainBytes)
        {
            thumbprints.Add(ComputeSha256Hex(certBytes));
        }

        return JsonSerializer.Serialize(thumbprints);
    }

    /// <summary>
    /// Reads the x5chain (key 33) from a CBOR map, returning certificate bytes.
    /// </summary>
    private static List<byte[]> ReadX5ChainFromMap(CborReader reader)
    {
        var mapLength = reader.ReadStartMap();
        var x5chainArray = new List<byte[]>();

        for (var i = 0; i < mapLength; i++)
        {
            // COSE header keys can be integers or text strings (RFC 8152 §3.1).
            // We only care about the integer key 33 (x5chain), so skip text string keys.
            if (reader.PeekState() == CborReaderState.TextString)
            {
                reader.SkipValue(); // skip the text key
                reader.SkipValue(); // skip its value
                continue;
            }

            var key = reader.ReadInt32();

            if (key == CoseX5ChainKey)
            {
                var state = reader.PeekState();

                if (state == CborReaderState.ByteString)
                {
                    // Single certificate
                    x5chainArray.Add(reader.ReadByteString());
                }
                else if (state == CborReaderState.StartArray)
                {
                    // Array of certificates
                    var certArrayLength = reader.ReadStartArray();
                    for (var j = 0; j < certArrayLength; j++)
                    {
                        x5chainArray.Add(reader.ReadByteString());
                    }
                    reader.ReadEndArray();
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected x5chain value type: {state}");
                }
            }
            else
            {
                reader.SkipValue();
            }
        }

        reader.ReadEndMap();

        if (x5chainArray.Count == 0)
        {
            throw new InvalidOperationException("x5chain not found in unprotected header.");
        }

        return x5chainArray;
    }

    /// <summary>
    /// Computes the SHA256 hash of the given bytes and returns it as a lowercase hex string.
    /// </summary>
    private static string ComputeSha256Hex(byte[] data)
    {
        var hashBytes = SHA256.HashData(data);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
