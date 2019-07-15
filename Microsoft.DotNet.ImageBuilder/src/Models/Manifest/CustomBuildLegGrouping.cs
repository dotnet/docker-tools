using System.ComponentModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Manifest
{
    [Description(
        "This object describes the tag dependencies of the image for a specific named scenario. This is " +
        "for advanced cases only. It allows tooling to modify the build matrix that would normally be " +
        "generated for the image by including the customizations described in this metadata. An example " +
        "usage of this is in PR builds where it is necessary to build and test in the same job. In such " +
        "a scenario, some images are part of a test matrix that require images to be available on the " +
        "build machine that aren't part of that images dependency graph in normal scenarios. By " +
        "specifying a customBuildLegGrouping for this scenario, those additional image dependencies can " +
        "be specified and the build pipeline can make use of them when constructing its build graph when " +
        "specified to do so."
        )]
    public class CustomBuildLegGrouping
    {
        [Description("Name of the grouping. This is just a custom label that can then be used by tooling to lookup the grouping when necessary.")]
        [JsonProperty(Required = Required.Always)]
        public string Name { get; set; }

        [Description("The set of image tags that this image is dependent upon for this scenario.")]
        [JsonProperty(Required = Required.Always)]
        public string[] Dependencies { get; set; }
    }
}
