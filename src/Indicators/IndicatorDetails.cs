using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CryptoBot.Indicators
{
    public class IndicatorDetails
    {
        public string Name;
        public bool IsOscillator;
        public bool IsLagging;

        [JsonConverter(typeof(StringEnumConverter))]
        public IndicatorType Type;

        public IndicatorSetting[] Settings;

        public IndicatorDetails
        (
            string name,
            bool oscillator,
            bool lagging,
            IndicatorType type,
            IndicatorSetting[] settings = null
        )
        {
            Name         = name;
            IsOscillator = oscillator;
            IsLagging    = lagging;
            Type         = type;
            Settings     = settings ?? new IndicatorSetting[0];
        }
    }
}