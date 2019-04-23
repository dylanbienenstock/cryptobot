using System.Collections.Generic;

namespace CryptoBot.TradingStategyLanguage
{
    public static class TSLTemplate
    {
        public static string Indentation = "        ";
        public static string Declaration = "private ";
        public static string EndStatement = ";\n";

        public static string FileStart = 
@"using CryptoBot.Exchanges;
using CryptoBot.Metrics;

namespace CryptoBot.TradingStrategies
{
    public class %title%
    {
        public static long Timescale = %timescale%;
";

        public static string FileEnd = 
@"    }
}";

        public static Dictionary<string, string> metrics = 
            new Dictionary<string, string>()
            {
                { "ATR#",   "AverageTrueRange" },
                { "RENKO", "Renko" },
            };

        public static string GetMetricClass(TSLToken token)
        {
            if (metrics.ContainsKey(token.Value))
                return metrics[token.Value];

            if (metrics.ContainsKey(token.Value + "#"))
                return metrics[token.Value + "#"];

            return null;
        }

        public static string GetMetricName(TSLToken token, TSLToken nextToken)
        {
            if (metrics.ContainsKey(token.Value))
                return "_" + token.Value.ToLower();

            if (metrics.ContainsKey(token.Value + "#"))
                return "_" + token.Value.ToLower() + nextToken.Value;

            return null;
        }
    }
}