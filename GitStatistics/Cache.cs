using System.Collections.Generic;

namespace GitStatistics
{
    public class Cache
    {
        public Cache()
        {
            FilesInTree = new Dictionary<string, int>();
            LinesInBlob = new Dictionary<string, int>();
        }

        public Dictionary<string, int> FilesInTree { get; set; }

        public Dictionary<string, int> LinesInBlob { get; set; }
    }
}
