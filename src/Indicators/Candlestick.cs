using System;
using System.Collections.Generic;
using System.Drawing;
using System.Dynamic;
using System.Threading;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Series;
using CryptoBot.Indicators.Renderers;
using CryptoBot.Series;

namespace CryptoBot.Indicators
{
    using Node = StatisticalSeriesNode<TradingPeriod>;

    public class Candlestick : Indicator
    {
        public decimal Value { get; private set; }

        public Candlestick() { }
        
        public override IndicatorDetails Details => new IndicatorDetails
        (
            name:       "Candlestick",
            oscillator: false,
            lagging:    false,
            type:       IndicatorType.Price
        );

        public override void Configure(dynamic settings)
        {
            OutputField("Open");
            OutputField("High");
            OutputField("Low");
            PrimaryOutputField("Close");

            var series = RequireInput(TimeFrame, 4);
            BindTo(series.Values);
        }

        public override void OnFinalizeRecord(Node node) {
            EmitNextValue(new Dictionary<string, object>()
            {
                { "Open",  node.Value.Open  },
                { "High",  node.Value.High  },
                { "Low",   node.Value.Low   },
                { "Close", node.Value.Close }
            });
        }

        public override void OnTradingPeriodClose(Node node)
        {

        }

        public override void OnPostAdd(Node node) {
            // EmitNextValue(new Dictionary<string, decimal>()
            // {
            //     { "Open",  node.Value.Open  },
            //     { "High",  node.Value.High  },
            //     { "Low",   node.Value.Low   },
            //     { "Close", node.Value.Close }
            // });
        }
        
        public override void OnPostRemove(Node node) { }
        public override void OnPreUpdate(Node node) { }

        public override void OnPostUpdate(Node node)
        {
            // EmitUpdateValue(new Dictionary<string, decimal>()
            // {
            //     { "High",  node.Value.High  },
            //     { "Low",   node.Value.Low   },
            //     { "Close", node.Value.Close }
            // });
        }
    }
}