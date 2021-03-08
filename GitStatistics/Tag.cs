using System;
using System.Collections.Generic;

namespace GitStatistics
{
    public class Tag
    {
        public DateTime Stamp { get; set; }

        public string Hash { get; set; }

        public DateTime Date { get; set; }


        public int Commits { get; set; }

        public Dictionary<string, int> Authors { get; set; }
    }
}
