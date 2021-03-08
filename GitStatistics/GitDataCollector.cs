using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace GitStatistics
{
    public class GitDataCollector : DataCollector
    {
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

        public int ActivityByHourOfDayBusiest { get; set; }

        public DictionaryWithDefault<string, int> CommitsByTimezone { get; set; }

        public int TotalCommits { get; set; }

        public DictionaryWithDefault<long, object> FilesByStamp { get; set; }

        public DictionaryWithDefault<string, DictionaryWithDefault<string, int>> Extensions { get; set; }

        public DictionaryWithDefault<DateTime, Change> ChangesByDate { get; set; }

        public DictionaryWithDefault<string, Author> Authors { get; set; }

        public int TotalLines { get; set; }

        public int TotalLinesRemoved { get; set; }

        public int TotalLinesAdded { get; set; }

        public int ActivityByYearWeekPeak { get; set; }

        public int TotalAuthors { get; set; }

        public List<DateTime> ActiveDays { get; set; }

        public DictionaryWithDefault<int, int> ActivityByDayOfWeek { get; set; }

        public DictionaryWithDefault<int, int> ActivityByHourOfDay { get; set; }

        public DateTime LastCommitStamp { get; set; }

        public DateTime FirstCommitStamp { get; set; }

        public int TotalFiles { get; set; }

        public void Collect(string directory)
        {
            Dir = directory;
            ProjectName = Path.GetFileName(directory); //os.Path.basename(Path.GetFullPath(directory));

            string ext;
            string author;
            DateTime stamp;
            string[] parts;
            string output;

            try
            {
                TotalAuthors = Convert.ToInt32(GitStats.GetPipeOutput(new[]
                {
                    "git shortlog -s",
                    "wc -l"
                }));
            }
            catch (Exception e)
            {
                TotalAuthors = 0;
            }


            // Gets the Calendar instance associated with a CultureInfo.
            CultureInfo myCI = new CultureInfo("en-US");
            Calendar myCal = myCI.Calendar;

            ActivityByHourOfDay = new DictionaryWithDefault<int, int>();
            ActivityByDayOfWeek = new DictionaryWithDefault<int, int>();
            ActivityByMonthOfYear = new DictionaryWithDefault<int, int>();
            ActivityByHourOfWeek = new DictionaryWithDefault<int, DictionaryWithDefault<int, int>>();
            ActivityByHourOfDayBusiest = 0;
            ActivityByHourOfWeekBusiest = 0;
            ActivityByYearWeek = new DictionaryWithDefault<string, int>();
            ActivityByYearWeekPeak = 0;
            Authors = new DictionaryWithDefault<string, Author>();
            // domains
            Domains = new DictionaryWithDefault<string, Domain>();
            // author of the month
            AuthorOfMonth = new DictionaryWithDefault<string, DictionaryWithDefault<string, int>>();
            AuthorOfYear = new DictionaryWithDefault<int, DictionaryWithDefault<string, int>>();
            CommitsByMonth = new DictionaryWithDefault<string, int>();
            CommitsByYear = new DictionaryWithDefault<int, int>();
            ActiveDays = new List<DateTime>();
            // lines
            TotalLines = 0;
            TotalLinesAdded = 0;
            TotalLinesRemoved = 0;
            // timezone
            CommitsByTimezone = new DictionaryWithDefault<string, int>();
            // tags
            Tags = new DictionaryWithDefault<string, Tag>();
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
            // var tagsSortedByDateDesc = map(el => el[1], reversed(map(el => (el[1]["date"], el[0]), this.Tags.items()).OrderBy(p1 => p1).ToList()));
            var tagsSortedByDateDesc = Tags.Select(el => (el.Value.Date, el.Key)).OrderBy(p1 => p1).Reverse()
                .Select(el => el.Key);

            foreach (var tag in tagsSortedByDateDesc.Reverse())
            {
                var cmd = string.Format($"git shortlog -s \"{tag}\"");
                //if (prev != null)
                //{
                //    cmd += string.Format(" \"^%s\"", prev);
                //}
                output = GitStats.GetPipeOutput(new[] {cmd});
                if (output.Length == 0) continue;
                var prev = tag;
                foreach (var line in output.Split("\n"))
                {
                    parts = Regex.Split(line, "\\s+", RegexOptions.None);
                    var commits = Convert.ToInt32(parts[1]);
                    author = parts[2];
                    Tags[tag].Commits += commits;
                    Tags[tag].Authors[author] = commits;
                }
            }

            // Collect revision statistics
            // Outputs "<stamp> <date> <time> <timezone> <author> '<' <mail> '>'"
            lines = GitStats.GetPipeOutput(new[]
            {
                "git rev-list --pretty=format:\"%at %ai %an <%aE>\" HEAD",
                "grep -v ^commit"
            }).Split("\n").ToList();
            foreach (var line in lines)
            {
                parts = Regex.Split(line, "([01-9-:+]+ )").Where(x => !string.IsNullOrEmpty(x)).Select(s => s.Trim())
                    .ToArray();
                //parts = line.Split(" ", 4);
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
                author = tup1[0];
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
                    $"{date.Year}-{myCal.GetWeekOfYear(date, myCI.DateTimeFormat.CalendarWeekRule, myCI.DateTimeFormat.FirstDayOfWeek)}";
                ActivityByYearWeek[yyw] = ActivityByYearWeek[yyw] + 1;
                if (ActivityByYearWeekPeak < ActivityByYearWeek[yyw]) ActivityByYearWeekPeak = ActivityByYearWeek[yyw];
                // author stats
                if (!Authors.ContainsKey(author)) Authors[author] = new Author();
                // commits
                if (Authors[author].LastCommitStamp == DateTime.MinValue) Authors[author].LastCommitStamp = stamp;

                Authors[author].FirstCommitStamp = stamp;
                Authors[author].Commits = Authors[author].Commits + 1;
                // author of the month/year
                var yymm = $"{date.Year}-{date.Month}";
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

            // TODO Optimize this, it's the worst bottleneck
            // outputs "<stamp> <files>" for each revision
            FilesByStamp = new DictionaryWithDefault<long, object>();
            var revlines = GitStats.GetPipeOutput(new[]
            {
                "git rev-list --pretty=format:\"%at %T\" HEAD",
                "grep -v ^commit"
            }).Trim().Split("\n");
            foreach (var revline in revlines)
            {
                var tup2 = revline.Split(" ");
                var time = tup2[0];
                var rev = tup2[1];
                var linecount = GetFilesInCommit(rev);
                lines.Add($"{Convert.ToInt32(time)} {linecount}");
            }

            TotalCommits = lines.Count;
            foreach (var line in lines)
            {
                parts = line.Split(" ");
                if (parts.Length != 2) continue;
                try
                {
                    FilesByStamp[Convert.ToInt32(parts[0])] = Convert.ToInt32(parts[1]);
                }
                catch (FormatException)
                {
                    Console.WriteLine("Warning: failed to parse line \"%s\"", line);
                }
            }

            // extensions
            Extensions = new DictionaryWithDefault<string, DictionaryWithDefault<string, int>>();
            lines = GitStats.GetPipeOutput(new[]
            {
                "git ls-tree -r -z HEAD"
            }).Split("\000").ToList();
            ;
            TotalFiles = lines.Count();
            foreach (var line in lines)
            {
                if (line.Length == 0) continue;
                parts = Regex.Split(line, "\\s+", RegexOptions.None);
                var sha1 = parts[2];
                var filename = parts[3];
                if (filename.IndexOf(".", StringComparison.Ordinal) == -1 ||
                    filename.IndexOf(".", StringComparison.Ordinal) == 0)
                    ext = "";
                else
                    ext = filename.Substring(filename.IndexOf(".", StringComparison.Ordinal) + 1);
                if (ext.Length > (int) GitStats.Conf["max_ext_length"]) ext = "";
                if (!Extensions.ContainsKey(ext))
                    Extensions[ext] = new DictionaryWithDefault<string, int>
                    {
                        {
                            "files",
                            0
                        },
                        {
                            "lines",
                            0
                        }
                    };
                Extensions[ext]["files"] += 1;
                try
                {
                    Extensions[ext]["lines"] += GetLinesInBlob(sha1);
                }
                catch
                {
                    Console.WriteLine("Warning: Could not count lines for file \"%s\"", line);
                }
            }

            // line statistics
            // outputs:
            //  N files changed, N insertions (+), N deletions(-)
            // <stamp> <author>
            ChangesByDate = new DictionaryWithDefault<DateTime, Change>();
            lines = GitStats.GetPipeOutput(new[] {"git log --shortstat --pretty=format:\"%at %an\""}).Split("\n")
                .ToList();
            lines.Reverse();
            var files = 0;
            var inserted = 0;
            var deleted = 0;
            var totalLines = 0;
            author = null;
            foreach (var line in lines)
            {
                if (line.Length == 0) continue;
                // <stamp> <author>
                if (line.IndexOf(" changed,", StringComparison.CurrentCultureIgnoreCase) == -1)
                {
                    var pos = line.IndexOf(" ", StringComparison.CurrentCultureIgnoreCase);
                    if (pos != -1)
                        try
                        {
                            var datetime = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt32(line[0])).DateTime;
                            author = line.Substring(pos + 1);
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
                    var linesAdded = GetIntFromStartOfRegex(line, "\\d+ insertion");
                    var linesDeleted = GetIntFromStartOfRegex(line, "\\d+ delet");

                    totalLines += linesAdded;
                    totalLines -= linesDeleted;
                    TotalLinesAdded += linesAdded;
                    TotalLinesRemoved += linesDeleted;
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
                a.CommitsFrac = 100 * a.Commits / GetTotalCommits();
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

        public Dictionary<int, int> GetActivityByHourOfDay()
        {
            return ActivityByHourOfDay;
        }

        public Author GetAuthorInfo(string author)
        {
            return Authors[author];
        }

        public IEnumerable<string> GetAuthors()
        {
            var res = GetKeysSortedByAuthorKey(Authors);
            res = res.Reverse();
            return res;
        }

        public long GetCommitDeltaDays()
        {
            return (LastCommitStamp - FirstCommitStamp).Days;
        }

        public Domain GetDomainInfo(string domain)
        {
            return Domains[domain];
        }

        public Dictionary<string, Domain>.KeyCollection GetDomains()
        {
            return Domains.Keys;
        }

        public int GetFilesInCommit(string rev)
        {
            int res;
            try
            {
                res = Cache["files_in_tree"][rev];
            }
            catch
            {
                res = Convert.ToInt32(GitStats.GetPipeOutput(new[]
                {
                    $"git ls-tree -r --name-only \"{rev}\"",
                    "wc -l"
                }).Split("\n")[0]);
                if (!Cache.ContainsKey("files_in_tree")) Cache["files_in_tree"] = new Dictionary<string, int>();
                Cache["files_in_tree"][rev] = res;
            }

            return res;
        }

        public DateTime GetFirstCommitDate()
        {
            return FirstCommitStamp;
        }

        public DateTime GetLastCommitDate()
        {
            return LastCommitStamp;
        }

        public int GetLinesInBlob(string sha1)
        {
            int res;
            try
            {
                res = Cache["lines_in_blob"][sha1];
            }
            catch
            {
                res = Convert.ToInt32(GitStats.GetPipeOutput(new[]
                {
                    $"git cat-file blob {sha1}",
                    "wc -l"
                }).Split()[0]);
                if (!Cache.ContainsKey("lines_in_blob")) Cache["lines_in_blob"] = new Dictionary<string, int>();
                Cache["lines_in_blob"][sha1] = res;
            }

            return res;
        }

        public string[] GetTags()
        {
            var lines = GitStats.GetPipeOutput(new[]
            {
                "git show-ref --tags",
                "cut -d/ -f3"
            });
            return lines.Split("\n");
        }

        public string GetTagDate(string tag)
        {
            return RevToDate("tags/" + tag);
        }

        public object GetTotalAuthors()
        {
            return TotalAuthors;
        }

        public int GetTotalCommits()
        {
            return TotalCommits;
        }

        public int GetTotalFiles()
        {
            return TotalFiles;
        }

        public int GetTotalLoc()
        {
            return TotalLines;
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