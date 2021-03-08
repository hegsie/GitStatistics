using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GlobExpressions;

namespace GitStatistics
{
    public static class MyListExtensions
    {
        public static IEnumerable<T> GetNth<T>(this List<T> list, int n)
        {
            for (var i = 0; i < list.Count; i += n)
                yield return list[i];
        }
    }

    public class HtmlReportCreator : ReportCreator
    {
        public static object ImageType = "svg";

        public static Dictionary<object, object> GnuplotImageSpecifications = new Dictionary<object, object>
        {
            {
                "svg",
                ""
            },
            {
                "png",
                "transparent"
            }
        };

        public static object GnuplotCommon =
            $"set terminal {ImageType} {GnuplotImageSpecifications[ImageType]}\nset size 1.0,0.5\n";

        private string _title;

        private readonly int version = 1;
        private Configuration _configuration;

        public HtmlReportCreator(Configuration configuration)
        {
            _configuration = configuration;
        }

        public static string html_linkify(string text)
        {
            return text.ToLower().Replace(" ", "_");
        }

        public static object html_header(object level, string text)
        {
            var name = html_linkify(text);
            return $"\n<h{level}><a href=\"#{name}\" name=\"{name}\">{text}</a></h{level}>\n\n";
        }

        public void Create(GitDataCollector data, string path)
        {
            object next;
            string[] authors;
            object r;
            int commits;
            Data = data;
            Path = path;

            _title = data.ProjectName;
            // copy static files. Looks in the binary directory, ../share/gitstats and /usr/share/gitstats
            var binaryPath = Directory.GetCurrentDirectory();
            var secondaryPath = System.IO.Path.Join(binaryPath, "..", "share", "gitstats");
            var baseDirs = new List<object>
            {
                binaryPath,
                secondaryPath,
                "/usr/share/gitstats"
            };
            foreach (var file in new List<string>
                {"gitstats.css", "sortable.js", "arrow-up.gif", "arrow-down.gif", "arrow-none.gif"})
            foreach (var @base in baseDirs)
            {
                var src = @base + "\\" + file;
                if (File.Exists(src))
                {
                    File.Copy(src, path + "\\" + file, true);
                    break;
                }
            }

            var f = new StreamWriter(path + "\\index.html");
            var format = "yyyy-MM-dd H:mm:ss";
            PrintHeader(f);
            f.Write($"<h1>GitStatistics - {data.ProjectName}</h1>");
            PrintNav(f);
            f.Write("<dl>");
            f.Write($"<dt>Project name</dt><dd>{data.ProjectName}</dd>");
            var runTime = DateTime.Now - data.GetStampCreated();
            f.Write(
                $"<dt>Generated</dt><dd>{DateTime.Now.ToString(format)} (in {runTime.Seconds:f2} seconds)</dd>");
            f.Write(
                "<dt>Generator</dt><dd><a href=\"https://github.com/hegsie/GitStatistics\">GitStatistics</a> (version {0})</dd>",
                version);
            f.Write($"<dt>Report Period</dt><dd>{data.GetFirstCommitDate().ToString(format)} to {data.GetLastCommitDate().ToString(format)}</dd>");
            f.Write("<dt>Age</dt><dd>{0} days, {1} active days ({2:f2}%)</dd>", data.GetCommitDeltaDays(),
                data.GetActiveDays().Count, 100.0 * data.GetActiveDays().Count / data.GetCommitDeltaDays());
            f.Write($"<dt>Total Files</dt><dd>{data.GetTotalFiles()}</dd>");
            f.Write("<dt>Total Lines of Code</dt><dd>{0} ({1} added, {2} removed)</dd>", data.TotalLines,
                data.TotalLinesAdded, data.TotalLinesRemoved);
            f.Write("<dt>Total Commits</dt><dd>{0} (average {1:F1} commits per active day, {2:F1} per all days)</dd>",
                data.GetTotalCommits(), data.GetTotalCommits() / data.GetActiveDays().Count,
                data.GetTotalCommits() / data.GetCommitDeltaDays());
            f.Write($"<dt>Authors</dt><dd>{data.TotalAuthors}</dd>");
            f.Write("</dl>");
            f.Write("</body>\n</html>");
            f.Close();
            //##
            // Activity
            f = new StreamWriter(path + "\\activity.html");
            PrintHeader(f);
            f.Write("<h1>Activity</h1>");
            PrintNav(f);
            //f.Write('<h2>Last 30 days</h2>')
            //f.Write('<h2>Last 12 months</h2>')
            // Weekly activity
            const int weeks = 32;
            f.Write(html_header(2, "Weekly activity"));
            f.Write("<p>Last {0} weeks</p>", weeks);
            // generate weeks to show (previous N weeks from now)
            var now = DateTime.Now;
            var weeksList = new List<string>();
            var stampCur = now;
            CultureInfo cultureInfo = new CultureInfo("en-US");
            Calendar calendar = cultureInfo.Calendar;

            foreach (var i in Enumerable.Range(0, weeks - 0))
            {
                var weekOfYear = calendar.GetWeekOfYear(stampCur, cultureInfo.DateTimeFormat.CalendarWeekRule,
                    cultureInfo.DateTimeFormat.FirstDayOfWeek);
                weeksList.Insert(0,
                    stampCur.ToString(
                        $"yyyy-{weekOfYear}"));
                stampCur = stampCur.AddDays(-7);
            }

            // top row: commits & bar
            f.Write("<table class=\"noborders\"><tr>");
            foreach (var i in Enumerable.Range(0, weeks - 0))
            {
                commits = 0;
                if (data.ActivityByYearWeek.ContainsKey(weeksList[i])) 
                    commits = data.ActivityByYearWeek[weeksList[i]];

                decimal percentage = 0;
                if (data.ActivityByYearWeek.ContainsKey(weeksList[i]))
                    percentage = data.ActivityByYearWeek[weeksList[i]] / data.ActivityByYearWeekPeak;

                var height = Math.Max(1, Convert.ToInt32(200 * percentage));
                f.Write(
                    "<td style=\"text-align: center; vertical-align: bottom\">{0}<div style=\"display: block; background-color: red; width: 20px; height: {1}px\"></div></td>",
                    commits, height);
            }

            // bottom row: year/week
            f.Write("</tr><tr>");
            foreach (var i in Enumerable.Range(0, weeks - 0)) f.Write("<td>{0}</td>", weeks - i);
            f.Write("</tr></table>");
            // Hour of Day
            f.Write(html_header(2, "Hour of Day"));
            var hourOfDay = data.GetActivityByHourOfDay();
            f.Write("<table><tr><th>Hour</th>");
            foreach (var i in Enumerable.Range(0, 24 - 0)) f.Write("<th>{0}</th>", i);
            f.Write("</tr>\n<tr><th>Commits</th>");
            var fp = new StreamWriter(path + "\\hour_of_day.dat");
            foreach (var i in Enumerable.Range(0, 24 - 0))
                if (hourOfDay.ContainsKey(i))
                {
                    r = 127 + Convert.ToInt32(hourOfDay[i] / data.ActivityByHourOfDayBusiest * 128);
                    f.Write("<td style=\"background-color: rgb({0}, 0, 0)\">{1}</td>", r, hourOfDay[i]);
                    fp.Write("{0} {1}\n", i, hourOfDay[i]);
                }
                else
                {
                    f.Write("<td>0</td>");
                    fp.Write("{0} 0\n", i);
                }

            fp.Close();
            f.Write("</tr>\n<tr><th>%</th>");
            var totalCommits = data.GetTotalCommits();
            foreach (var i in Enumerable.Range(0, 24 - 0))
                if (hourOfDay.ContainsKey(i))
                {
                    r = 127 + Convert.ToInt32(hourOfDay[i] / data.ActivityByHourOfDayBusiest * 128);
                    f.Write("<td style=\"background-color: rgb({0}, 0, 0)\">{1:F2}</td>", r,
                        100 * hourOfDay[i] / totalCommits);
                }
                else
                {
                    f.Write("<td>0.00</td>");
                }

            f.Write("</tr></table>");
            f.Write($"<img src=\"hour_of_day.{ImageType}\" alt=\"Hour of Day\" />");
            var fg = new StreamWriter(path + "\\hour_of_day.dat");
            foreach (var i in Enumerable.Range(0, 24 - 0))
                fg.Write(hourOfDay.ContainsKey(i) ? $"{i + 1} {hourOfDay[i]}\n" : $"{i + 1} 0\n");
            fg.Close();
            // Day of Week
            f.Write(html_header(2, "Day of Week"));
            var dayOfWeek = data.GetActivityByDayOfWeek();
            f.Write("<div class=\"vtable\"><table>");
            f.Write("<tr><th>Day</th><th>Total (%)</th></tr>");
            fp = new StreamWriter(path + "\\day_of_week.dat");
            foreach (var d in Enumerable.Range(0, 7 - 0))
            {
                commits = 0;
                if (dayOfWeek.ContainsKey(d)) commits = dayOfWeek[d];
                fp.Write("{0} {1}\n", d + 1, commits);
                f.Write("<tr>");
                f.Write("<th>{0}</th>", d + 1);
                if (dayOfWeek.ContainsKey(d))
                    f.Write("<td>{0} ({1:F2}%)</td>", dayOfWeek[d], 100.0 * dayOfWeek[d] / totalCommits);
                else
                    f.Write("<td>0</td>");
                f.Write("</tr>");
            }

            f.Write("</table></div>");
            f.Write($"<img src=\"day_of_week.{ImageType}\" alt=\"Day of Week\" />");
            fp.Close();
            // Hour of Week
            f.Write(html_header(2, "Hour of Week"));
            f.Write("<table>");
            f.Write("<tr><th>Weekday</th>");
            foreach (var hour in Enumerable.Range(0, 24 - 0)) f.Write("<th>{0}</th>", hour);
            f.Write("</tr>");
            foreach (var weekday in Enumerable.Range(0, 7 - 0))
            {
                f.Write("<tr><th>{0}</th>", weekday + 1);
                foreach (var hour in Enumerable.Range(0, 24 - 0))
                {
                    commits = 0;
                    if (data.ActivityByHourOfWeek.TryGetValue(weekday, out var weekdayDict))
                        if (weekdayDict.TryGetValue(hour, out var hourOutput))
                            commits = hourOutput;

                    if (commits != 0)
                    {
                        f.Write("<td");
                        r = 127 + Convert.ToInt32(commits / data.ActivityByHourOfWeekBusiest * 128);
                        f.Write(" style=\"background-color: rgb({0}, 0, 0)\"", r);
                        f.Write(">{0}</td>", commits);
                    }
                    else
                    {
                        f.Write("<td></td>");
                    }
                }

                f.Write("</tr>");
            }

            f.Write("</table>");
            // Month of Year
            f.Write(html_header(2, "Month of Year"));
            f.Write("<div class=\"vtable\"><table>");
            f.Write("<tr><th>Month</th><th>Commits (%)</th></tr>");
            fp = new StreamWriter(path + "\\month_of_year.dat");
            foreach (var mm in Enumerable.Range(1, 13 - 1))
            {
                commits = 0;
                if (data.ActivityByMonthOfYear.ContainsKey(mm)) commits = data.ActivityByMonthOfYear[mm];
                f.Write("<tr><td>{0}</td><td>{1} ({2:F2} %)</td></tr>", mm, commits,
                    100.0 * commits / data.GetTotalCommits());
                fp.Write("{0} {1}\n", mm, commits);
            }

            fp.Close();
            f.Write("</table></div>");
            f.Write($"<img src=\"month_of_year.{ImageType}\" alt=\"Month of Year\" />");
            // Commits by year/month
            f.Write(html_header(2, "Commits by year/month"));
            f.Write("<div class=\"vtable\"><table><tr><th>Month</th><th>Commits</th></tr>");
            foreach (var yymm in data.CommitsByMonth.Keys.OrderBy(p1 => p1).Reverse().ToList())
                f.Write("<tr><td>{0}</td><td>{1}</td></tr>", yymm, data.CommitsByMonth[yymm]);
            f.Write("</table></div>");
            f.Write($"<img src=\"commits_by_year_month.{ImageType}\" alt=\"Commits by year/month\" />");
            fg = new StreamWriter(path + "\\commits_by_year_month.dat");
            foreach (var yymm in data.CommitsByMonth.Keys.OrderBy(p2 => p2).ToList())
                fg.Write("{0} {1}s\n", yymm, data.CommitsByMonth[yymm]);
            fg.Close();
            // Commits by year
            f.Write(html_header(2, "Commits by Year"));
            f.Write("<div class=\"vtable\"><table><tr><th>Year</th><th>Commits (% of all)</th></tr>");
            foreach (var yy in data.CommitsByYear.Keys.OrderBy(p3 => p3).Reverse().ToList())
                f.Write("<tr><td>{0}</td><td>{1} ({2:F2}%)</td></tr>", yy, data.CommitsByYear[yy],
                    100.0 * data.CommitsByYear[yy] / data.GetTotalCommits());
            f.Write("</table></div>");
            f.Write($"<img src=\"commits_by_year.{ImageType}\" alt=\"Commits by Year\" />");
            fg = new StreamWriter(path + "\\commits_by_year.dat");
            foreach (var yy in data.CommitsByYear.Keys.OrderBy(p4 => p4).ToList())
                fg.Write($"{yy} {data.CommitsByYear[yy]}\n");
            fg.Close();
            // Commits by timezone
            f.Write(html_header(2, "Commits by Timezone"));
            f.Write("<table><tr>");
            f.Write("<th>Timezone</th><th>Commits</th>");
            var maxCommitsOnTz = data.CommitsByTimezone.Values.Max();
            foreach (var i in data.CommitsByTimezone.Keys.OrderBy(Convert.ToInt32).ToList())
            {
                commits = data.CommitsByTimezone[i];
                r = 127 + Convert.ToInt32(commits / maxCommitsOnTz * 128);
                f.Write("<tr><th>{0}</th><td style=\"background-color: rgb({1}, 0, 0)\">{2}</td></tr>", i, r, commits);
            }

            f.Write("</tr></table>");
            f.Write("</body></html>");
            f.Close();
            //##
            // Authors
            f = new StreamWriter(path + "\\authors.html");
            PrintHeader(f);
            f.Write("<h1>Authors</h1>");
            PrintNav(f);
            // Authors :: List of authors
            f.Write(html_header(2, "List of Authors"));
            f.Write("<table class=\"authors sortable\" id=\"authors\">");
            f.Write(
                "<tr><th>Author</th><th>Commits (%)</th><th>+ lines</th><th>- lines</th><th>First commit</th><th>Last commit</th><th class=\"unsortable\">Age</th><th>Active days</th><th># by commits</th></tr>");
            foreach (var author in data.GetAuthors(_configuration.MaxAuthors))
            {
                var info = data.GetAuthorInfo(author);
                f.Write(
                    "<tr><td>{0}</td><td>{1} ({2}%)</td><td>{3}</td><td>{4}</td><td>{5:yyyy-MM-dd}</td><td>{6:yyyy-MM-dd}</td><td>{7}</td><td>{8}</td><td>{9}</td></tr>",
                    author, info.Commits, info.CommitsFrac, info.LinesAdded, info.LinesRemoved,
                    info.DateFirst, info.DateLast, info.TimeDelta, info.ActiveDays,
                    info.PlaceByCommits);
            }

            f.Write("</table>");
            var allAuthors = data.GetAuthors(_configuration.MaxAuthors).ToArray();
            if (allAuthors.Count() > _configuration.MaxAuthors)
            {
                var rest = allAuthors.Take(_configuration.MaxAuthors);
                f.Write("<p class=\"moreauthors\">These didn\'t make it to the top: {0}</p>", string.Join(", ", rest));
            }

            // Authors :: Author of Month
            f.Write(html_header(2, "Author of Month"));
            f.Write("<table class=\"sortable\" id=\"aom\">");
            f.Write(
                "<tr><th>Month</th><th>Author</th><th>Commits (%)</th><th class=\"unsortable\">Next top 5</th></tr>");
            var authMonthRev = data.AuthorOfMonth.Keys.OrderBy(p6 => p6).Reverse().ToList();
            foreach (var yymm in authMonthRev)
            {
                var authordict = data.AuthorOfMonth[yymm];
                authors = authordict.OrderByDescending(pair => pair.Value).Select(a => a.Key).ToArray();
                commits = data.AuthorOfMonth[yymm][authors[0]];
                var top5 = new List<string>(authors.ToList());
                top5.RemoveAt(0);
                next = string.Join(", ", top5.Take(5));
                f.Write("<tr><td>{0}</td><td>{1}</td><td>{2} ({3:F2}% of {4})</td><td>{5}</td></tr>", yymm, authors[0],
                    commits, 100.0 * commits / data.CommitsByMonth[yymm], data.CommitsByMonth[yymm], next);
            }

            f.Write("</table>");
            f.Write(html_header(2, "Author of Year"));
            f.Write(
                "<table class=\"sortable\" id=\"aoy\"><tr><th>Year</th><th>Author</th><th>Commits (%)</th><th class=\"unsortable\">Next top 5</th></tr>");
            foreach (var yy in data.AuthorOfYear.Keys.OrderBy(p7 => p7).Reverse().ToList())
            {
                var authordict = data.AuthorOfYear[yy];
                authors = authordict.OrderByDescending(pair => pair.Value).Select(a => a.Key).ToArray();
                commits = data.AuthorOfYear[yy][authors[0]];
                var top5 = new List<string>(authors.ToList());
                top5.RemoveAt(0);
                next = string.Join(", ", top5.Take(5));
                f.Write("<tr><td>{0}</td><td>{1}</td><td>{2} ({3:F2}% of {4})</td><td>{5}</td></tr>", yy, authors[0],
                    commits, 100.0 * commits / data.CommitsByYear[yy], data.CommitsByYear[yy], next);
            }

            f.Write("</table>");
            // Domains
            f.Write(html_header(2, "Commits by Domains"));
            var domainsByCommits = data.Domains.Keys.Select(outerKey => (data.Domains[outerKey].Commits, outerKey))
                .OrderBy(p1 => p1).Select(tuple => tuple.outerKey);
            domainsByCommits.Reverse();
            f.Write("<div class=\"vtable\"><table>");
            f.Write("<tr><th>Domains</th><th>Total (%)</th></tr>");
            fp = new StreamWriter(path + "\\domains.dat");
            var n = 0;
            foreach (var domain in domainsByCommits)
            {
                if (n == _configuration.MaxDomains) break;
                commits = 0;
                n += 1;
                var info = data.GetDomainInfo(domain);
                fp.Write("{0} {1} {2}\n", domain, n, info.Commits);
                f.Write("<tr><th>{0}</th><td>{1} ({2:F2}%)</td></tr>", domain, info.Commits,
                    100.0 * info.Commits / totalCommits);
            }

            f.Write("</table></div>");
            f.Write($"<img src=\"domains.{ImageType}\" alt=\"Commits by Domains\" />");
            fp.Close();
            f.Write("</body></html>");
            f.Close();
            //##
            // Files
            f = new StreamWriter(path + "\\files.html");
            PrintHeader(f);
            f.Write("<h1>Files</h1>");
            PrintNav(f);
            f.Write("<dl>\n");
            f.Write($"<dt>Total files</dt><dd>{data.GetTotalFiles()}</dd>");
            f.Write($"<dt>Total lines</dt><dd>{data.TotalLines}</dd>");
            f.Write("<dt>Average file size</dt><dd>{0} bytes</dd>", 100.0 * data.TotalLines / data.GetTotalFiles());
            f.Write("</dl>\n");
            // Files :: File count by date
            f.Write(html_header(2, "File count by date"));
            // use set to get rid of duplicate/unnecessary entries
            var filesByDate = new List<string>();
            foreach (var stamp in data.FilesByStamp.Keys.OrderBy(p8 => p8).ToList())
                filesByDate.Add(
                    $"{stamp:yyyy-MM-dd} {data.FilesByStamp[stamp]}");
            fg = new StreamWriter(path + "\\files_by_date.dat");
            foreach (var line in filesByDate.ToList().OrderBy(p9 => p9).ToList()) 
                fg.Write($"{line}\n");
            
            fg.Close();
            f.Write($"<img src=\"files_by_date.{ImageType}\" alt=\"Files by Date\" />");
            
            f.Write(html_header(2, "Extensions"));
            f.Write(
                "<table class=\"sortable\" id=\"ext\"><tr><th>Extension</th><th>Files (%)</th><th>Lines (%)</th><th>Lines/file</th></tr>");
            foreach (var ext in data.Extensions.Keys.OrderBy(p10 => p10).ToList())
            {
                var files = data.Extensions[ext].Files;
                var lines = data.Extensions[ext].Lines;
                f.Write("<tr><td>{0}</td><td>{1} ({2}%)</td><td>{3} ({4:F2}%)</td><td>{5}</td></tr>", ext, files,
                    100.0 * files / data.GetTotalFiles(), lines,
                    100.0 * lines / data.TotalLines, lines / files);
            }

            f.Write("</table>");
            f.Write("</body></html>");
            f.Close();
            //##
            // Lines
            f = new StreamWriter(path + "\\lines.html");
            PrintHeader(f);
            f.Write("<h1>Lines</h1>");
            PrintNav(f);
            f.Write("<dl>\n");
            f.Write("<dt>Total lines</dt><dd>{0}</dd>", data.TotalLines);
            f.Write("</dl>\n");
            f.Write(html_header(2, "Lines of Code"));
            f.Write($"<img src=\"lines_of_code.{ImageType}\" />");
            fg = new StreamWriter(path + "\\lines_of_code.dat");
            foreach (var stamp in data.ChangesByDate.Keys.OrderBy(p11 => p11).ToList())
                fg.Write($"{(int)(stamp.Subtract(new DateTime(1970, 1, 1))).TotalSeconds} {data.ChangesByDate[stamp].TotalLines}\n");
            fg.Close();
            f.Write("</body></html>");
            f.Close();
            //##
            // tags.html
            f = new StreamWriter(path + "\\tags.html");
            PrintHeader(f);
            f.Write("<h1>Tags</h1>");
            PrintNav(f);
            f.Write("<dl>");
            f.Write("<dt>Total tags</dt><dd>{0}</dd>", data.Tags.Count);
            if (data.Tags.Count > 0)
                f.Write("<dt>Average commits per tag</dt><dd>{0}</dd>", 1.0 * data.GetTotalCommits() / data.Tags.Count);
            f.Write("</dl>");
            f.Write("<table class=\"tags\">");
            f.Write("<tr><th>Name</th><th>Date</th><th>Commits</th><th>Authors</th></tr>");
            // sort the tags by date desc
            var tagsSortedByDateDesc = data.Tags.Select(el => (el.Value.Date, el.Key)).OrderBy(p12 => p12)
                .Reverse().Select(el => el.Key);
            foreach (var tag in tagsSortedByDateDesc)
            {
                var authorInfo = new List<string>();
                var authorsByCommits = data.Tags[tag].Authors.OrderByDescending(pair => pair.Value).Select(a => a.Key);
                foreach (var i in authorsByCommits.Reverse().ToList())
                    authorInfo.Add($"{i} ({data.Tags[tag].Authors[i]})");

                f.Write("<tr><td>{0}</td><td>{1}</td><td>{2}2</td><td>{3}</td></tr>", tag, data.Tags[tag].Date,
                    data.Tags[tag].Commits, string.Join(", ", authorInfo));
            }

            f.Write("</table>");
            f.Write("</body></html>");
            f.Close();
            CreateGraphs(path);
        }

        public void CreateGraphs(string path)
        {
            Console.WriteLine("Generating graphs...");
            // hour of day
            var f = new StreamWriter(path + "\\hour_of_day.plot");
            f.Write(GnuplotCommon);
            f.Write($@"
set output 'hour_of_day.{ImageType}'
unset key
set xrange [0.5:24.5]
set xtics 4
set grid y
set ylabel ""Commits""
plot 'hour_of_day.dat' using 1:2:(0.5) w boxes fs solid
");
            f.Close();
            // day of week
            f = new StreamWriter(path + "\\day_of_week.plot");
            f.Write(GnuplotCommon);
            f.Write($@"
set output 'day_of_week.{ImageType}'
unset key
set xrange [0.5:7.5]
set xtics 1
set grid y
set ylabel ""Commits""
plot 'day_of_week.dat' using 1:2:(0.5) w boxes fs solid
");
            f.Close();
            // Domains
            f = new StreamWriter(path + "\\domains.plot");
            f.Write(GnuplotCommon);
            f.Write($@"
set output 'domains.{ImageType}'
unset key
unset xtics
set grid y
set ylabel ""Commits""
plot 'domains.dat' using 2:3:(0.5) with boxes fs solid, '' using 2:3:1 with labels rotate by 45 offset 0,1
");
            f.Close();
            // Month of Year
            f = new StreamWriter(path + "\\month_of_year.plot");
            f.Write(GnuplotCommon);
            f.Write($@"
set output 'month_of_year.{ImageType}'
unset key
set xrange [0.5:12.5]
set xtics 1
set grid y
set ylabel ""Commits""
plot 'month_of_year.dat' using 1:2:(0.5) w boxes fs solid
");
            f.Close();
            // commits_by_year_month
            f = new StreamWriter(path + "\\commits_by_year_month.plot");
            f.Write(GnuplotCommon);
            f.Write($@"
set output 'commits_by_year_month.{ImageType}'
unset key
set xdata time
set timefmt ""%Y-%m""
set format x ""%Y-%m""
set xtics rotate by 90 15768000
set bmargin 5
set grid y
set ylabel ""Commits""
plot 'commits_by_year_month.dat' using 1:2:(0.5) w boxes fs solid
");
            f.Close();
            // commits_by_year
            f = new StreamWriter(path + "\\commits_by_year.plot");
            f.Write(GnuplotCommon);
            f.Write($@"
set output 'commits_by_year.{ImageType}'
unset key
set xtics 1 rotate by 90
set grid y
set ylabel ""Commits""
set yrange [0:]
plot 'commits_by_year.dat' using 1:2:(0.5) w boxes fs solid
");
            f.Close();
            // Files by date
            f = new StreamWriter(path + "\\files_by_date.plot");
            f.Write(GnuplotCommon);
            f.Write($@"
set output 'files_by_date.{ImageType}'
unset key
set xdata time
set timefmt ""%Y-%m-%d""
set format x ""%Y-%m-%d""
set grid y
set ylabel ""Files""
set xtics rotate by 90
set ytics autofreq
set bmargin 6
plot 'files_by_date.dat' using 1:2 w steps
");
            f.Close();
            // Lines of Code
            f = new StreamWriter(path + "\\lines_of_code.plot");
            f.Write(GnuplotCommon);
            f.Write($@"
set output 'lines_of_code.{ImageType}'
unset key
set xdata time
set timefmt ""%s""
set format x ""%Y-%m-%d""
set grid y
set ylabel ""Lines""
set xtics rotate by 90
set bmargin 6
plot 'lines_of_code.dat' using 1:2 w lines
");
            f.Close();
            Directory.SetCurrentDirectory(path);
            var matchingFiles = Glob.Files(path, "*.plot").ToArray();
            foreach (var file in matchingFiles)
            {
                var output = GitStats.GetPipeOutput(new[]
                {
                    Program.GnuPlotCmd + $" \"{file}\""
                });
                if (output.Length > 0) Console.WriteLine(output);
            }
        }

        public void PrintHeader(StreamWriter f, string title = "")
        {
            f.Write($@"<?xml version=""1.0"" encoding=""UTF-8""?>
                                    <!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Transitional//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"">
                                    <html xmlns=""http://www.w3.org/1999/xhtml"">
                                    <head>
	                                    <title>GitStats - {this._title}</title>
	                                    <link rel=""stylesheet"" href=""{_configuration.Style}"" type=""text/css"" />
	                                    <meta name=""generator"" content=""GitStatistics {version}"" />
	                                    <script type=""text/javascript"" src=""sortable.js""></script>
                                    </head>
                                    <body>
                                    ");
        }

        public void PrintNav(StreamWriter f)
        {
            f.Write(@"
                    <div class=""nav"">
                    <ul>
                    <li><a href=""index.html"">General</a></li>
                    <li><a href=""activity.html"">Activity</a></li>
                    <li><a href=""authors.html"">Authors</a></li>
                    <li><a href=""files.html"">Files</a></li>
                    <li><a href=""lines.html"">Lines</a></li>
                    <li><a href=""tags.html"">Tags</a></li>
                    </ul>
                    </div>
                    ");
        }
    }
}