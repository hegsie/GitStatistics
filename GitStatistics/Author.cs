using System;

namespace GitStatistics
{
    public class Author
    {
        public int LinesAdded { get; set; }

        public DateTime LastCommitStamp { get; set; }
        public DateTime FirstCommitStamp { get; set; }

        public int Commits { get; set; }
        public DateTime LastActiveDay { get; set; }

        public int ActiveDays { get; set; }

        public int PlaceByCommits { get; set; }

        public int CommitsFrac { get; set; }

        public DateTime DateFirst { get; set; }

        public DateTime DateLast { get; set; }

        public TimeSpan TimeDelta { get; set; }

        public int LinesRemoved { get; set; }
    }
}
