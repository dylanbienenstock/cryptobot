using System;
using Newtonsoft.Json;
using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Adapters;
using CryptoBot.TcpDebug;
using CryptoBot.Arbitrage;
using CryptoBot.Indicators;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.TradingStategyLanguage;
using CommandLine;

namespace CryptoBot
{
    class Program
    {
        public static CommandLineArguments Options;
        public static ExchangeNetwork Network;
        public static IndicatorManifold Indicators;
        public static InfluxStorage InfluxStorage;

        static void Main(string[] args)
        {
            var startTime = DateTime.Now;

            Parser.Default.ParseArguments<CommandLineArguments>(args)
                .WithParsed<CommandLineArguments>(o => Options = o);

            Storage.Initialize();
            TelegramBot.Initialize();

            InfluxStorage = new InfluxStorage();

            AppDomain.CurrentDomain.ProcessExit += (_, __) => OnProcessExit();
            Console.CancelKeyPress +=  (_, __) => OnProcessExit();
            Journal.Log($"[!] Process started at {startTime.ToString()}", echoTelegram: false);

            TelegramBot.Send
            (
                text: $"Process started at {startTime}",
                options: new TelegramBot.InlineKeyboard
                (
                    ("Terminate", _ =>
                    {
                        TelegramBot.Send($"Process terminated at {DateTime.Now}");
                        TelegramBot.RemoveOldInlineKeyboards();
                        Environment.Exit(0);
                    })
                )
            );

            try
            {
                Console.Clear();
                Console.CursorVisible = true;
                Console.CancelKeyPress += (_, __) => Console.CursorVisible = true;

                Console.WriteLine("CryptoBot v0.0\n");

                CreateExchangeNetwork();

                while (true) {
                    while (Console.KeyAvailable) Console.ReadKey(true);
                    ConsoleKeyInfo key = Console.ReadKey(true);
                }
            }
            catch (Exception ex)
            {
                Console.ResetColor();
                throw ex;
            }
        }

        public static void OnProcessExit()
        {
            TelegramBot.RemoveOldInlineKeyboards();
            Journal.Log($"[!] Process terminated at {DateTime.Now}", echoTelegram: false);
            Journal.Log($"\n{new string('#', 128)}\n");
        }

        public static async void CreateExchangeNetwork()
        {
            Network = new ExchangeNetwork
            (
                exchanges: new Exchange[]
                {
                    new Binance()
                },
                currencies: new Currency[]
                {
                    Currency.USDT
                },
                filter: CurrencyFilter.Either
            );
            
            await Network.Connect();

            OnExchangeNetworkConnected();
        }

        public static void OnExchangeNetworkConnected()
        {
            Network.Indicators.Use.MACD(DebugConst.Timescale);
            // Network.Indicators.Use.RSI(DebugConst.Timescale);

            // var pathfinder = new Pathfinder(Network);
            // pathfinder.Start();

            InfluxStorage.ScrapeHistory
            (
                exchange: Network.GetExchange("Binance"),
                pair: new CurrencyPair(Currency.BTC, Currency.USDT)
            );
        }
    }
}
