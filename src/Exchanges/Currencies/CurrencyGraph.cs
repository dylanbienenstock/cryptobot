using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using CryptoBot.Exchanges.Orders;

namespace CryptoBot.Exchanges.Currencies
{
    public class CurrencyGraph
    {
        private static Currency[] CurrencyBlacklist = new Currency[] 
        {
            Currency.EUR, Currency.GBP
        };

        public List<CurrencyVertex> Vertices;
        public List<CurrencyEdge>   Edges;
        public List<CurrencyVertex> Cycle;
        private ExchangeNetwork     _network;
        private Random              _random;

        public CurrencyGraph(ExchangeNetwork network)
        {
            Vertices = new List<CurrencyVertex>();
            Edges    = new List<CurrencyEdge>();
            Cycle    = new List<CurrencyVertex>();
            _network = network;
            _random  = new Random();
        }

        public bool HasCycle(CurrencyEdge edge) => 
            edge.Away.MinDistance > (edge.Home.MinDistance + edge.Weight);

        private CurrencyEdge FindEdge(CurrencyVertex home, CurrencyVertex away) => 
            Edges.Find(e => e.Home == home && e.Away == away);

        public void BellmanFord()
        {
            var sw = new Stopwatch();
            sw.Start();

            ResetEdges();
            Cycle.Clear();

            foreach (var vertex in Vertices.Skip(1).SkipLast(1))
                Edges.ForEach(edge => Relax(edge));

            foreach (var edge in Edges)
            {
                if (!HasCycle(edge)) continue;
                if (edge.ExchangeRate == -1) continue;

                var vertex = edge.Home;
                var count = 0;

                while (vertex != edge.Away && !vertex.Visited && count++ < 32)
                {
                    Cycle.Add(vertex);
                    vertex.Visited = true;
                    vertex = vertex.Previous;
                }
                
                Cycle.Add(vertex);

                Cycle.Reverse();

                try
                {
                    TraceProfit(100, sw);
                }
                catch { }

                break;
            }
        }

        private void Relax(CurrencyEdge edge)
        {
            if (edge.ExchangeRate == -1) return;

            double newDistance = edge.Home.MinDistance + edge.Weight;

            if (edge.Away.MinDistance <= newDistance) return;

            edge.Away.MinDistance = newDistance;
            edge.Away.Previous = edge.Home;
        }

        public static Tuple<decimal, Market, OrderSide> GetExchangeRate(CurrencyEdge edge, bool withFee = true)
        {
            var pair = new CurrencyPair(edge.Home.Currency, edge.Away.Currency);
            var market = edge.Away.Exchange.Markets.First(m =>
                (m.Key.Base == edge.Home.Currency && m.Key.Quote == edge.Away.Currency) ||
                (m.Key.Base == edge.Away.Currency && m.Key.Quote == edge.Home.Currency)
            ).Value;

            decimal fee = withFee ? market.Exchange.Fee : 0;
            decimal exchangeRate = -1;
            var side = OrderSide.Bid;

            if (market.Pair.Base == edge.Home.Currency)
            {
                side = OrderSide.Ask;

                if (market.Orders.Bids.Tail != null)
                    exchangeRate = (1m - fee) * market.BestBid;
            }
            else
            {
                if (market.Orders.Asks.Tail != null)
                    exchangeRate = (1m - fee) * 1 / market.BestAsk;
            }

            return Tuple.Create(exchangeRate, market, side);
        }

        private string _lastPath = "";
        private double _lastProfit = 0;
        private void TraceProfit(double startingCash, Stopwatch sw)
        {
            if (Cycle.Count < 3) return;

            double cash = startingCash;
            var orderPartsList = new List<List<string>>();

            for (int i = 0; i < Cycle.Count; i++)
            {
                double lastCash = cash;
                int nextIndex = (i + 1) % Cycle.Count;
                int index = i % Cycle.Count;
                var edge = FindEdge(Cycle[index], Cycle[nextIndex]);
                cash *= edge.ExchangeRate;

                string buyOrSell = edge.Side == OrderSide.Bid ? "BUY-" : "SELL";
                orderPartsList.Add(new List<string>()
                {
                    $"[{edge.Home.Exchange.Name}]",
                    lastCash.ToString(),
                    edge.Home.Currency.ToString(),
                    $"---{buyOrSell}--->",
                    edge.Away.Currency.ToString(),
                    cash.ToString(),
                    $"[{edge.Away.Exchange.Name}]"
                });
            }

            sw.Stop();

            if (cash < startingCash) return;

            double profitPercent = ((cash / startingCash) - 1) * 100;

            string path = String.Join(" --> ", Cycle.Select(c => Enum.GetName(typeof(Currency), c.Currency)));
            bool differentPath = _lastPath != path;
             _lastPath = path;
            string profitStr = "PROFIT: ";

            if (differentPath)
            {
                Journal.Log($"\nArbitrage path found at {DateTime.Now.ToString()}");
                Journal.Log($"{path} (Took {sw.Elapsed.TotalMilliseconds} ms)");
                Journal.Log("---------------------------------------------------------------------------------------");

                var orderPartLengths = new List<int>();

                foreach (var orderParts in orderPartsList)
                {
                    for (int i = 0; i < orderParts.Count; i++)
                    {
                        var orderPart = orderParts[i];

                        if (orderPartLengths.Count < (i + 1)) 
                            orderPartLengths.Add(orderPart.Length);

                        orderPartLengths[i] = Math.Max(orderPartLengths[i], orderPart.Length);
                    }
                }

                foreach (var orderParts in orderPartsList)
                {
                    for (int i = 0; i < orderParts.Count; i++)
                    {
                        if (orderParts[i].Length < orderPartLengths[i])
                            orderParts[i] += new string(' ', orderPartLengths[i] - orderParts[i].Length);                     
                    }

                    Console.WriteLine(String.Join(' ', orderParts));
                    Journal.Log(String.Join(' ', orderParts));
                }
                
                Journal.Log("---------------------------------------------------------------------------------------");
            }
            else 
            {
                if (profitPercent != _lastProfit)
                    profitStr = "        ";
            }

            if (profitPercent != _lastProfit)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                
                if (!differentPath && _lastProfit > profitPercent)
                    Console.ForegroundColor = ConsoleColor.Red;

                Console.WriteLine(profitStr + " " + profitPercent + "%");
                Journal.Log(profitStr + " " + profitPercent + "%");
                Console.ResetColor();
            }

            _lastProfit = profitPercent;
        }

        private void ResetEdges()
        {
            for (int i = 0; i < Edges.Count; i++)
            {
                Edges[i].Home.MinDistance = Double.MaxValue;
                Edges[i].Away.MinDistance = Double.MaxValue;
                Edges[i].Home.Previous = null;
                Edges[i].Away.Previous = null;
                Edges[i].Home.Visited = false;
                Edges[i].Away.Visited = false;
            }

            Edges = Edges.Select(edge => {
                var rateTuple = GetExchangeRate(edge);

                return new CurrencyEdge
                {
                    Home = edge.Home,
                    Away = edge.Away,
                    ExchangeRate = (double)rateTuple.Item1,
                    Market = rateTuple.Item2,
                    Side = rateTuple.Item3
                };
            }).ToList();

            Edges[0].Home.MinDistance = 0;
        }

        public CurrencyVertex FindOrCreateVertex(Exchange exchange, Currency currency)
        {
            var index = Vertices.FindIndex(v => v.Exchange == exchange && v.Currency == currency);

            if (index != -1) return Vertices[index];

            var vertex = new CurrencyVertex
            {
                Exchange = exchange,
                Currency = currency,
                MinDistance = Double.MaxValue
            };

            Vertices.Add(vertex);

            return vertex;
        }

        public void Build()
        {
            for (int i = 0; i < _network.Exchanges.Length; i++)
            {
                Exchange homeExchange = _network.Exchanges[i];

                for (int j = 0; j < homeExchange.Currencies.Count; j++)
                {
                    Currency homeCurrency = homeExchange.Currencies[j];
                    
                    if (CurrencyBlacklist.Contains(homeCurrency)) continue;

                    var homeVertex = FindOrCreateVertex(homeExchange, homeCurrency);

                    for (int k = 0; k < _network.Exchanges.Length; k++)
                    {
                        Exchange awayExchange = _network.Exchanges[k];

                        foreach (var awayPair in awayExchange.Markets.Keys)
                        {
                            Currency? awayCurrency = null;

                            if (awayPair.Base == homeCurrency)
                                awayCurrency = awayPair.Quote;
                            else if (awayPair.Quote == homeCurrency)
                                awayCurrency = awayPair.Base;
                            else continue;

                            if (awayCurrency == null) continue;
                            if (CurrencyBlacklist.Contains((Currency)awayCurrency)) continue;

                            var awayVertex = FindOrCreateVertex(awayExchange, (Currency)awayCurrency);
                        
                            Edges.Add(new CurrencyEdge
                            {
                                Home = homeVertex,
                                Away = awayVertex
                            });
                        }
                    }
                }
            }
        }

        public void RenderToImage()
        {
            int nodeRadius = 96;
            int imageSize = (int)Math.Ceiling((Vertices.Count * nodeRadius * 2) / Math.PI);

            using (var image = new Bitmap(imageSize, imageSize))
            {
                using (var graphics = Graphics.FromImage(image))
                {
                    Font font = new Font("Iosevka", 7);
                    StringFormat textFormat = new StringFormat();
                    textFormat.LineAlignment = StringAlignment.Center;
                    textFormat.Alignment = StringAlignment.Center;
                    var nodePos = new Dictionary<int, Point>();

                    for (int i = 0; i < Vertices.Count; i++)
                    {
                        var vertex = Vertices[i];
                        var hash = (vertex.Exchange.Index << 2) ^ (int)vertex.Currency;

                        Brush brush = Brushes.Silver;
                        double angle = i * (Math.PI * 2 / Vertices.Count);
                        double dirX = Math.Cos(angle);
                        double dirY = Math.Sin(angle);
                        double posX = (imageSize / 2 + dirX * (imageSize / 2 - nodeRadius));
                        double posY = (imageSize / 2 + dirY * (imageSize / 2 - nodeRadius));
                        string text = vertex.Currency.ToString() + "\n" + vertex.Exchange.Name;

                        nodePos[hash] = new Point((int)posX, (int)posY);

                        graphics.FillEllipse(brush, (int)posX - nodeRadius / 2, (int)posY - nodeRadius / 2, nodeRadius, nodeRadius);
                        graphics.DrawString(text, font, Brushes.Black, (int)posX, (int)posY, textFormat);
                    }

                    foreach (var edge in Edges)
                    {
                        var homeHash = (edge.Home.Exchange.Index << 2) ^ (int)edge.Home.Currency;
                        var awayHash = (edge.Away.Exchange.Index << 2) ^ (int)edge.Away.Currency;

                        double gap = nodeRadius * 0.62;
                        double diffX = nodePos[awayHash].X - nodePos[homeHash].X;
                        double diffY = nodePos[awayHash].Y - nodePos[homeHash].Y;
                        double dist = Math.Sqrt(diffX * diffX + diffY * diffY);
                        double angle = Math.Atan2(diffY, diffX);
                        double dirX = diffX / dist;
                        double dirY = diffY / dist;
                        int startX = (int)Math.Round(nodePos[homeHash].X + dirX * gap);
                        int startY = (int)Math.Round(nodePos[homeHash].Y + dirY * gap);
                        int endX = (int)Math.Round(nodePos[awayHash].X - dirX * gap);
                        int endY = (int)Math.Round(nodePos[awayHash].Y - dirY * gap);
                        int headLen = 12;
                        double toRad = Math.PI / 180;

                        var pen = new Pen(edge.Home.Exchange == edge.Away.Exchange ? Color.Black : Color.Black, 2);

                        graphics.DrawLine(pen, startX, startY, endX, endY);
                        graphics.DrawLine(
                            pen,
                            endX,
                            endY,
                            (int)Math.Round(endX - Math.Cos(angle + 30 * toRad) * headLen),
                            (int)Math.Round(endY - Math.Sin(angle + 30 * toRad) * headLen)
                        );
                        graphics.DrawLine(
                            pen,
                            endX,
                            endY,
                            (int)Math.Round(endX - Math.Cos(angle - 30 * toRad) * headLen),
                            (int)Math.Round(endY - Math.Sin(angle - 30 * toRad) * headLen)
                        );
                    }
                }

                image.Save(Path.Join(".", "marketgraph.png"));
            }
        }
    }
}