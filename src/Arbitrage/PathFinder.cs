using System;
using System.Collections.Generic;
using System.Threading;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Series;

namespace CryptoBot.Arbitrage
{
    public class Pathfinder
    {
        private ExchangeNetwork _network;
        private Thread _thread;

        public Pathfinder(ExchangeNetwork network)
        {
            _network = network;
            _thread = new Thread(Start);
            _thread.Start();
        }

        public void Start()
        {
            while (true)
            {
                _network.CurrencyGraph.BellmanFord();
                Thread.Sleep(1000);
            }
        }
    }
}