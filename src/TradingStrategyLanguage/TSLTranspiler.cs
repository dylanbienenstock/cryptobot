using System;
using System.Collections.Generic;
using System.Linq;

namespace CryptoBot.TradingStategyLanguage
{
    public class TSLTranspiler
    {
        public void Transpile(string tsl)
        {
            Console.WriteLine("\n----INPUT----------------------------------------------------------------\n\n");
            Console.WriteLine(tsl);
            Console.WriteLine("\n\n----TOKENS---------------------------------------------------------------\n\n");

            var tokenizer = new TSLTokenizer();
            var tokens = tokenizer.Tokenize(tsl);
            var globals = new Dictionary<string, string>();

            var csharp = TSLTemplate.FileStart;

            SetGlobalVariables(tokens, globals);
            AddMetrics(tokens, ref csharp);

            csharp += TSLTemplate.FileEnd;
            csharp = RenderTemplate(csharp, globals);

            Console.WriteLine("\n----OUTPUT---------------------------------------------------------------\n");
            Console.WriteLine(csharp);
        }

        private void SetGlobalVariables(List<TSLToken> tokens, Dictionary<string, string> globals)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if (token.Type != TSLTokenType.Assignment) continue;
                if (token.ValueSubType != TSLTokenValueSubType.Global) continue;

                globals[token.Value] = GetTokenValue(tokens[i + 1]);
            }
        }

        private void AddMetrics(List<TSLToken> tokens, ref string csharp)
        {
            var seenMetrics = new List<string>();

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if (token.Type == TSLTokenType.Metric)
                {
                    if (seenMetrics.Contains(token.Value)) continue;

                    csharp += TSLTemplate.Indentation;
                    csharp += TSLTemplate.Declaration;
                    csharp += TSLTemplate.GetMetricClass(token);
                    csharp += " ";
                    csharp += TSLTemplate.GetMetricName(token, tokens[i + 1]);
                    csharp += TSLTemplate.EndStatement;

                    seenMetrics.Add(token.Value);
                }
            }
        }

        private string GetTokenValue(TSLToken token)
        {
            switch (token.ValueType)
            {
                case TSLTokenValueType.Identifier:
                case TSLTokenValueType.String:
                    return token.Value;
                case TSLTokenValueType.Number:
                    float value = float.Parse(token.Value);
                    
                    switch (token.ValueSubType)
                    {
                        case TSLTokenValueSubType.Percentage:
                            value /= 100; 
                            break;
                        case TSLTokenValueSubType.Milliseconds:
                            value *= 1;
                            break;
                        case TSLTokenValueSubType.Seconds:
                            value *= 1000;
                            break; 
                        case TSLTokenValueSubType.Minutes:
                            value *= 1000 * 60;
                            break; 
                        case TSLTokenValueSubType.Hours:
                            value *= 1000 * 60 * 60;
                            break; 
                        case TSLTokenValueSubType.Days:
                            value *= 1000 * 60 * 60 * 24;
                            break;
                    }
                    return value.ToString();
            }

            return token.Value;
        }

        private string RenderTemplate(string csharp, Dictionary<string, string> globals)
        {
            foreach (var globalName in globals.Keys)
                csharp = csharp.Replace($"%{globalName}%", globals[globalName].Replace(" ", ""));

            return csharp;
        }
    }
}