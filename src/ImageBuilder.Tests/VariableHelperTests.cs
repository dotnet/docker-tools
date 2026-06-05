#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    [TestClass]
    public class VariableHelperTests
    {
        [TestMethod]
        public void NestedVariables()
        {
            Manifest manifest = CreateManifest();
            manifest.Variables = new Dictionary<string, string>()
            {
                { "test", "abc" },
                { "test2", "$(test)" },
                { "test3", "$(test2)" },
                { "test4", "xyz" }
            };

            Mock<IManifestOptionsInfo> manifestOptionsInfoMock = new();
            manifestOptionsInfoMock
                .SetupGet(o => o.Variables)
                .Returns(new Dictionary<string, string>
                {
                    { "test4", "$(test)-123" }
                });

            VariableHelper helper = new(manifest, manifestOptionsInfoMock.Object, id => null);

            helper.ResolvedVariables.Count.ShouldBe(4);
            helper.ResolvedVariables["test"].ShouldBe("abc");
            helper.ResolvedVariables["test2"].ShouldBe("abc");
            helper.ResolvedVariables["test3"].ShouldBe("abc");
            helper.ResolvedVariables["test4"].ShouldBe("abc-123");
        }

        [TestMethod]
        public void ReferenceToUnresolvedVariable()
        {
            Manifest manifest = CreateManifest();
            manifest.Variables = new Dictionary<string, string>()
            {
                { "test1", "$(test2)" },
                { "test2", "abc" }
            };

            Should.Throw<NotSupportedException>(() => new VariableHelper(manifest, Mock.Of<IManifestOptionsInfo>(), id => null));
        }

        [TestMethod]
        public void ReferenceToUndefinedVariable()
        {
            Manifest manifest = CreateManifest();
            manifest.Variables = new Dictionary<string, string>()
            {
                { "test1", "$(test2)" }
            };


            Should.Throw<InvalidOperationException>(() => new VariableHelper(manifest, Mock.Of<IManifestOptionsInfo>(), id => null));
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void ProvideNewVariableThroughOptions(bool hasManifestVariables)
        {
            Manifest manifest = CreateManifest();

            if (hasManifestVariables)
            {
                manifest.Variables = new Dictionary<string, string>()
                {
                    { "predefinedVar", "123" },
                };
            }

            Dictionary<string, string> optionsVariables = new()
            {
                { "newVar", "abc" }
            };

            if (hasManifestVariables)
            {
                optionsVariables.Add("newDerivativeVar", "$(predefinedVar)456");
            }

            Mock<IManifestOptionsInfo> manifestOptionsInfoMock = new();
            manifestOptionsInfoMock
                .SetupGet(o => o.Variables)
                .Returns(optionsVariables);

            VariableHelper helper = new(manifest, manifestOptionsInfoMock.Object, id => null);

            if (hasManifestVariables)
            {
                helper.ResolvedVariables.Count.ShouldBe(3);
                helper.ResolvedVariables["predefinedVar"].ShouldBe("123");
                helper.ResolvedVariables["newVar"].ShouldBe("abc");
                helper.ResolvedVariables["newDerivativeVar"].ShouldBe("123456");
            }
            else
            {
                helper.ResolvedVariables.ShouldHaveSingleItem();
                helper.ResolvedVariables["newVar"].ShouldBe("abc");
            }
        }
    }
}
