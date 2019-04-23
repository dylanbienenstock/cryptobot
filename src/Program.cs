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

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<CommandLineArguments>(args)
                .WithParsed<CommandLineArguments>(o => Options = o);

            AppDomain.CurrentDomain.ProcessExit += (_, __) => OnProcessExit();
            Console.CancelKeyPress +=  (_, __) => OnProcessExit();
            Journal.Log($"[!] Process started at {DateTime.Now.ToString()}");
            Storage.Initialize();

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
            Journal.Log($"[!] Process exited at {DateTime.Now.ToString()}");
            Journal.Log($"\n{new string('#', 128)}\n");
        }

        public static async void CreateExchangeNetwork()
        {
            Network = new ExchangeNetwork
            (
                exchanges: new Exchange[]
                {
                    new CoinbasePro(),
                    // new Gemini(),
                    // new Binance()
                }
            );
            
            await Network.Connect();

            OnExchangeNetworkConnected();

            // Console.Clear();
            // var scriptPath = "/CryptoBot/src/TradingStrategyLanguage/Scripts/renko.tsl";
            // var tslScript = File.ReadAllText(Path.Join(Environment.CurrentDirectory, scriptPath));
            // (new TSLTranspiler()).Transpile(tslScript);
        }

        public static void OnExchangeNetworkConnected()
        {
            var btcusd = Network
                .GetExchange("Coinbase Pro")
                .GetMarket(new CurrencyPair(Currency.BTC, Currency.USD));

            // var rsi = new RelativeStrengthIndex(TradingPeriod.Field.Close, btcusd.TradingPeriods);

            // TcpServer server = new TcpServer(IPAddress.Loopback, 8008);
            // server.Start(Network);

            // var cointegrator = new Cointegrator(Network);
            // var pathfinder = new Pathfinder(Network);
        }
    }
}
