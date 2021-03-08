using CommandLine;

namespace GitStatistics
{
    public class Configuration
    {
        public Configuration()
        {
            MaxDomains = 10;
            MaxExtensionLength = 10;
            Style = "gitstats.css";
            MaxAuthors = 20;
        }

        [Option('r', "RepositoryPath", Required = true, HelpText = "Set repository Path.")]
        public string RepositoryPath { get; set; }

        [Option('o', "RepositoryPath", Required = true, HelpText = "Set output Path.")]
        public string OutputPath { get; set; }

        [Option('d', "MaxDomains", Required = false, HelpText = "Set the maximum number of domains to collect.")]
        public int MaxDomains { get; set; }

        [Option('e', "MaxExtensionLength", Required = false, HelpText = "Set teh maximum length of extensions.")]
        public int MaxExtensionLength { get; set; }

        [Option('s', "Style", Required = false, HelpText = "Set the css style file.")]
        public string Style { get; set; }

        [Option('a', "MaxAuthors", Required = false, HelpText = "Set the maximum authors.")]
        public int MaxAuthors { get; set; }
    }
}
