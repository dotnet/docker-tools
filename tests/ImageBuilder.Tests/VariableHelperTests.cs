// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class VariableHelperTests
    {
        [Fact]
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

            Assert.Equal(4, helper.ResolvedVariables.Count);
            Assert.Equal("abc", helper.ResolvedVariables["test"]);
            Assert.Equal("abc", helper.ResolvedVariables["test2"]);
            Assert.Equal("abc", helper.ResolvedVariables["test3"]);
            Assert.Equal("abc-123", helper.ResolvedVariables["test4"]);
        }

        [Fact]
        public void ReferenceToUnresolvedVariable()
        {
            Manifest manifest = CreateManifest();
            manifest.Variables = new Dictionary<string, string>()
            {
                { "test1", "$(test2)" },
                { "test2", "abc" }
            };

            Assert.Throws<NotSupportedException>(() => new VariableHelper(manifest, Mock.Of<IManifestOptionsInfo>(), id => null));
        }

        [Fact]
        public void ReferenceToUndefinedVariable()
        {
            Manifest manifest = CreateManifest();
            manifest.Variables = new Dictionary<string, string>()
            {
                { "test1", "$(test2)" }
            };


            Assert.Throws<InvalidOperationException>(() => new VariableHelper(manifest, Mock.Of<IManifestOptionsInfo>(), id => null));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
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
                Assert.Equal(3, helper.ResolvedVariables.Count);
                Assert.Equal("123", helper.ResolvedVariables["predefinedVar"]);
                Assert.Equal("abc", helper.ResolvedVariables["newVar"]);
                Assert.Equal("123456", helper.ResolvedVariables["newDerivativeVar"]);
            }
            else
            {
                Assert.Equal(1, helper.ResolvedVariables.Count);
                Assert.Equal("abc", helper.ResolvedVariables["newVar"]);
            }
        }
    }
}
