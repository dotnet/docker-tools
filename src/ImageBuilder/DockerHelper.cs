// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder;

public static class DockerHelper
{
    private static readonly Lazy<Architecture> LazyArchitecture = new(GetArchitecture);
    private static readonly Lazy<OS> LazyOs = new(GetOS);

    public static Architecture Architecture => LazyArchitecture.Value;
    public static OS OS => LazyOs.Value;

    public const string DockerHubRegistry = "docker.io";
    public const string DockerHubApiRegistry = "registry-1.docker.io";
    public const string AcrDomain = ".azurecr.io";

    public static string FormatAcrName(string acrName)
    {
        if (!acrName.EndsWith(AcrDomain, StringComparison.OrdinalIgnoreCase))
        {
            acrName = $"{acrName}{AcrDomain}";
        }

        return acrName;
    }

    public static Uri GetAcrUri(string acrName) => new($"https://{FormatAcrName(acrName)}");

    public static IEnumerable<string> GetImageDigests(string image, bool isDryRun, CancellationToken cancellationToken)
    {
        string digests = ExecuteCommandWithFormat(
            "inspect", "index .RepoDigests", "Failed to retrieve image digests", cancellationToken, image, isDryRun);

        string trimmedDigests = digests.TrimStart('[').TrimEnd(']');
        if (trimmedDigests == string.Empty)
        {
            return [];
        }

        return trimmedDigests.Split(' ');
    }

    public static long GetImageSize(string image, bool isDryRun, CancellationToken cancellationToken)
    {
        string size = ExecuteCommandWithFormat(
            "inspect", ".Size", "Failed to retrieve image size", cancellationToken, additionalArgs: image, isDryRun: isDryRun);
        return isDryRun ? 0 : long.Parse(size);
    }

    public static string GetRepo(string image)
    {
        int tagSeparator = GetTagOrDigestSeparatorIndex(image);
        if (tagSeparator >= 0)
        {
            return image[..tagSeparator];
        }

        return image;
    }

    public static bool LocalImageExists(string tag, bool isDryRun, CancellationToken cancellationToken) => ResourceExists(ManagementType.Image, tag, isDryRun, cancellationToken);

    public static void Login(RegistryCredentials credentials, string server, bool isDryRun, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(
            "docker", $"login -u {credentials.Username} --password-stdin {server}")
        {
            RedirectStandardInput = true
        };
        ExecuteHelper.ExecuteWithRetry(
            startInfo,
            cancellationToken,
            process =>
            {
                process.StandardInput.WriteLine(credentials.Password);
                process.StandardInput.Close();
            },
            isDryRun);
    }

    public static void Logout(string server, bool isDryRun, CancellationToken cancellationToken) =>
        ExecuteHelper.ExecuteWithRetry("docker", $"logout {server}", isDryRun, cancellationToken);

    public static void PullImage(string image, string? platform, bool isDryRun, CancellationToken cancellationToken)
    {
        string platformArg = "";
        if (platform is not null)
        {
            platformArg = $"--platform {platform} ";
        }

        ExecuteHelper.ExecuteWithRetry("docker", $"pull {platformArg}{image}", isDryRun, cancellationToken);
    }

    public static string ReplaceRepo(string image, string newRepo) =>
        newRepo + image[GetTagOrDigestSeparatorIndex(image)..];

    public static void CreateTag(string image, string tag, bool isDryRun, CancellationToken cancellationToken) => ExecuteCommand("tag", "Failed to create tag", cancellationToken, $"{image} {tag}", isDryRun);

    public static string GetCreatedDate(string image, bool isDryRun, CancellationToken cancellationToken)
    {
        return ExecuteCommandWithFormat(
            "inspect", ".Created", "Failed to retrieve created date", cancellationToken, image, isDryRun);
    }

    public static string GetDigestSha(string digest) => digest[(digest.IndexOf("@") + 1)..];

    public static string GetDigestString(string repo, string sha) => $"{repo}@{sha}";

    public static string GetImageName(string? registry, string repo, string? tag = null, string? digest = null)
    {
        if (tag != null && digest != null)
        {
            throw new InvalidOperationException($"Invalid to provide both the {nameof(tag)} and {nameof(digest)} arguments.");
        }

        string imageName = registry ?? string.Empty;
        if (imageName.Length > 0)
        {
            imageName += $"/";
        }

        imageName += repo;

        if (tag != null)
        {
            return $"{imageName}:{tag}";
        }
        else if (digest != null)
        {
            return $"{imageName}@{digest}";
        }

        return imageName;
    }

    public static string GetTagName(string imageName) => imageName[(GetTagOrDigestSeparatorIndex(imageName) + 1)..];

    public static string NormalizeRepo(string image)
    {
        string? registry = GetRegistry(image);
        string repoAndTag = TrimRegistry(image, registry);

        if ((registry is null || registry == DockerHubRegistry) && !repoAndTag.Contains('/'))
        {
            repoAndTag = $"library/{repoAndTag}";
        }

        if (registry is null)
        {
            return repoAndTag;
        }

        return $"{registry}/{repoAndTag}";
    }

    public static string TrimRegistry(string tag) => TrimRegistry(tag, GetRegistry(tag));

    public static string TrimRegistry(string tag, string? registry) => tag.TrimStartString($"{registry}/");

    public static bool IsInRegistry(string tag, string registry) => registry is not null && tag.StartsWith(registry, StringComparison.OrdinalIgnoreCase);

    public static string? GetRegistry(string imageName)
    {
        int separatorIndex = imageName.IndexOf("/");
        if (separatorIndex >= 0)
        {
            string firstSegment = imageName[..separatorIndex];
            if (firstSegment.Contains('.') || firstSegment.Contains(':'))
            {
                return firstSegment;
            }
        }

        return null;
    }

    private static int GetTagOrDigestSeparatorIndex(string imageName)
    {
        int separatorPosition = imageName.IndexOf('@');
        if (separatorPosition < 0)
        {
            separatorPosition = imageName.IndexOf(':');
        }

        return separatorPosition;
    }

    private static OS GetOS()
    {
        string osString = ExecuteCommandWithFormat("version", ".Server.Os", "Failed to detect Docker OS", CancellationToken.None);
        if (!Enum.TryParse(osString, true, out OS os))
        {
            throw new PlatformNotSupportedException("Unknown Docker OS");
        }

        return os;
    }

    private static bool ResourceExists(ManagementType type, string filterArg, bool isDryRun, CancellationToken cancellationToken)
    {
        string output = ExecuteCommand(
            $"{Enum.GetName(typeof(ManagementType), type)?.ToLowerInvariant()} ls -a -q {filterArg}",
            "Failed to find resource",
            cancellationToken,
            isDryRun: isDryRun);
        return output != "";
    }

    private static Architecture GetArchitecture()
    {
        string infoArchitecture = ExecuteCommandWithFormat(
            "info", ".Architecture", "Failed to detect Docker architecture", CancellationToken.None);
        Architecture architecture = infoArchitecture switch
        {
            "x86_64" => Architecture.AMD64,
            "arm" or "arm_32" or "armv7l" => Architecture.ARM,
            "aarch64" or "arm64" => Architecture.ARM64,
            _ => throw new PlatformNotSupportedException($"Unknown Docker Architecture '{infoArchitecture}'"),
        };
        return architecture;
    }

    public static string ExecuteCommand(
        string command, string errorMessage, CancellationToken cancellationToken, string? additionalArgs = null, bool isDryRun = false)
    {
        string output = ExecuteHelper.Execute("docker", $"{command} {additionalArgs}", isDryRun, cancellationToken, errorMessage);
        return isDryRun ? "" : output;
    }

    private static string ExecuteCommandWithFormat(
        string command, string outputFormat, string errorMessage, CancellationToken cancellationToken, string? additionalArgs = null, bool isDryRun = false) =>
        ExecuteCommand(command, errorMessage, cancellationToken, $"{additionalArgs} -f \"{{{{ {outputFormat} }}}}\"", isDryRun);

    private enum ManagementType
    {
        Image,
        Container,
    }
}
