using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using CommandLine;

namespace GitStatistics
{
    public class GitStats
    {
        public static bool OnLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public static TimeSpan ExecTimeInternal;

        public static TimeSpan ExecTimeExternal;

        public static DateTime TimeStart = DateTime.Now;

        public static int Version;

        public static string GetPipeOutput(string[] cmds, PipingLevel pipeLevel = PipingLevel.Full, bool triggerAltWrite = false)
        {
            var start = DateTime.Now;

            var command = string.Join(" | ", cmds);

            // Start the child process.
            var sb = new StringBuilder();

            var p0 = new Process
            {
                StartInfo =
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    FileName = "cmd.exe",
                    Arguments = "/c " + command
                }
            };

            p0.Start();

            sb.Append(p0.StandardOutput.ReadToEnd());
            p0.WaitForExit();
  

            var end = DateTime.Now;
            switch (pipeLevel)
            {
                case PipingLevel.Minimal:
                    Console.Write(".");
                    break;
                case PipingLevel.Full:
                {
                    Console.WriteLine("[{0}] >> {1}", end - start, command);
                    break;
                }
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
            Parser.Default.ParseArguments<Configuration>(argsOrig)
                .WithParsed(o =>
                {
                    var gitPath = Path.GetFullPath(o.RepositoryPath);
                    var outputPath = Path.GetFullPath(o.OutputPath);
                    var runDir = Directory.GetCurrentDirectory();
                    try
                    {
                        Directory.CreateDirectory(outputPath);
                    }
                    catch
                    {
                    }

                    if (!Directory.Exists(outputPath))
                    {
                        Console.WriteLine($"FATAL: Output Path {outputPath} is not a directory or does not exist");
                        Environment.Exit(1);
                    }

                    Console.WriteLine($"Git Path: {gitPath}");
                    Console.WriteLine("Output Path: {0}", outputPath);
                    Directory.SetCurrentDirectory(gitPath);
                    var cacheFile = Path.Join(outputPath, "gitstats.cache");
                    Console.WriteLine("Collecting Data...");
                    var data = new GitDataCollector();
                    data.LoadCache(cacheFile);
                    data.Collect(gitPath, o);
                    Console.WriteLine("Refining Data...");
                    data.SaveCache(cacheFile);
                    data.Refine();
                    Directory.SetCurrentDirectory(runDir);
                    Console.WriteLine("Generating report...");
                    var report = new HtmlReportCreator(o);
                    report.Create(data, outputPath);
                    var timeEnd = DateTime.Now;
                    ExecTimeInternal += timeEnd - TimeStart;
                    
                    Console.WriteLine(
                        $"Execution time {ExecTimeInternal.Seconds} secs, {ExecTimeExternal.Seconds} secs ({(100.0 * ExecTimeExternal / ExecTimeInternal):F2} %) in external commands)");
                });
        }
    }
}