using System;
using System.Collections.Generic;

namespace CryptoBot.TradingStategyLanguage
{
    public enum TSLTokenType
    {
        Unknown,
        EndStatement,
        Scope,
        Assignment,
        Reference,
        Literal,
        Condition,
        Event,
        Metric,
        SetMetricMode,
        Redirect,
        Command,
    }

    public enum TSLTokenValueType
    {
        Unknown,
        Identifier,
        String,
        Number
    }

    public enum TSLTokenValueSubType
    {
        None,
        Local,
        Global,
        Percentage,
        Milliseconds,
        Seconds,
        Minutes,
        Hours,
        Days,
    }

    public static class TSLOperator
    {
        public const string EndStatement  = ";";
        public const string Scope         = "|---";
        public const string Assign        = ":";
        public const string Redirect      = "->";
        public const string SetMetricMode = "~";
    }

    public class TSLToken
    {
        public TSLTokenType Type;
        public string Value;
        public TSLTokenValueType ValueType;
        public TSLTokenValueSubType ValueSubType;

        public bool Valid => Type != TSLTokenType.Unknown;

        public string CurrentWord
        {
            get
            {
                if (!Value.Contains(' ')) return Value;
                var words = Value.Split(' ');
                return words[words.Length - 1];
            }
        }

        public TSLToken(TSLTokenType type = TSLTokenType.Unknown)
        {
            Type = type;
            Value = "";
            ValueType = TSLTokenValueType.Unknown;
            ValueSubType = TSLTokenValueSubType.None;
        }

        public override string ToString()
        {
            string type = Enum.GetName(typeof(TSLTokenType), Type);
            string valueType = Enum.GetName(typeof(TSLTokenValueType), ValueType);
            string valueSubType = Enum.GetName(typeof(TSLTokenValueSubType), ValueSubType);

            string str = $"({type}";

            if (valueType != "Unknown")
            {
                str += $" {valueType}";

                if (valueSubType != "None")
                    str += $"/{valueSubType}";
            }

            if (!String.IsNullOrWhiteSpace(Value))
                str += $" \"{Value}\"";

            str += ")";
            str = str.Replace(Environment.NewLine, "");
            str += "\n";

            if (Type == TSLTokenType.EndStatement)
                str += "\n";

            return str;
        }
    }

    public class TSLTokenizer
    {
        public static string Numbers = "0123456789";
        public static string Letters = "%@abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public enum CharType
        {
            None,
            Space,
            Number,
            Letter,
            Symbol
        }

        public static Dictionary<string, TSLTokenType> TokenKeywords = 
            new Dictionary<string, TSLTokenType>()
            {
                { "when",  TSLTokenType.Event     },
                { "up",    TSLTokenType.Condition },
                { "down",  TSLTokenType.Condition },
                { "buy",   TSLTokenType.Command   },
                { "sell",  TSLTokenType.Command   },
                { "RENKO", TSLTokenType.Metric    },
                { "ATR#",  TSLTokenType.Metric    },
            };

        public static Dictionary<string, TSLTokenValueSubType> ValueSubTypeIndicators = 
            new Dictionary<string, TSLTokenValueSubType>()
            {
                { "%",  TSLTokenValueSubType.Percentage   },
                { "ms", TSLTokenValueSubType.Milliseconds },
                { "s",  TSLTokenValueSubType.Seconds      },
                { "m",  TSLTokenValueSubType.Minutes      },
                { "h",  TSLTokenValueSubType.Hours        },
                { "d",  TSLTokenValueSubType.Days         },
            };

        public List<TSLToken> Tokens;
        public List<string> GlobalVariables;
        public List<string> LocalVariables;
        public CharType CurrentCharType = CharType.None;
        public CharType LastCharType = CharType.None;

        public TSLTokenizer()
        {
            Tokens = new List<TSLToken>();
            GlobalVariables = new List<string>();
            LocalVariables = new List<string>();

            Tokens.Add(new TSLToken());
        }

        public List<TSLToken> Tokenize(string tsl)
        {
            tsl = tsl.Replace("\r", "");
            tsl = tsl.Replace("|   ", "|---");

            foreach (string line in tsl.Split("\n"))
            {
                foreach (char c in line)
                    ReadCharacter(c);

                TryReadWord();
                FinishToken();
                SetTokenType(TSLTokenType.EndStatement);
                FinishToken();
            }

            TryReadWord();
            FinishToken();
            CleanUpTokens();

            foreach (var token in Tokens)
                Console.Write(token.ToString());

            return Tokens;
        }

        private void CleanUpTokens()
        {
            var tokens = new List<TSLToken>();
            var dstI = 0;

            for (int srcI = 0; srcI < Tokens.Count; srcI++)
            {
                if (srcI != 0)
                {
                    if (Tokens[srcI].Type == TSLTokenType.Unknown)
                    {
                        if (String.IsNullOrWhiteSpace(Tokens[srcI].Value))
                            continue;
                    }
                    else if (Tokens[srcI].Type == TSLTokenType.EndStatement)
                    {
                        if (tokens[dstI - 1].Type == TSLTokenType.EndStatement)
                            continue;
                    }
                }

                tokens.Add(Tokens[srcI]);
                dstI++;
            }

            Tokens = tokens;
        }

        private CharType GetCharType(char c)
        {
            if      (c == ' ')            return CharType.Space;
            else if (Letters.Contains(c)) return CharType.Letter;
            else if (Numbers.Contains(c)) return CharType.Number;
            else                          return CharType.Symbol;
        }

        private void ReadCharacter(char c)
        {
            CurrentCharType = GetCharType(c);

            if (CharTypeSwitchBreaksWord() && TryReadWord())
            {
                FinishToken();
            }
            else
            {
                if (TryReadAsOperator()) FinishToken();
            }

            CurrentToken.Value += c;
            LastCharType = CurrentCharType;            
        }

        private bool CharTypeSwitchBreaksWord()
        {
            if (LastCharType == CharType.None) return false;

            switch (CurrentCharType)
            {
                case CharType.Space:  return true;
                case CharType.Letter: return LastCharType != CharType.Letter && LastCharType != CharType.Number; 
                case CharType.Number: return LastCharType != CharType.Number; 
                case CharType.Symbol: return LastCharType != CharType.Symbol;
            }

            return true;
        }

        private bool TryReadWord()
        {
            if (CurrentWord == null)  return false;
            if (TryReadAsOperator())  return true;
            if (TryReadAsKeyword())   return true;
            if (TryReadAsReference()) return true;
            if (TryReadAsLiteral())   return true;

            return false;
        }

        private bool TryReadAsOperator()
        {
            if (CurrentWord.Length == 0) return false;

            switch (CurrentWord)
            {
                case TSLOperator.EndStatement:
                    FinishToken();
                    SetTokenType(TSLTokenType.EndStatement);
                    return true;

                case TSLOperator.Scope:
                    SetTokenType(TSLTokenType.Scope);

                    if (LastToken.Type == TSLTokenType.Scope)
                    {
                        CurrentToken.Value = (int.Parse(LastToken.Value) + 1).ToString();
                        RemoveLastToken();
                    }
                    else
                    {
                        CurrentToken.Value = "0";
                    }
                    return true;

                case TSLOperator.Assign:
                    RemoveCurrentToken();
                    SetTokenType(TSLTokenType.Assignment);
                    SetTokenValueType(TSLTokenValueType.Identifier);
                    
                    if (CurrentToken.Value.StartsWith("@"))
                    {
                        RemoveCharsFromStart(1);
                        SetTokenValueSubType(TSLTokenValueSubType.Global);
                        GlobalVariables.Add(CurrentWord);
                    }
                    else
                    {
                        SetTokenValueSubType(TSLTokenValueSubType.Local);
                        LocalVariables.Add(CurrentWord);
                    }
                    return true;
                
                case TSLOperator.SetMetricMode:
                    SetTokenType(TSLTokenType.SetMetricMode);
                    ClearTokenValue();
                    return true;

                case TSLOperator.Redirect:
                    SetTokenType(TSLTokenType.Redirect);
                    ClearTokenValue();
                    return true;
            }

            return false;
        }

        private bool TryReadAsKeyword()
        {
            foreach (var rawIndicator in TokenKeywords.Keys)
            {
                string indicator = rawIndicator;

                if (rawIndicator.EndsWith("#"))
                    indicator = indicator.Substring(0, indicator.Length - 1);

                if (CurrentWord != indicator) continue;

                SetTokenType(TokenKeywords[rawIndicator]);
                return true;
            }

            return false;
        }

        private bool TryReadAsReference()
        {
            string variableName = CurrentWord;

            if (CurrentWord.StartsWith("@"))
            {
                variableName = CurrentWord.Substring(1, CurrentWord.Length - 1);

                if (!GlobalVariables.Contains(variableName)) return false;
                
                SetTokenType(TSLTokenType.Reference);
                SetTokenValueType(TSLTokenValueType.Identifier);
                SetTokenValueSubType(TSLTokenValueSubType.Global);
            }

            if (!LocalVariables.Contains(variableName)) return false;
            
            SetTokenType(TSLTokenType.Reference);
            SetTokenValueType(TSLTokenValueType.Identifier);
            SetTokenValueSubType(TSLTokenValueSubType.Local);

            return true;
        }

        private bool TryReadAsLiteral()
        {
            if (String.IsNullOrWhiteSpace(CurrentWord)) return false;

            CurrentToken.Value = CurrentToken.Value.Trim();
            SetTokenType(TSLTokenType.Literal);

            switch (GetCharType(CurrentWord[0]))
            {
                case CharType.Letter:
                    SetTokenValueType(TSLTokenValueType.String);

                    if (Tokens.Count < 2)                                return true;
                    if (LastToken.Type != TSLTokenType.Literal)          return true;
                    if (LastToken.ValueType != TSLTokenValueType.String) return true;

                    LastToken.Value += " " + CurrentToken.Value;
                    RemoveCurrentToken();
                    return true;

                case CharType.Number:
                    return TryReadTokenNumberLiteral();
            }

            return false;
        }

        private bool TryReadTokenNumberLiteral()
        {
            foreach (var indicator in ValueSubTypeIndicators.Keys)
            {
                if (!CurrentWord.EndsWith(indicator)) continue;

                SetTokenValueType(TSLTokenValueType.Number);
                SetTokenValueSubType(ValueSubTypeIndicators[indicator]);
                RemoveCharsFromEnd(indicator.Length);

                return true;
            }

            foreach (var c in CurrentWord)
            {
                if (GetCharType(c) != CharType.Number)
                    return false;
            }

            SetTokenValueType(TSLTokenValueType.Number);

            return true;
        }

        private void RemoveCharsFromStart(int count) => CurrentToken.Value = CurrentToken.Value.Substring(count, CurrentToken.Value.Length - 1);
        private void RemoveCharsFromEnd(int count) => CurrentToken.Value = CurrentToken.Value.Substring(0, CurrentToken.Value.Length - count);
        private TSLToken CurrentToken => Tokens[Tokens.Count - 1];
        private TSLToken LastToken => Tokens[Tokens.Count - 2];
        private string CurrentWord => CurrentToken.CurrentWord;
        private void RemoveCurrentToken() => Tokens.RemoveAt(Tokens.Count - 1);
        private void RemoveLastToken() => Tokens.RemoveAt(Tokens.Count - 2);
        private void ClearTokenValue() => CurrentToken.Value = "";
        private void SetTokenType(TSLTokenType type) => CurrentToken.Type = type;
        private void SetTokenValueType(TSLTokenValueType type) => CurrentToken.ValueType = type;
        private void SetTokenValueSubType(TSLTokenValueSubType type) => CurrentToken.ValueSubType = type;
        private void FinishToken()
        {
            CurrentToken.Value = CurrentToken.Value.Trim();
            Tokens.Add(new TSLToken());
        }
    }
}