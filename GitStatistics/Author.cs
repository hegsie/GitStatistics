using System;

namespace GitStatistics
{
    public class Author
    {
        public int LinesAdded { get; set; }

        public DateTime LastCommitStamp { get; set; }
        public DateTime FirstCommitStamp { get; set; }

        public decimal Commits { get; set; }
        public DateTime LastActiveDay { get; set; }

        public int ActiveDays { get; set; }

        public int PlaceByCommits { get; set; }

        public decimal CommitsFrac { get; set; }

        public DateTime DateFirst { get; set; }

        public DateTime DateLast { get; set; }

        public TimeSpan TimeDelta { get; set; }

        public int LinesRemoved { get; set; }
    }
}
