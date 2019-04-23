using System;
using System.IO;
using System.Collections.Generic;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Currencies;
using TeaTime;

namespace CryptoBot
{
    public static class Storage
    {
        public struct HistoricTrade
        {      
            public decimal Price;
            public decimal Amount;
            public TeaTime.Time Time;
        }

        private static int _fileDurationMilliseconds = 30 * 60000;

        private static string _storageDirectory;
        private static Dictionary<Market, TeaFile<HistoricTrade>> _tradeFiles;
        private static Dictionary<Market, double> _tradeFileTimes;

        public static void Initialize()
        {
            _storageDirectory = Environment.GetEnvironmentVariable("StorageDir")
                ?? Path.Join(Environment.CurrentDirectory, "Storage");
            _tradeFiles = new Dictionary<Market, TeaFile<HistoricTrade>>();
            _tradeFileTimes = new Dictionary<Market, double>();

            if (!Directory.Exists(_storageDirectory))
                Directory.CreateDirectory(_storageDirectory);
        }

        public static void RecordTrade(Market market, CurrencyTrade trade)
        {
            var tradeRecord = new HistoricTrade
            {
                Price = trade.Price,
                Amount = trade.Amount,
                Time = new TeaTime.Time(trade.Time.Ticks)
            };

            GetTradeFile(market, trade).Write(tradeRecord);
        }

        private static TeaFile<HistoricTrade> GetTradeFile(Market market, CurrencyTrade trade)
        {
            var quantizedMilliseconds = trade.GetQuantizedMilliseconds(_fileDurationMilliseconds);

            if (!_tradeFileTimes.ContainsKey(market) || _tradeFileTimes[market] != quantizedMilliseconds)
            {
                var quantizedTime     = DateTime.UnixEpoch.AddMilliseconds(quantizedMilliseconds);
                var exchangeDirectory = Path.Join(_storageDirectory, market.Exchange.Name);
                var tradesDirectory   = Path.Join(exchangeDirectory, "Trades");
                var pairDirectory     = Path.Join(tradesDirectory, market.Pair.ToString("-"));
                var currentHour       = $"{(quantizedTime.Hour < 10 ? "0" : "")}{quantizedTime.Hour}";
                var currentMinute     = $"{(quantizedTime.Minute < 10 ? "0" : "")}{quantizedTime.Minute}";
                var tradeFileName     = $"{quantizedTime.ToString("MMM-dd-yyyy")}-T{currentHour}{currentMinute}";
                var tradeFilePath     = Path.Join(pairDirectory, tradeFileName + ".tea");
                    
                if (!Directory.Exists(exchangeDirectory))
                    Directory.CreateDirectory(exchangeDirectory);

                if (!Directory.Exists(tradesDirectory))
                    Directory.CreateDirectory(tradesDirectory);

                if (!Directory.Exists(pairDirectory))
                    Directory.CreateDirectory(pairDirectory);

                if (_tradeFiles.ContainsKey(market))
                {
                    _tradeFiles[market].Close();
                    _tradeFiles[market].Dispose();
                    _tradeFiles[market] = null;
                }

                _tradeFileTimes[market] = quantizedMilliseconds;
                _tradeFiles[market] = File.Exists(tradeFilePath)
                    ? TeaFile<HistoricTrade>.Append(tradeFilePath)
                    : TeaFile<HistoricTrade>.Create(tradeFilePath);
            }

            return _tradeFiles[market];
        }
    }
}