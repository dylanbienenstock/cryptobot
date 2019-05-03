using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Indicators;
using InfluxClient;
using Newtonsoft.Json;

namespace CryptoBot
{
    public struct InfluxQueryResponse
    {
        [JsonProperty("results")]
        public InfluxQueryResult[] Results;
    }

    public struct InfluxQueryResult
    {
        [JsonProperty("series")]
        public InfluxQueryResultSeries[] Series;
    }

    public struct InfluxQueryResultSeries
    {
        [JsonProperty("name")]
        public string Name;

        [JsonProperty("columns")]
        public List<string> Columns;

        [JsonProperty("values")]
        public dynamic[][] Values;
    }

    public class InfluxStorage
    {
        private InfluxManager _influx;
        private string _dbName;

        public InfluxStorage()
        {
            _dbName = "trading_periods_db";
            _influx = new InfluxManager("http://localhost:8086", _dbName, true);
        }

        public void WriteTradingPeriod(Exchange exchange, CurrencyPair pair, TradingPeriod period)
        {
            var data = new Measurement("periods", period.Time)
                .AddTag("exchange", exchange.Name)
                .AddTag("pair", pair.ToString())
                .AddField("open",   (float)period.Open)
                .AddField("high",   (float)period.High)
                .AddField("low",    (float)period.Low)
                .AddField("close",  (float)period.Close)
                .AddField("volume", (float)period.Volume);

            _influx.Write(data);
        }

        public async Task<InfluxQueryResponse> Query(string query)
        {
            var raw      = await _influx.QueryJSON(query);
            var json     = await raw.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<InfluxQueryResponse>(json);

            return response;
        }

        public async Task<List<TradingPeriod>> GetTradingPeriod(string query)
        {
            var response  = await Query(query);

            if (response.Results[0].Series == null)
                return new List<TradingPeriod>();

            var series    = response.Results[0].Series[0];
            var columnMap = new Dictionary<string, int>();

            for (int i = 0; i < series.Columns.Count; i++)
                columnMap[series.Columns[i].Replace("last_", "")] = i;

            DateTime DateTimeColumn(object[] values, string column) =>
                (DateTime.Parse(values[columnMap[column]].ToString()));

            decimal NumberColumn(object[] values, string column) =>
                Convert.ToDecimal(values[columnMap[column]]);

            return series.Values.Select
            (
                values => 
                {
                    var shit = DateTimeColumn(values, "time");

                    return new TradingPeriod
                    (
                        time:   DateTimeColumn(values, "time"),
                        open:   NumberColumn(values, "open"),
                        high:   NumberColumn(values, "high"),
                        low:    NumberColumn(values, "low"),
                        close:  NumberColumn(values, "close"),
                        volume: NumberColumn(values, "volume")
                    );
                }
            ).ToList();
        }

        public void ScrapeHistory(Exchange exchange, CurrencyPair pair)
        {
            var thread = new Thread(_ScrapeHistory);
            thread.Start();

            async void _ScrapeHistory()
            {
                var lastPeriodQuery  = "SELECT last(*) FROM periods LIMIT 1";
                var lastPeriodSeries = (await GetTradingPeriod(lastPeriodQuery));
                var pairListingTime  = (await exchange.FetchPairListingDate(pair)).Quantize(60000);
                var lastPeriodTime   = lastPeriodSeries.Count > 0 ? lastPeriodSeries[0].Time : pairListingTime;
                var cursorTime       = lastPeriodTime;
                var startTime        = DateTime.UtcNow;
                var symbol           = pair.ToString("");

                while (cursorTime < startTime)
                {
                    double cursorMillis   = (cursorTime - DateTime.UnixEpoch).TotalMilliseconds / 1000;
                    var historicalPeriods = await exchange.FetchHistoricalTradingPeriods(symbol, cursorMillis, 60000, 256);
                    
                    Console.WriteLine(cursorMillis + " " + historicalPeriods.Last().Time.ToString());

                    foreach (var period in historicalPeriods)
                        WriteTradingPeriod(exchange, pair, period);
                    
                    cursorTime = historicalPeriods.Last().Time;
                }
            }
        }
    }
}