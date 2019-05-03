using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Orders;
using TeaTime;

namespace CryptoBot
{
    public static class Storage
    {
        public struct StorageEntry
        {
            public decimal Price;
            public decimal Amount;
            public OrderSide Side;
            public TeaTime.Time Time;

            public StorageEntry(CurrencyOrder order)
            {
                Price  = order.Price;
                Amount = order.Amount;
                Side   = order.Side;
                Time   = new TeaTime.Time(order.Time.Ticks);
            }

            public StorageEntry(CurrencyTrade trade)
            {
                Price  = trade.Price;
                Amount = trade.Amount;
                Side   = trade.Side;
                Time   = new TeaTime.Time(trade.Time.Ticks);
            }
        }

        private static int _fileDurationMilliseconds = 30000;

        private static Dictionary<string, Dictionary<Market, TeaFile<StorageEntry>>> _storageFiles;
        private static Dictionary<string, Dictionary<Market, double>> _storageFileTimes;
        private static string _storageDirectory;

        public static void Initialize()
        {
            _storageFiles     = new Dictionary<string, Dictionary<Market, TeaFile<StorageEntry>>>();
            _storageFileTimes = new Dictionary<string, Dictionary<Market, double>>();
            _storageDirectory = Environment.GetEnvironmentVariable("StorageDir")
                ?? Path.Join(Environment.CurrentDirectory, "Storage");

            if (!Directory.Exists(_storageDirectory))
                Directory.CreateDirectory(_storageDirectory);
        }

        public static void RecordOrder(Market market, CurrencyOrder order)
        {
            var orderRecord = new StorageEntry(order);
            GetOrderFile(market, order).Write(orderRecord);
        }

        public static void RecordTrade(Market market, CurrencyTrade trade)
        {
            var tradeRecord = new StorageEntry(trade);
            GetTradeFile(market, trade).Write(tradeRecord);
        }

        private static TeaFile<StorageEntry> GetTradeFile(Market market, CurrencyTrade trade) =>
            GetStorageFile("Trades", market, trade.Time.GetQuantizedMilliseconds(_fileDurationMilliseconds));

        private static TeaFile<StorageEntry> GetOrderFile(Market market, CurrencyOrder order) =>
            GetStorageFile("Orders", market, order.Time.GetQuantizedMilliseconds(_fileDurationMilliseconds));

        private static TeaFile<StorageEntry> GetStorageFile(string section, Market market, double quantizedMilliseconds)
        {
            if (!_storageFiles.ContainsKey(section))
            {
                _storageFiles[section] = new Dictionary<Market, TeaFile<StorageEntry>>();
                _storageFileTimes[section] = new Dictionary<Market, double>();
            }

            if (!_storageFileTimes[section].ContainsKey(market) ||
                _storageFileTimes[section][market] != quantizedMilliseconds)
            {
                var symbol            = market.Pair.ToString("-");
                var quantizedDateTime = DateTimeExtension.FromMilliseconds(quantizedMilliseconds);
                var exchangeDirectory = Path.Join(_storageDirectory, market.Exchange.Name);
                var sectionDirectory  = Path.Join(exchangeDirectory, section);
                var pairDirectory     = Path.Join(sectionDirectory, symbol);
                var currentHour       = $"{(quantizedDateTime.Hour < 10 ? "0" : "")}{quantizedDateTime.Hour}";
                var currentMinute     = $"{(quantizedDateTime.Minute < 10 ? "0" : "")}{quantizedDateTime.Minute}";
                var storageFileName     = $"{symbol}-{quantizedDateTime.ToString("MMM-dd-yyyy")}-T{currentHour}{currentMinute}";
                var storageFilePath     = Path.Join(pairDirectory, storageFileName + ".dirty.tea");
                    
                if (!Directory.Exists(exchangeDirectory))
                    Directory.CreateDirectory(exchangeDirectory);

                if (!Directory.Exists(sectionDirectory))
                    Directory.CreateDirectory(sectionDirectory);

                if (!Directory.Exists(pairDirectory))
                    Directory.CreateDirectory(pairDirectory);

                if (_storageFiles[section].ContainsKey(market))
                    FinishStorageFile(_storageFiles[section][market]);

                _storageFileTimes[section][market] = quantizedMilliseconds;
                _storageFiles[section][market] = File.Exists(storageFilePath)
                    ? TeaFile<StorageEntry>.Append(storageFilePath)
                    : TeaFile<StorageEntry>.Create(storageFilePath);
            }

            return _storageFiles[section][market];
        }

        private static void FinishStorageFile(TeaFile<StorageEntry> tradeFile)
        {
            var oldTradeFilePath = tradeFile.Name;
            var newTradeFilePath = tradeFile.Name
                .Substring(0, tradeFile.Name.Length - 4)
                + ".clean.tea";

            tradeFile.Flush();
            tradeFile.Close();
            tradeFile.Dispose();
            tradeFile = null;

            var tradeFileReadMode = TeaFile<StorageEntry>.OpenRead(oldTradeFilePath);
            var trades = new List<StorageEntry>(tradeFileReadMode.Items);
            var sortedTrades = trades.OrderBy(trade => trade.Time.Ticks);

            tradeFileReadMode.Close();
            tradeFileReadMode.Dispose();
            tradeFileReadMode = null;

            File.Delete(oldTradeFilePath);
            File.Delete(newTradeFilePath);

            var sortedTradeFile = TeaFile<StorageEntry>.Create(newTradeFilePath);

            foreach (var sortedTrade in sortedTrades)
                sortedTradeFile.Write(sortedTrade);

            sortedTradeFile.Flush();
            sortedTradeFile.Close();
            sortedTradeFile.Dispose();
            sortedTradeFile = null;
        }
    }
}