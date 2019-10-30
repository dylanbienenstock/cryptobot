using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CryptoBot.Indicators
{
    public struct IndicatorSetting
    {
        public string Key;
        public string Name;
        [JsonConverter(typeof(StringEnumConverter))]
        public IndicatorSettingType Type;
        public dynamic DefaultValue;

        public string TypescriptType
        {
            get
            {
                switch (Type)
                {
                    case IndicatorSettingType.Int:
                    case IndicatorSettingType.Float:
                        return "number";
                    case IndicatorSettingType.Aspect:
                        return "'open' | 'high' | 'low' | 'close'";
                }
                return "unknown";
            }
        }

        public IndicatorSetting(string key, string name, IndicatorSettingType type, dynamic defaultValue)
        {
            Key = key;
            Name = name;
            Type = type;
            DefaultValue = defaultValue;
        }

        public IndicatorSetting(string key, IndicatorSettingType type, dynamic defaultValue)
        {
            Key = key;
            Name = key;
            Type = type;
            DefaultValue = defaultValue;
        }
    }
}