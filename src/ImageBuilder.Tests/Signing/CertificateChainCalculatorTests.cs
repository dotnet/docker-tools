// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.DotNet.ImageBuilder.Signing;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Signing;

public class CertificateChainCalculatorTests
{
    [Fact]
    public void CalculateCertificateChainThumbprints_IntegerKeysOnly_ReturnsThumbprints()
    {
        byte[] cert1 = [0x30, 0x82, 0x01, 0x00];
        byte[] cert2 = [0x30, 0x82, 0x02, 0x00];

        var (fileSystem, filePath) = WriteCoseSign1(cert1, cert2, includeTextKey: false);

        var result = CertificateChainCalculator.CalculateCertificateChainThumbprints(filePath, fileSystem);

        var thumbprints = JsonSerializer.Deserialize<string[]>(result);
        thumbprints.ShouldNotBeNull();
        thumbprints.Length.ShouldBe(2);
        thumbprints[0].ShouldBe(ComputeSha256Hex(cert1));
        thumbprints[1].ShouldBe(ComputeSha256Hex(cert2));
    }

    [Fact]
    public void CalculateCertificateChainThumbprints_MixedIntegerAndTextKeys_ReturnsThumbprints()
    {
        byte[] cert = [0x30, 0x82, 0x03, 0x00];

        var (fileSystem, filePath) = WriteCoseSign1(cert, includeTextKey: true);

        var result = CertificateChainCalculator.CalculateCertificateChainThumbprints(filePath, fileSystem);

        var thumbprints = JsonSerializer.Deserialize<string[]>(result);
        thumbprints.ShouldNotBeNull();
        thumbprints.Length.ShouldBe(1);
        thumbprints[0].ShouldBe(ComputeSha256Hex(cert));
    }

    [Fact]
    public void CalculateCertificateChainThumbprints_SingleCert_ReturnsThumbprint()
    {
        byte[] cert = [0x30, 0x82, 0x04, 0x00];

        var (fileSystem, filePath) = WriteCoseSign1SingleCert(cert);

        var result = CertificateChainCalculator.CalculateCertificateChainThumbprints(filePath, fileSystem);

        var thumbprints = JsonSerializer.Deserialize<string[]>(result);
        thumbprints.ShouldNotBeNull();
        thumbprints.Length.ShouldBe(1);
        thumbprints[0].ShouldBe(ComputeSha256Hex(cert));
    }

    [Fact]
    public void CalculateCertificateChainThumbprints_MissingX5Chain_ThrowsException()
    {
        var (fileSystem, filePath) = WriteCoseSign1WithoutX5Chain();

        Should.Throw<InvalidOperationException>(
            () => CertificateChainCalculator.CalculateCertificateChainThumbprints(filePath, fileSystem))
            .Message.ShouldContain("x5chain not found");
    }

    /// <summary>
    /// Writes a COSE_Sign1 envelope with an x5chain array of certificates.
    /// Optionally includes a text string key in the unprotected header map.
    /// </summary>
    private static (InMemoryFileSystem FileSystem, string FilePath) WriteCoseSign1(
        byte[] cert1, byte[]? cert2 = null, bool includeTextKey = false)
    {
        var writer = new CborWriter();
        writer.WriteTag((CborTag)18); // COSE_Sign1

        writer.WriteStartArray(4);

        // [0] Protected header (empty bstr)
        writer.WriteByteString([]);

        // [1] Unprotected header map
        var mapEntries = 1 + (includeTextKey ? 1 : 0);
        writer.WriteStartMap(mapEntries);

        if (includeTextKey)
        {
            writer.WriteTextString("some-custom-key");
            writer.WriteTextString("some-value");
        }

        // x5chain key = 33
        writer.WriteInt32(33);
        var certs = cert2 is not null ? new[] { cert1, cert2 } : new[] { cert1 };
        writer.WriteStartArray(certs.Length);
        foreach (var cert in certs)
        {
            writer.WriteByteString(cert);
        }
        writer.WriteEndArray();

        writer.WriteEndMap();

        // [2] Payload
        writer.WriteByteString([0x01, 0x02]);

        // [3] Signature
        writer.WriteByteString([0xFF, 0xFE]);

        writer.WriteEndArray();

        var fileSystem = new InMemoryFileSystem();
        var filePath = "/test/payload.cose";
        fileSystem.AddFile(filePath, writer.Encode());
        return (fileSystem, filePath);
    }

    /// <summary>
    /// Writes a COSE_Sign1 envelope with a single certificate (byte string, not array).
    /// </summary>
    private static (InMemoryFileSystem FileSystem, string FilePath) WriteCoseSign1SingleCert(byte[] cert)
    {
        var writer = new CborWriter();
        writer.WriteTag((CborTag)18);

        writer.WriteStartArray(4);
        writer.WriteByteString([]);

        writer.WriteStartMap(1);
        writer.WriteInt32(33);
        writer.WriteByteString(cert); // single cert, not wrapped in array
        writer.WriteEndMap();

        writer.WriteByteString([0x01, 0x02]);
        writer.WriteByteString([0xFF, 0xFE]);
        writer.WriteEndArray();

        var fileSystem = new InMemoryFileSystem();
        var filePath = "/test/payload.cose";
        fileSystem.AddFile(filePath, writer.Encode());
        return (fileSystem, filePath);
    }

    /// <summary>
    /// Writes a COSE_Sign1 envelope with no x5chain key.
    /// </summary>
    private static (InMemoryFileSystem FileSystem, string FilePath) WriteCoseSign1WithoutX5Chain()
    {
        var writer = new CborWriter();
        writer.WriteTag((CborTag)18);

        writer.WriteStartArray(4);
        writer.WriteByteString([]);

        writer.WriteStartMap(1);
        writer.WriteInt32(99); // not x5chain
        writer.WriteByteString([0x00]);
        writer.WriteEndMap();

        writer.WriteByteString([0x01, 0x02]);
        writer.WriteByteString([0xFF, 0xFE]);
        writer.WriteEndArray();

        var fileSystem = new InMemoryFileSystem();
        var filePath = "/test/payload.cose";
        fileSystem.AddFile(filePath, writer.Encode());
        return (fileSystem, filePath);
    }

    private static string ComputeSha256Hex(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
}
