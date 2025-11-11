// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Cottle;
using Microsoft.DotNet.DockerTools.Templating.Shared;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ReadModel;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.DockerTools.Templating.Cottle;

public static class CottleContextExtensions
{
    extension(IContext context)
    {
        public IContext Add(Value key, Value value)
        {
            var newContext = Context.CreateCustom(new Dictionary<Value, Value> { { key, value } });
            return Context.CreateCascade(primary: newContext, fallback: context);
        }

        public IContext Add(Dictionary<Value, Value> symbols)
        {
            var newContext = Context.CreateCustom(symbols);
            return Context.CreateCascade(primary: newContext, fallback: context);
        }

        public IContext Add(IDictionary<string, string> variables)
        {
            var variablesDictionary = variables.ToCottleDictionary();
            var newContext = Context.CreateCustom(variablesDictionary);
            return Context.CreateCascade(newContext, context);
        }
    }

    extension(IDictionary<string, string> stringDictionary)
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
        public Dictionary<string, string> PlatformSpecificTemplateVariables => new()
        {
            { "ARCH_SHORT", platform.Model.Architecture.ShortName },
            { "ARCH_NUPKG", platform.Model.Architecture.NupkgName },
            { "ARCH_VERSIONED", platform.ArchWithVariant },
            { "ARCH_TAG_SUFFIX", $"-{platform.ArchWithVariant}" },
            { "PRODUCT_VERSION", platform.Image.ProductVersion ?? "" },
            { "OS_VERSION", platform.Model.OsVersion },
            { "OS_VERSION_BASE", platform.BaseOsVersion },
            { "OS_VERSION_NUMBER", platform.GetOsVersionNumber() },
            { "OS_ARCH_HYPHENATED", platform.GetOsArchHyphenatedName() },
        };
    }
}

public static class ManifestInfoVariableExtensions
{
    extension(ManifestInfo manifest)
    {
        public Dictionary<string, string> TemplateVariables => new()
        {
            { "IS_PRODUCT_FAMILY", true.ToString() },
        };
    }
}

public static class RepoInfoVariableExtensions
{
    extension(RepoInfo repo)
    {
        public Dictionary<string, string> TemplateVariables => new()
        {
            { "REPO", repo.Model.Name },
            { "FULL_REPO", repo.FullName },
            { "PARENT_REPO", repo.GetParentRepoName() },
            { "SHORT_REPO", repo.ShortName },
        };

        private string ShortName =>
            // LastIndexOf returns -1 when not found, so in the case the repo
            // name doesn't have any slashes, (-1 + 1) becomes 0 which selects
            // the whole string.
            repo.Model.Name[(repo.Model.Name.LastIndexOf('/') + 1)..];

        private string GetParentRepoName()
        {
            // Avoid using string.Split(...) to prevent array allocation.
            var name = repo.Model.Name;
            int last = name.LastIndexOf('/');
            if (last <= 0)
            {
                return string.Empty;
            }

            int prev = name.LastIndexOf('/', last - 1);
            return name[(prev + 1)..last];
        }
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

        public string DockerName => architecture.ToString().ToLowerInvariant();
    }

    extension(OS os)
    {
        public string DockerName => os.ToString().ToLowerInvariant();
    }

    [GeneratedRegex(@"(-(?<Prefix>[a-zA-Z_]*))?(?<Version>\d+.\d+)")]
    private static partial Regex OsVersionRegex { get; }
}
