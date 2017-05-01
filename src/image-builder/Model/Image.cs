using System.Collections.Generic;

namespace ImageBuilder.Model
{
    public class Image
    {
        public string[] SharedTags { get; set; }
        public IDictionary<string, Platform> Platforms { get; set; }
    }
}
