using System.Collections.Generic;

namespace ImageBuilder.Model
{
    public class Repo
    {
        public string DockerRepo { get; set; }
        public Image[] Images { get; set; }
        public IDictionary<string, string[]> TestCommands {get;set;}
    }
}
