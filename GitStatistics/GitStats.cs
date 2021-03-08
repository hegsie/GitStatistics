using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Gnu.Getopt;

namespace GitStatistics
{
    public class GitStats
    {
        public static bool OnLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public static DateTime ExecTimeInternal;

        public static DateTime ExecTimeExternal;

        public static DateTime TimeStart = DateTime.Now;

        public static Dictionary<string, object> Conf = new Dictionary<string, object>
        {
            {
                "max_domains",
                10
            },
            {
                "max_ext_length",
                10
            },
            {
                "style",
                "gitstats.css"
            },
            {
                "max_authors",
                20
            }
        };

        // dict['author'] = { 'commits': 512 } - ...key(dict, 'commits')

        public static int Version;

        public static string GetPipeOutput(string[] cmds, bool quiet = false)
        {
            var start = DateTime.Now;
            if (!quiet && OnLinux)
            {
                //} && os.isatty(1)) {
                Console.Write(">> " + string.Join(" | ", cmds));
                Console.Out.Flush();
            }


            // Start the child process.

            StringBuilder sb = new StringBuilder();
            foreach (var x in cmds)
            {
                Process p0 = new Process
                {
                    StartInfo =
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        FileName = "cmd.exe", 
                        Arguments = "/c " + x
                    }
                };
                p0.Start();
                sb.Append(p0.StandardOutput.ReadToEnd());
                p0.WaitForExit();
            }

            var end = DateTime.Now;
            if (!quiet)
            {
                if (OnLinux)
                    // && os.isatty(1)) {
                    Console.Write("\r");
                Console.WriteLine("[{0}] >> {1}", end - start, string.Join(" | ", cmds));
            }

            ExecTimeExternal += end - start;
            return sb.ToString().TrimEnd('\n');
        }

        public static int GetVersion()
        {
            if (Version == 0)
                Version = Convert.ToInt32(GetPipeOutput(new[] {"git rev-parse --short HEAD"}).Split(null)[0]);
            return Version;
        }

        public void Run(string[] argsOrig)
        {
            var g = new Getopt("GitStatistics", argsOrig, "c:");
            int c;
            while ((c = g.getopt()) != -1)
            {
                switch (c)
                {
                    case 'c':
                        var tup3 = g.Optarg.Split("=", 1);
                        var key = tup3[0];
                        var value = tup3[1];
                        if (!Conf.Keys.Contains(key))
                            throw new ApplicationException($"Error: no such key {key} in config");
                        if (Conf[key] is int)
                            Conf[key] = Convert.ToInt32(value);
                        else
                            Conf[key] = value;
                        break;

                    case '?':
                        Console.WriteLine(@"Usage: gitstats [options] <gitpath> <outputpath>

                                                    Options:
                                                    -c key=value     Override configuration value

                                                    Default config values:
                                                    {0}
                                                    ", Conf);
                        Environment.Exit(0);
                        break;

                    default:
                        Console.WriteLine("getopt() returned " + c);
                        break;
                }
            }
            
            var gitPath = g.Argv[0];
            var outputPath = Path.GetFullPath(g.Argv[1]);
            var runDir = Directory.GetCurrentDirectory();
            try
            {
                Directory.CreateDirectory(outputPath);
            }
            catch
            { }

            if (!Directory.Exists(outputPath))
            {
                Console.WriteLine("FATAL: Output Path is not a directory or does not exist");
                Environment.Exit(1);
            }

            Console.WriteLine($"Git Path: {gitPath}");
            Console.WriteLine("Output Path: {0}", outputPath);
            Directory.SetCurrentDirectory(gitPath);
            var cacheFile = Path.Join(outputPath, "gitstats.cache");
            Console.WriteLine("Collecting Data...");
            var data = new GitDataCollector();
            data.LoadCache(cacheFile);
            data.Collect(gitPath);
            Console.WriteLine("Refining Data...");
            data.SaveCache(cacheFile);
            data.Refine();
            Directory.SetCurrentDirectory(runDir);
            Console.WriteLine("Generating report...");
            var report = new HtmlReportCreator();
            report.Create(data, outputPath);
            var timeEnd = DateTime.Now;
            var exectimeInternal = timeEnd - TimeStart;
            Console.WriteLine(
                $"Execution time {exectimeInternal} secs, {ExecTimeExternal} secs ({0 /*100.0 *  ExecTimeExternal / exectimeInternal*/} %%) in external commands)");
        }
    }
}