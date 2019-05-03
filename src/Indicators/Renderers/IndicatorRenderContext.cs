using System;
using System.Collections.Generic;
using System.Drawing;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Currencies;

namespace CryptoBot.Indicators.Renderers
{
    public class IndicatorRenderContext
    {
        public readonly Indicator              Indicator;
        public readonly IndicatorDataAggregate DataAggregate;
        public readonly IndicatorSignal        Signal;
        public readonly string                 SignalNote;
        public readonly Rectangle              Bounds;
        public readonly Rectangle              OuterBounds;
        public readonly Color                  Background;
        public readonly Bitmap                 Result;

        public int OuterBoundsMin => Math.Min(OuterBounds.Width, OuterBounds.Height);
        public int OuterBoundsMax => Math.Max(OuterBounds.Width, OuterBounds.Height);
        public int BoundsMin => Math.Min(Bounds.Width, Bounds.Height);
        public int BoundsMax => Math.Max(Bounds.Width, Bounds.Height);

        public Market       Market => Indicator.Market;
        public CurrencyPair Pair   => Indicator.Market.Pair;

        public IndicatorRenderContext
        (
            Indicator indicator,
            IndicatorDataAggregate data,
            IndicatorSignal signal,
            string signalNote,
            Rectangle bounds,
            Color background
        )
        {
            Indicator      = indicator;
            DataAggregate  = data;
            Signal         = signal;
            SignalNote     = signalNote;
            Bounds         = new Rectangle(bounds.Location, bounds.Size);
            OuterBounds    = bounds;
            Background     = background;
            Result         = new Bitmap(bounds.Width, bounds.Height);

            int padding = (int)(OuterBoundsMax * 0.05f);
            Bounds.Inflate(-padding, -padding);
        }
    }
}