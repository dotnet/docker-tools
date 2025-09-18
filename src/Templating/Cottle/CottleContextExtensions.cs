// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Cottle;
using Microsoft.DotNet.ImageBuilder.ReadModel;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.DotNet.DockerTools.Templating.Cottle;

public static class CottleContextExtensions
{
    extension(IContext context)
    {
        public IContext Add(IReadOnlyDictionary<string, string> variables)
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

public static class PlatformInfoVariableExtensions
{
    extension(PlatformInfo platform)
    {
        public IReadOnlyDictionary<string, string> PlatformSpecificTemplateVariables =>
            new Dictionary<string, string>()
            {
                { "ARCH_SHORT", platform.Model.Architecture.ShortName },
                { "ARCH_NUPKG", platform.Model.Architecture.NupkgName },
                { "ARCH_VERSIONED", platform.ArchWithVariant },
                { "ARCH_TAG_SUFFIX", $"-{platform.ArchWithVariant}" },
                { "PRODUCT_VERSION", platform.Image.ProductVersion ?? "" },
                { "OS_VERSION", platform.Model.OsVersion },
                { "OS_VERSION_BASE", "" },
                { "OS_VERSION_NUMBER", platform.GetOsVersionNumber() },
                { "OS_ARCH_HYPHENATED", platform.GetOsArchHyphenatedName() },
            };
    }
}

internal static partial class PlatformInfoExtensions
{
    extension(PlatformInfo platform)
    {
        public string ArchWithVariant => platform.Model.Architecture.LongName + platform.ArchVariant;
        public string ArchVariant => platform.Model.Variant?.ToLowerInvariant() ?? "";
        public string BaseOsVersion => platform.Model.OsVersion.TrimEndString("-slim");

        public string GetOsVersionNumber()
        {
            const string PrefixGroup = "Prefix";
            const string VersionGroup = "Version";
            const string LtscPrefix = "ltsc";
            Match match = OsVersionRegex.Match(platform.Model.OsVersion);

            string versionNumber = string.Empty;
            if (match.Groups[PrefixGroup].Success && match.Groups[PrefixGroup].Value == LtscPrefix)
            {
                versionNumber = LtscPrefix;
            }

            versionNumber += match.Groups[VersionGroup].Value;
            return versionNumber;
        }

        public string GetOsArchHyphenatedName()
        {
            string osName;
            if (platform.BaseOsVersion.Contains("nanoserver"))
            {
                string version = platform.BaseOsVersion.Split('-')[1];
                osName = $"NanoServer-{version}";
            }
            else if (platform.BaseOsVersion.Contains("windowsservercore"))
            {
                string version = platform.BaseOsVersion.Split('-')[1];
                osName = $"WindowsServerCore-{version}";
            }
            else
            {
                osName = platform.OSDisplayName.Replace(' ', '-');
            }

            string archName = platform.Model.Architecture != Architecture.AMD64
                ? $"-{platform.Model.Architecture.GetDisplayName()}"
                : string.Empty;

            return osName + archName;
        }

        private string OSDisplayName => platform.Model.OS switch
        {
            OS.Windows => GetWindowsOSDisplayName(platform.BaseOsVersion),
            _ => GetLinuxOSDisplayName(platform.BaseOsVersion)
        };
    }

    extension(Architecture architecture)
    {
        public string ShortName => architecture switch
        {
            Architecture.AMD64 => "x64",
            _ => architecture.ToString().ToLowerInvariant(),
        };

        public string NupkgName => architecture switch
        {
            Architecture.AMD64 => "x64",
            Architecture.ARM => "arm32",
            _ => architecture.ToString().ToLowerInvariant(),
        };

        public string LongName => architecture switch
        {
            Architecture.ARM => "arm32",
            _ => architecture.ToString().ToLowerInvariant(),
        };

        private string GetDisplayName(string? variant = null)
        {
            string displayName = architecture switch
            {
                Architecture.ARM => "arm32",
                _ => architecture.ToString().ToLowerInvariant(),
            };

            if (variant != null)
            {
                displayName += variant.ToLowerInvariant();
            }

            return displayName;
        }

        public string DockerName => architecture.ToString().ToLowerInvariant();
    }

    extension(OS os)
    {
        public string DockerName => os.ToString().ToLowerInvariant();
    }

    private static string GetWindowsOSDisplayName(string osName)
    {
        string version = osName.Split('-')[1];
        return osName switch
        {
            var s when s.StartsWith("nanoserver") =>
                GetWindowsVersionDisplayName("Nano Server", version),
            var s when s.StartsWith("windowsservercore") =>
                GetWindowsVersionDisplayName("Windows Server Core", version),
            _ => throw new NotSupportedException($"The OS version '{osName}' is not supported.")
        };
    }

    private static string GetLinuxOSDisplayName(string osName) => osName switch
    {
        string s when s.Contains("debian") => "Debian",
        string s when s.Contains("bookworm") => "Debian 12",
        string s when s.Contains("trixie") => "Debian 13",
        string s when s.Contains("forky") => "Debian 14",
        string s when s.Contains("duke") => "Debian 15",
        string s when s.Contains("jammy") => "Ubuntu 22.04",
        string s when s.Contains("noble") => "Ubuntu 24.04",
        string s when s.Contains("azurelinux") => FormatVersionableOsName(osName, name => "Azure Linux"),
        string s when s.Contains("cbl-mariner") => FormatVersionableOsName(osName, name => "CBL-Mariner"),
        string s when s.Contains("leap") => FormatVersionableOsName(osName, name => "openSUSE Leap"),
        string s when s.Contains("ubuntu") => FormatVersionableOsName(osName, name => "Ubuntu"),
        string s when s.Contains("alpine")
            || s.Contains("centos")
            || s.Contains("fedora") => FormatVersionableOsName(osName, name => name.FirstCharToUpper()),
        _ => throw new NotSupportedException($"The OS version '{osName}' is not supported.")
    };

    private static string GetWindowsVersionDisplayName(string windowsName, string version) =>
        version.StartsWith("ltsc") switch
        {
            true => $"{windowsName} {version.TrimStartString("ltsc")}",
            false => $"{windowsName}, version {version}"
        };

    private static string FormatVersionableOsName(string os, Func<string, string> formatName)
    {
        (string osName, string osVersion) = GetOsVersionInfo(os);
        if (string.IsNullOrEmpty(osVersion))
        {
            return formatName(osName);
        }
        else
        {
            return $"{formatName(osName)} {osVersion}";
        }
    }

    private static (string Name, string Version) GetOsVersionInfo(string os)
    {
        // Regex matches an os name ending in a non-numeric or decimal character and up to
        // a 3 part version number. Any additional characters are dropped (e.g. -distroless).
        Regex versionRegex = new Regex(@"(?<name>.+[^0-9\.])(?<version>\d+(\.\d*){0,2})");
        Match match = versionRegex.Match(os);

        if (match.Success)
        {
            return (match.Groups["name"].Value, match.Groups["version"].Value);
        }
        else
        {
            return (os, string.Empty);
        }
    }

    public static string FirstCharToUpper(this string source) => char.ToUpper(source[0]) + source.Substring(1);

    [return: NotNullIfNotNull(nameof(source))]
    private static string? TrimEndString(this string? source, string trim) => source switch
    {
        string s when s.EndsWith(trim) => s.Substring(0, s.Length - trim.Length).TrimEndString(trim),
        _ => source,
    };

    [return: NotNullIfNotNull(nameof(source))]
    private static string? TrimStartString(this string? source, string trim) => source switch
    {
        string s when s.StartsWith(trim) => s.Substring(trim.Length).TrimStartString(trim),
        _ => source,
    };

    [GeneratedRegex(@"(-(?<Prefix>[a-zA-Z_]*))?(?<Version>\d+.\d+)")]
    private static partial Regex OsVersionRegex { get; }
}
