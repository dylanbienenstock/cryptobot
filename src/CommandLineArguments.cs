using CommandLine;

namespace CryptoBot
{
    public class CommandLineArguments
    {
        [Option("no-history", HelpText = "Disables fetching historical data")]
        public bool NoHistory { get; set; }

        [Option("record", HelpText = "Records trades to the specified storage directory")]
        public bool Record { get; set; }
    }
}