using System;

namespace GitStatistics
{
    class Program
    {
        public static GitStats GitStats = new GitStats();

        public static string GnuPlotCmd = "gnuplot";

        static void Main(string[] args)
        {
            var environmentVariable = Environment.GetEnvironmentVariable("GNUPLOT");
            if (!string.IsNullOrEmpty(environmentVariable))
            {
                GnuPlotCmd = environmentVariable;
            }

            GitStats.Run(args);
        }
    }
}
