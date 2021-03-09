using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GitStatistics
{
    public class GitDataCollector : DataCollector
    {
        private readonly CultureInfo _cultureInfo;
        private readonly Calendar _calendar;
        private Configuration _configuration;

        public GitDataCollector()
        {
            // Gets the Calendar instance associated with a CultureInfo.
            _cultureInfo = new CultureInfo("en-US");
            _calendar = _cultureInfo.Calendar;

            ActivityByHourOfDay = new DictionaryWithDefault<int, decimal>();
            ActivityByDayOfWeek = new DictionaryWithDefault<int, int>();
            ActivityByMonthOfYear = new DictionaryWithDefault<int, int>();
            ActivityByHourOfWeek = new DictionaryWithDefault<int, DictionaryWithDefault<int, int>>();
            ActivityByHourOfDayBusiest = 0;
            ActivityByHourOfWeekBusiest = 0;
            ActivityByYearWeek = new DictionaryWithDefault<string, int>();
            ActivityByYearWeekPeak = 0;
            Authors = new DictionaryWithDefault<string, Author>();
            Domains = new DictionaryWithDefault<string, Domain>();
            AuthorOfMonth = new DictionaryWithDefault<string, DictionaryWithDefault<string, int>>();
            AuthorOfYear = new DictionaryWithDefault<int, DictionaryWithDefault<string, int>>();
            CommitsByMonth = new DictionaryWithDefault<string, int>();
            CommitsByYear = new DictionaryWithDefault<int, int>();
            ActiveDays = new List<DateTime>();
            TotalLines = 0;
            TotalLinesAdded = 0;
            TotalLinesRemoved = 0;
            CommitsByTimezone = new DictionaryWithDefault<string, int>();
            Tags = new DictionaryWithDefault<string, Tag>();
        }

        public DictionaryWithDefault<string, Tag> Tags { get; set; }

        public DictionaryWithDefault<string, Domain> Domains { get; set; }

        public DateTime LastActiveDay { get; set; }

        public DictionaryWithDefault<int, int> CommitsByYear { get; set; }

        public DictionaryWithDefault<int, DictionaryWithDefault<string, int>> AuthorOfYear { get; set; }

        public DictionaryWithDefault<string, int> CommitsByMonth { get; set; }

        public DictionaryWithDefault<string, DictionaryWithDefault<string, int>> AuthorOfMonth { get; set; }

        public DictionaryWithDefault<string, int> ActivityByYearWeek { get; set; }

        public DictionaryWithDefault<int, int> ActivityByMonthOfYear { get; set; }

        public int ActivityByHourOfWeekBusiest { get; set; }

        public DictionaryWithDefault<int, DictionaryWithDefault<int, int>> ActivityByHourOfWeek { get; set; }

        public decimal ActivityByHourOfDayBusiest { get; set; }

        public DictionaryWithDefault<string, int> CommitsByTimezone { get; set; }

        public int TotalCommits { get; set; }

        public DictionaryWithDefault<DateTime, int> FilesByStamp { get; set; }

        public DictionaryWithDefault<string, Extension> Extensions { get; set; }

        public DictionaryWithDefault<DateTime, Change> ChangesByDate { get; set; }

        public DictionaryWithDefault<string, Author> Authors { get; set; }

        public int TotalLines { get; set; }

        public int TotalLinesRemoved { get; set; }

        public int TotalLinesAdded { get; set; }

        public decimal ActivityByYearWeekPeak { get; set; }

        public int TotalAuthors { get; set; }

        public List<DateTime> ActiveDays { get; set; }

        public DictionaryWithDefault<int, int> ActivityByDayOfWeek { get; set; }

        public DictionaryWithDefault<int, decimal> ActivityByHourOfDay { get; set; }

        public DateTime LastCommitStamp { get; set; }

        public DateTime FirstCommitStamp { get; set; }

        public int TotalFiles { get; set; }

        public void Collect(string directory, Configuration configuration)
        {
            _configuration = configuration;
            Dir = directory;
            ProjectName = Path.GetFileName(directory);

            GetTotalAuthors();

            GetTagData();

            var lines = GetActivityDataAndAuthors();

            GetFilesByStampAndTotalCommits(lines);

            GetExtensions();

            GetChangesByDateAndTotalLines();
        }

        private void GetTotalAuthors()
        {
            try
            {
                TotalAuthors = Convert.ToInt32(GitStats.GetPipeOutput(new[]
                {
                    "git shortlog -s",
                    "wc -l"
                }));
            }
            catch (Exception)
            {
                TotalAuthors = 0;
            }
        }

        private void GetTagData()
        {
            string[] parts;
            string output;

            var lines = GitStats.GetPipeOutput(new[]
            {
                "git show-ref --tags"
            }).Split("\n").ToList();
            foreach (var line in lines)
            {
                if (line.Length == 0) continue;
                var split = line.Split(" ");
                var hash = split[0];
                var tag = split[1];

                tag = tag.Replace("refs/tags/", "");
                output = GitStats.GetPipeOutput(new[]
                {
                    string.Format("git log \"%s\" --pretty=format:\"%%at %%an\" -n 1", hash)
                });
                if (output.Length > 0)
                {
                    parts = output.Split(" ");
                    DateTime stamp;
                    try
                    {
                        stamp = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt32(parts[0])).DateTime;
                    }
                    catch (FormatException)
                    {
                        stamp = DateTime.MinValue;
                    }

                    Tags[tag] = new Tag
                    {
                        Stamp = stamp, Hash = hash, Date = stamp,
                        Commits = 0, Authors = new Dictionary<string, int>()
                    };
                }
            }

            // Collect info on tags, starting from latest
            var tagsSortedByDateDesc = Tags.Select(el => (el.Value.Date, el.Key)).OrderBy(p1 => p1).Reverse()
                .Select(el => el.Key);

            foreach (var tag in tagsSortedByDateDesc.Reverse())
            {
                var cmd = string.Format($"git shortlog -s \"{tag}\"");
                output = GitStats.GetPipeOutput(new[] {cmd});
                if (output.Length == 0) continue;
                var prev = tag;
                foreach (var line in output.Split("\n"))
                {
                    parts = Regex.Split(line, "\\s+", RegexOptions.None);
                    var commits = Convert.ToInt32(parts[1]);
                    var author = parts[2];
                    Tags[tag].Commits += commits;
                    Tags[tag].Authors[author] = commits;
                }
            }
        }

        // Collect revision statistics
        // Outputs "<stamp> <date> <time> <timezone> <author> '<' <mail> '>'"
        private List<string> GetActivityDataAndAuthors()
        {
            var lines = GitStats.GetPipeOutput(new[]
            {
                "git rev-list --pretty=format:\"%at %ai %an <%aE>\" HEAD",
                "grep -v ^commit"
            }).Split("\n").ToList();
            foreach (var line in lines)
            {
                var parts = Regex.Split(line, "([01-9-:+]+ )").Where(x => !string.IsNullOrEmpty(x)).Select(s => s.Trim())
                    .ToArray();
                DateTime stamp;
                try
                {
                    stamp = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt32(parts[0])).DateTime;
                }
                catch (FormatException)
                {
                    stamp = DateTime.MinValue;
                }

                var timezone = parts[3];
                var tup1 = parts[4].Split("<");
                var author = tup1[0];
                var mail = tup1[1];
                author = author.TrimEnd();
                mail = mail.TrimEnd('>');
                var domain = "?";
                if (mail.IndexOf("@", StringComparison.CurrentCultureIgnoreCase) != -1) domain = mail.Split("@")[1];
                var date = stamp;
                // First and last commit stamp
                if (LastCommitStamp == DateTime.MinValue) LastCommitStamp = stamp;
                FirstCommitStamp = stamp;
                // activity
                // hour
                var hour = date.Hour;
                ActivityByHourOfDay[hour] = ActivityByHourOfDay[hour] + 1;
                // most active hour?
                if (ActivityByHourOfDay[hour] > ActivityByHourOfDayBusiest)
                    ActivityByHourOfDayBusiest = ActivityByHourOfDay[hour];
                // day of week
                var day = (int) date.DayOfWeek;
                ActivityByDayOfWeek[day] = ActivityByDayOfWeek[day] + 1;
                // domain stats
                if (!Domains.ContainsKey(domain)) Domains[domain] = new Domain();
                // commits
                Domains[domain].Commits = Domains[domain].Commits + 1;
                // hour of week
                if (!ActivityByHourOfWeek.ContainsKey(day))
                    ActivityByHourOfWeek[day] = new DictionaryWithDefault<int, int>();

                ActivityByHourOfWeek[day][hour] = ActivityByHourOfWeek[day][hour] + 1;
                // most active hour?
                if (ActivityByHourOfWeek[day][hour] > ActivityByHourOfWeekBusiest)
                    ActivityByHourOfWeekBusiest = ActivityByHourOfWeek[day][hour];
                // month of year
                var month = date.Month;
                ActivityByMonthOfYear[month] = ActivityByMonthOfYear[month] + 1;
                // yearly/weekly activity
                var yyw =
                    $"{date.Year}-{_calendar.GetWeekOfYear(date, _cultureInfo.DateTimeFormat.CalendarWeekRule, _cultureInfo.DateTimeFormat.FirstDayOfWeek)}";
                ActivityByYearWeek[yyw] = ActivityByYearWeek[yyw] + 1;
                if (ActivityByYearWeekPeak < ActivityByYearWeek[yyw]) ActivityByYearWeekPeak = ActivityByYearWeek[yyw];
                // author stats
                if (!Authors.ContainsKey(author)) Authors[author] = new Author();
                // commits
                if (Authors[author].LastCommitStamp == DateTime.MinValue) Authors[author].LastCommitStamp = stamp;

                Authors[author].FirstCommitStamp = stamp;
                Authors[author].Commits = Authors[author].Commits + 1;
                // author of the month/year
                var yymm = $"{date.Year}-{date.Month:D2}";
                if (AuthorOfMonth.ContainsKey(yymm))
                    AuthorOfMonth[yymm][author] = AuthorOfMonth[yymm][author] + 1;
                else
                    AuthorOfMonth[yymm] = new DictionaryWithDefault<string, int> {[author] = 1};
                CommitsByMonth[yymm] = CommitsByMonth[yymm] + 1;
                var yy = date.Year;
                if (AuthorOfYear.ContainsKey(yy))
                    AuthorOfYear[yy][author] = AuthorOfYear[yy][author] + 1;
                else
                    AuthorOfYear[yy] = new DictionaryWithDefault<string, int> {[author] = 1};
                CommitsByYear[yy] = CommitsByYear[yy] + 1;
                // authors: active days
                var yymmdd = date;
                if (Authors[author].LastActiveDay == DateTime.MinValue)
                {
                    Authors[author].LastActiveDay = yymmdd;
                    Authors[author].ActiveDays = 1;
                }
                else if (yymmdd != Authors[author].LastActiveDay)
                {
                    Authors[author].LastActiveDay = yymmdd;
                    Authors[author].ActiveDays += 1;
                }

                // project: active days
                if (yymmdd != LastActiveDay)
                {
                    LastActiveDay = yymmdd;
                    ActiveDays.Add(yymmdd);
                }

                // timezone
                CommitsByTimezone[timezone] = CommitsByTimezone[timezone] + 1;
            }

            return lines;
        }

        private void GetFilesByStampAndTotalCommits(List<string> lines)
        {
            // TODO Optimize this, it's the worst bottleneck
            // outputs "<stamp> <files>" for each revision
            lines.Clear();
            FilesByStamp = new DictionaryWithDefault<DateTime, int>();
            var revLines = GitStats.GetPipeOutput(new[]
            {
                "git rev-list --pretty=format:\"%at %T\" HEAD",
                "grep -v ^commit"
            }).Trim().Split("\n");
            foreach (var revLine in revLines)
            {
                var tup2 = revLine.Split(" ");
                var time = tup2[0];
                var rev = tup2[1];
                var lineCount = GetFilesInCommit(rev);
                lines.Add($"{Convert.ToInt32(time)} {lineCount}");
            }

            TotalCommits = lines.Count;
            foreach (var line in lines)
            {
                var parts = line.Split(" ");
                if (parts.Length != 2) continue;
                try
                {
                    FilesByStamp[DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt32(parts[0])).DateTime] = Convert.ToInt32(parts[1]);
                }
                catch (FormatException)
                {
                    Console.WriteLine("Warning: failed to parse line \"%s\"", line);
                }
            }
        }

        private void GetExtensions()
        {
            Extensions = new DictionaryWithDefault<string, Extension>();
            var lines = GitStats.GetPipeOutput(new[]
            {
                "git ls-tree -r -z HEAD"
            }).Split("\0").ToList().Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            ;
            TotalFiles = lines.Count();
            foreach (var line in lines)
            {
                if (line.Length == 0) continue;
                var parts = Regex.Split(line, "\\s+", RegexOptions.None);
                var sha1 = parts[2];
                var filename = parts[3];
                string ext;
                if (filename.IndexOf(".", StringComparison.Ordinal) == -1 ||
                    filename.IndexOf(".", StringComparison.Ordinal) == 0)
                    ext = "";
                else
                    ext = filename.Substring(filename.LastIndexOf(".", StringComparison.Ordinal) + 1);
                if (ext.Length > _configuration.MaxExtensionLength) ext = "";
                if (!Extensions.ContainsKey(ext))
                    Extensions[ext] = new Extension();
                Extensions[ext].Files += 1;
                try
                {
                    Extensions[ext].Lines += GetLinesInBlob(sha1);
                }
                catch
                {
                    Console.WriteLine("Warning: Could not count lines for file \"%s\"", line);
                }
            }
        }

        private void GetChangesByDateAndTotalLines()
        {
            // line statistics
            // outputs:
            //  N files changed, N insertions (+), N deletions(-)
            // <stamp> <author>
            ChangesByDate = new DictionaryWithDefault<DateTime, Change>();
            var lines = GitStats.GetPipeOutput(new[] {"git log --shortstat --pretty=format:\"%at %an\""}).Split("\n")
                .ToList();
            lines.Reverse();
            var files = 0;
            var inserted = 0;
            var deleted = 0;
            var totalLines = 0;
            foreach (var line in lines)
            {
                if (line.Length == 0) 
                {
                    files = 0;
                    inserted = 0;
                    deleted = 0;
                    continue;
                }
                if (line.IndexOf(" changed,", StringComparison.CurrentCultureIgnoreCase) == -1)
                {
                    var pos = line.IndexOf(" ", StringComparison.CurrentCultureIgnoreCase);
                    if (pos != -1)
                        try
                        {
                            var datetime = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt32(line.Substring(0,pos))).DateTime;
                            var author = line.Substring(pos + 1);
                            ChangesByDate[datetime] = new Change
                            {
                                Files = files,
                                Inserted = inserted,
                                Deleted = deleted,
                                TotalLines = totalLines
                            };
                            if (!Authors.ContainsKey(author)) Authors[author] = new Author();
                            Authors[author].LinesAdded += inserted;
                            Authors[author].LinesRemoved += deleted;
                        }
                        catch (Exception)
                        {
                            Console.WriteLine($"Warning: unexpected line \"{line}\"");
                        }
                    else
                        Console.WriteLine($"Warning: unexpected line \"{line}\"");
                }
                else
                {
                    files = GetIntFromStartOfRegex(line, "\\d+ file");
                    inserted = GetIntFromStartOfRegex(line, "\\d+ insertion");
                    deleted = GetIntFromStartOfRegex(line, "\\d+ delet");

                    totalLines += inserted;
                    totalLines -= deleted;
                    TotalLinesAdded += inserted;
                    TotalLinesRemoved += deleted;
                }
            }

            TotalLines = totalLines;
        }

        public int GetIntFromStartOfRegex(string line, string regex)
        {
            string output = "0";
            var filesChangedRe = new Regex(regex);
            var fileChangedCollection = filesChangedRe.Matches(line);
            foreach (Match match in fileChangedCollection)
            {
                var groupValue = match.Groups[0].Value;
                output = groupValue.Split(" ")[0];
            }
            return Convert.ToInt32(output);
        }


        public void Refine()
        {
            // authors
            // name -> {place_by_commits, commits_frac, date_first, date_last, timedelta}
            var authorsByCommits = GetKeysSortedByAuthorKey(Authors);
            authorsByCommits = authorsByCommits.Reverse();
            foreach (var tup1 in authorsByCommits.Select((p1, p2) => Tuple.Create(p2, p1)))
            {
                var i = tup1.Item1;
                var name = tup1.Item2;
                Authors[name].PlaceByCommits = i + 1;
            }

            foreach (var name in Authors.Keys)
            {
                var a = Authors[name];
                a.CommitsFrac = 100 * a.Commits / TotalCommits;
                var dateFirst = a.FirstCommitStamp;
                var dateLast = a.LastCommitStamp;
                var delta = dateLast - dateFirst;
                a.DateFirst = dateFirst;
                a.DateLast = dateLast;
                a.TimeDelta = delta;
            }
        }

        public List<DateTime> GetActiveDays()
        {
            return ActiveDays;
        }

        public Dictionary<int, int> GetActivityByDayOfWeek()
        {
            return ActivityByDayOfWeek;
        }

        public Dictionary<int, decimal> GetActivityByHourOfDay()
        {
            return ActivityByHourOfDay;
        }

        public Author GetAuthorInfo(string author)
        {
            return Authors[author];
        }

        public IEnumerable<string> GetAuthors(int configurationMaxAuthors)
        {
            var res = GetKeysSortedByAuthorKey(Authors);
            res = res.Reverse();
            return res.Take(configurationMaxAuthors);
        }

        public long GetCommitDeltaDays()
        {
            var deltaDays = (LastCommitStamp - FirstCommitStamp).Days;
            return deltaDays == 0 ? 1 : deltaDays;
        }

        public Domain GetDomainInfo(string domain)
        {
            return Domains[domain];
        }

        public int GetFilesInCommit(string rev)
        {
            if (Cache.FilesInTree.TryGetValue(rev, out var res)) 
                return res;
            
            res = Convert.ToInt32(GitStats.GetPipeOutput(new[]
            {
                $"git ls-tree -r --name-only \"{rev}\"",
                "wc -l"
            }).Split("\n")[0]);

            Cache.FilesInTree[rev] = res;
            
            return res;
        }

        public int GetLinesInBlob(string sha1)
        {
            if (Cache.LinesInBlob.TryGetValue(sha1, out var res))
                return res;

            res = Convert.ToInt32(GitStats.GetPipeOutput(new[]
            {
                $"git cat-file blob {sha1}",
                "wc -l"
            }, PipingLevel.Minimal).Split()[0]);

            Cache.LinesInBlob[sha1] = res;

            return res;
        }

        public string RevToDate(string rev)
        {
            var stamp = Convert.ToInt32(GitStats.GetPipeOutput(new[]
            {
                $"git log --pretty=format:%%at \"{rev}\" -n 1"
            }));
            return DateTimeOffset.FromUnixTimeSeconds(stamp).DateTime.ToString("%Y-%m-%d");
        }
    }
}