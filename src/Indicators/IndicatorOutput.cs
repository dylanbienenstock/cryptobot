using System;
using System.Collections.Generic;

namespace CryptoBot.Indicators
{
    public struct IndicatorOutput
    {
        public DateTime Time;
        public Dictionary<string, object> Changes;

        public IndicatorOutput(DateTime time, Dictionary<string, object> changes)
        {
            Time = time;
            Changes = changes;
        }
    }
}