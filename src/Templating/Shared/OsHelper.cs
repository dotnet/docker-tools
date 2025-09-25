// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.DockerTools.Templating.Shared;

internal static class OsHelper
{
    public static string GetWindowsOSDisplayName(string osName)
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

    public static string GetLinuxOSDisplayName(string osName) => osName switch
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
}
