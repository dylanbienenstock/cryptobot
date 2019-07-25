using System;
using System.Collections.Generic;
using System.Drawing;
using System.Dynamic;
using System.Linq;
using System.Threading;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Series;
using CryptoBot.Indicators.Renderers;
using CryptoBot.Series;
using CryptoBot.Scripting.Typings;

namespace CryptoBot.Indicators
{
    using Node = StatisticalSeriesNode<TradingPeriod>;

    public class WilliamsFractals : Indicator
    {
        public bool Complete = false;
        public CapacitySeries<Fractal> Fractals;

        private long _lastUpFractalTime = 0;
        private long _lastDownFractalTime = 0;
        private double _lastUpFractalPrice = 0;
        private double _lastDownFractalPrice = 0;

        public WilliamsFractals() { }

        public struct Fractal
        {
            public long Time;
            public Double Price;
            public bool IsUpFractal;
        }
        
        public override IndicatorDetails Details => new IndicatorDetails
        (
            name:       "Williams' Fractals",
            oscillator: false,
            lagging:    true,
            type:       IndicatorType.Trend
        );

        public override void Configure(dynamic settings)
        {
            PrimaryOutputField("Last Up Fractal Time");
            OutputField("Last Up Fractal Price");
            OutputField("Last Down Fractal Time");
            OutputField("Last Down Fractal Price");

            var series = RequireInput(TimeFrame, 5);
            BindTo(series.Values);

            Fractals = new CapacitySeries<Fractal>(64);
        }

        private long TimeOf(Node node) => (long)node.Value.Time.GetQuantizedMilliseconds(TimeFrame);

        [TypescriptIgnore]
        public override void OnComplete() { Complete = true; }

        public override void OnTradingPeriodClose(Node node)
        {
            if (!Complete) return;

            var sideHighs = Source.Select(tp => tp.Value.High).ToList();
            var sideLows  = Source.Select(tp => tp.Value.Low).ToList();
            var middleHigh = sideHighs[2];
            var middleLow = sideLows[2];
            sideHighs.RemoveAt(2);
            sideLows.RemoveAt(2);

            if (sideHighs.All(high => high < middleHigh))
            {
                _lastUpFractalTime = TimeOf(Input.Values.Head.Next.Next);
                _lastUpFractalPrice = (double)middleHigh;
            }

            if (sideLows.All(low => low > middleLow))
            {
                _lastDownFractalTime = TimeOf(Input.Values.Head.Next.Next);
                _lastDownFractalPrice = (double)middleLow;
            }

            EmitNextValue(new Dictionary<string, object>()
            {
                { "Last Up Fractal Time",    _lastUpFractalTime    },
                { "Last Up Fractal Price",   _lastUpFractalPrice   },
                { "Last Down Fractal Time",  _lastDownFractalTime  },
                { "Last Down Fractal Price", _lastDownFractalPrice }
            });
        }

        public Fractal GetLastUpFractal() => new Fractal
        {
            Time  = _lastUpFractalTime,
            Price = _lastUpFractalPrice,
            IsUpFractal = true
        };

        public Fractal GetLastDownFractal() => new Fractal
        {
            Time  = _lastDownFractalTime,
            Price = _lastDownFractalPrice,
            IsUpFractal = false
        };
    }
}