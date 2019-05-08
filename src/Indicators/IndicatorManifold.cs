using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Series;
using CryptoBot.Indicators.Renderers;
using Telegram.Bot.Types;

namespace CryptoBot.Indicators
{
    public class IndicatorManifold
    {
        public static bool __DEBUG__ = true;

        private static Rectangle _renderBounds = new Rectangle(0, 0, 1024, 396);
        private static Color _renderBackground = Color.FromArgb(255, 48, 48, 48);
        
        public ExchangeNetwork                                                 Network;
        public Dictionary<Market, List<Indicator>>                             Indicators;
        public Dictionary<Indicator, IndicatorSignal>                          Signals;
        public Dictionary<Indicator, string>                                   Notes;
        public IndicatorFactory                                                Use;
        private Dictionary<Market, Dictionary<int, TradingPeriodSeries>>       _inputs;
        private Dictionary<Indicator, IndicatorDataAggregate>                  _outputs;
        private Dictionary<Market, DateTime>                                   _historyTime;
        private Dictionary<Indicator, int>                                     _periodDurations;

        public IndicatorManifold(ExchangeNetwork network)
        {
            Network          = network;
            Indicators       = new Dictionary<Market, List<Indicator>>();
            Signals          = new Dictionary<Indicator, IndicatorSignal>();
            Notes            = new Dictionary<Indicator, string>();
            Use              = new IndicatorFactory(this);

            _inputs          = new Dictionary<Market, Dictionary<int, TradingPeriodSeries>>();
            _outputs         = new Dictionary<Indicator, IndicatorDataAggregate>();
            _historyTime     = new Dictionary<Market, DateTime>();
            _periodDurations = new Dictionary<Indicator, int>();
            
            foreach (var exchange in Network.Exchanges)
            {
                foreach (var market in exchange.Markets.Values)
                {
                    Indicators[market] = new List<Indicator>();
                    _inputs[market] = new Dictionary<int, TradingPeriodSeries>();
                }
            }
        }

        public void AddHistoricalTradingPeriods(Market market, List<HistoricalTradingPeriod> tradingPeriods)
        {
            foreach (var input in _inputs[market].Values)
            {
                foreach (var tradingPeriod in tradingPeriods)
                {
                    _historyTime[market] = tradingPeriod.Time;
                    input.Add(tradingPeriod);
                }
            }
        }

        public IndicatorMultiInstance<T> CreateRaw<T>(params dynamic[] settings) where T : Indicator
        {
            var instances = new Dictionary<Market, T>();
            
            foreach (var market in _inputs.Keys)
            {
                T indicator = (T)Activator.CreateInstance(typeof(T), this);

                _outputs [indicator] = new IndicatorDataAggregate(DebugConst.Timescale);
                instances[market]    = indicator;
                Signals  [indicator] = IndicatorSignal.Neutral;
                Notes    [indicator] = "Neutral";
                Indicators[market].Add(indicator);

                indicator.Market = market;
                indicator.Configure(settings);
            }

            return new IndicatorMultiInstance<T>(instances);
        }

        public TradingPeriodSeries RequireInput(Indicator indicator, int periodDuration, int periods)
        {
            if (!_inputs[indicator.Market].ContainsKey(periodDuration))
            {
                _inputs[indicator.Market][periodDuration] = 
                    new TradingPeriodSeries(indicator.Market.Trades, periodDuration, periods);
            }

            return _inputs[indicator.Market][periodDuration];
        }

        public void RegisterPeriodDuration(Indicator indicator, int milliseconds) =>
            _periodDurations[indicator] = milliseconds;

        public void RegisterPrimaryOutput(Indicator indicator, string fieldName, IndicatorRenderer renderer) =>
            _outputs[indicator].AddPrimaryField(fieldName, renderer);

        public void RegisterOutput(Indicator indicator, string fieldName, IndicatorRenderer renderer) =>
            _outputs[indicator].AddField(fieldName, renderer);

        public string GetFormattedOutputValues(Indicator indicator)
        {
            string formatted = "";
            int maxNameLength = _outputs[indicator].Fields.Select(f => f.Key.Length).Max();
            bool negativeValuesPresent = _outputs[indicator].Fields.Values.Any(f => f.Values.Tail.Value < 0);

            foreach (var output in _outputs[indicator].Fields)
            {
                string name = output.Key + ':' + new string(' ', maxNameLength - output.Key.Length);
                float value = output.Value.Values.Tail.Value;
                string valueString = value.ToString();
                int dotIndex = valueString.IndexOf('.');
                string valueTruncated = valueString;

                if (Regex.Match(valueString, @"^-?\d+\.0{2}").Success)
                    valueTruncated = string.Format("{0:#.##E+0}", value);
                else if (dotIndex != -1) valueTruncated = 
                    valueTruncated = valueString.Substring(0, Math.Min(valueString.Length, dotIndex + 5));

                if (negativeValuesPresent && value >= 0)
                    valueTruncated = ' ' + valueTruncated;

                formatted += $"\n`• {name} {valueTruncated}`";
            }

            return formatted;
        }

        public void OnSignal(Indicator indicator, IndicatorSignal signal, string note)
        {
            if (!__DEBUG__ && Signals[indicator] == signal) return;
            if (!indicator.Market.UpToDate) return;

            Signals[indicator] = signal;
            Notes[indicator] = note;
            
            SendTelegramSignal(indicator);
        }

        private void SendTelegramSignal(Indicator indicator)
        {
            var primaryOutput = _outputs[indicator].PrimaryField;

            if (primaryOutput.Values.Count < 2) return;

            var signal     = Signals[indicator];
            var note       = Notes[indicator];
            var readout    = GetFormattedOutputValues(indicator);
            var signalName = Indicator.SignalToString(signal);
            var message    = $"\\[{indicator.Name}] *{indicator.Market.Pair.ToGenericSymbol()}* {signalName} \n\n*{note}* {readout}";

            var renderContext = new IndicatorRenderContext
            (
                indicator: indicator,
                data: _outputs[indicator],
                signal: signal,
                signalNote: note,
                bounds: _renderBounds,
                background: _renderBackground
            );

            var moreIndicatorsOptions = Indicators[indicator.Market]
                .Select<Indicator, (string, Action<CallbackQuery>)>
                (ind => 
                    (ind.Name, query => {
                        TelegramBot.UpdateInlineKeyboard(query.Message, null);
                        SendTelegramSignal(ind);
                    })
                )
                .ToArray();

            IndicatorRenderer.Render(renderContext);
            TelegramBot.SendImage
            (
                image: renderContext.Result,
                caption: message,
                options: new TelegramBot.InlineKeyboard
                (
                    ("More Indicators", query =>
                    {
                        TelegramBot.UpdateInlineKeyboard
                        (
                            message: query.Message,
                            options: new TelegramBot.InlineKeyboard(moreIndicatorsOptions)
                        );
                    })
                )
            );
        }

        public void OnNextValue(Indicator indicator, string fieldName, float value)
        {
            // ! TODO: FIX THIS ! This shouldn't be neccesary. 
            if (!indicator.Market.UpToDate && !_historyTime.ContainsKey(indicator.Market)) return;

            var currentTime = DateTime.UtcNow.Quantize(_periodDurations[indicator]);
            var historyTime = _historyTime[indicator.Market].Quantize(_periodDurations[indicator]);
            var time        = indicator.Market.UpToDate ? currentTime : historyTime;

            _outputs[indicator].Record(fieldName, value, time);
        }

        // public void RenderIndicator(Indicator indicator, Bitmap image, Rectangle _window)
        // {
        //     int periodDuration = _periodDurations[indicator];
            
        //     using (var graphics = Graphics.FromImage(image))
        //     {
        //         graphics.SmoothingMode = SmoothingMode.HighQuality;

        //         var vmax = (int)Math.Round((double)Math.Max(_window.Width, _window.Height));
        //         int padding = (int)(vmax * 0.05f);
        //         var window = new Rectangle(_window.Location, _window.Size);
        //         window.Inflate(-padding, -padding);

        //         var   primary    = _outputs[indicator][_primaryOutputs[indicator]];
        //         var   now        = primary.GetTime(primary.Tail).Quantize(periodDuration);
        //         float min        = _outputs[indicator].Values.Select(ts => ts.Select(n => (float)n.Value).Min()).Min();
        //         float max        = _outputs[indicator].Values.Select(ts => ts.Select(n => (float)n.Value).Max()).Max();
        //         var   headTime   = primary.GetTime(primary.Head);
        //         var   tailTime   = primary.GetTime(primary.Tail);
        //         int   maxPeriods = (int)_timespans[indicator].TotalMilliseconds / periodDuration;
        //         float domain     = (float)(tailTime - headTime).TotalMilliseconds;
        //         float range      = max - min;
        //         float scaleX     = window.Width / domain;
        //         float scaleY     = window.Height / range;

        //         Func<float, float> mapX = (val) => window.X + window.Width - val * scaleX;
        //         Func<float, float> mapY = (val) => window.Y + (val - min) * scaleY;

        //         float barPadding = 1;
        //         var outlinePen = new Pen(_renderBackground, 3.5f)
        //         {
        //             LineJoin = LineJoin.Round,
        //             EndCap = LineCap.Round
        //         };
        //         var baseline = mapY(0);
        //         var sortedOutputs = _outputs[indicator]
        //             .OrderByDescending(i => (int)_renderTypes[indicator][i.Key]);

        //         foreach (var output in sortedOutputs)
        //         {
        //             if (output.Value.Count < 2) continue;

        //             var color = _renderColors[indicator][output.Key];
        //             var brush = new SolidBrush(color);
        //             var pen = new Pen(color, 2.0f)
        //             {
        //                 LineJoin = LineJoin.Round,
        //                 EndCap = LineCap.Round
        //             };

        //             Func<StatisticalSeriesNode<decimal>, PointF> getPoint = (node) =>
        //             {
        //                 var time = (now - output.Value.GetTime(node).Quantize(periodDuration)).TotalMilliseconds;
        //                 return new PointF(mapX((float)time), mapY((float)node.Value));
        //             };

        //             Func<StatisticalSeriesNode<decimal>, RectangleF> getRectangle = (node) =>
        //             {
        //                 float time = (float)(now - output.Value.GetTime(node).Quantize(periodDuration)).TotalMilliseconds;
        //                 float width = window.Width / ((float)output.Value.Count) - barPadding * 2;
        //                 float x = mapX(time) + barPadding - width / 2;
        //                 float y = (float)node.Value * scaleY;

        //                 if (node.Value >= 0)
        //                     return new RectangleF(x, baseline - y, width, y);

        //                 return new RectangleF(x, baseline, width, -y);
        //             };

        //             Func<PointF[]> getPointArray = () => 
        //                 output.Value.Select(val => getPoint(val)).ToArray();

        //             Func<Func<decimal, bool>, RectangleF[]> getRectangleArray = (condition) =>
        //             {
        //                 return output.Value
        //                     .Where(node => condition(node.Value))
        //                     .Select(node => getRectangle(node))
        //                     .ToArray();
        //             };

        //             switch (_renderTypes[indicator][output.Key])
        //             {
        //                 case IndicatorRenderType.Line:
        //                     graphics.DrawLines(outlinePen, getPointArray());
        //                     graphics.DrawLines(pen, getPointArray());
        //                     break;

        //                 case IndicatorRenderType.Histogram:
        //                     var positiveBrush = new SolidBrush(IndicatorColor.Bullish);
        //                     var negativeBrush = new SolidBrush(IndicatorColor.Bearish);
        //                     var positiveBars = getRectangleArray(val => val >= 0);
        //                     var negativeBars = getRectangleArray(val => val < 0);

        //                     if (positiveBars.Length > 0)
        //                         graphics.FillRectangles(positiveBrush, positiveBars);

        //                     if (negativeBars.Length > 0)
        //                         graphics.FillRectangles(negativeBrush, negativeBars);

        //                     break;
        //             }
        //       }
        //   }
        // }
    }
}