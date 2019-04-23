using System;
using System.Threading;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using Priority_Queue;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using CryptoBot;
using CryptoBot.Exchanges;
using CryptoBot.Exchanges.Currencies;
using CryptoBot.Exchanges.Orders;

namespace CryptoBot.TcpDebug
{
    public class TcpServer
    {
        private delegate void OrderHandler(CurrencyOrder order);
        private OrderHandler CurrencyOrdered;

        public IPAddress IP;
        public int Port;
        public Thread ListenThread;

        private ExchangeNetwork Network;
        private TcpListener Server;
        private SimplePriorityQueue<byte[]> SendQueue;
        private UpdateFilter UpdateFilter;
        private JsonSerializerSettings SerializerSettings;

        public TcpServer(IPAddress ip, int port)
        {
            IP = ip;
            Port = port;

            SerializerSettings = new JsonSerializerSettings
            {
                Error = delegate(object sender, ErrorEventArgs args)
                {
                    WriteDebug("JSON ERROR: " + args.ErrorContext.Error.Message);
                    args.ErrorContext.Handled = true;
                }
            };
        }

        public void Start(ExchangeNetwork network)
        {
            Network = network;
            ListenThread = new Thread(Listen);
            UpdateFilter = new UpdateFilter(network);
            SendQueue = new SimplePriorityQueue<byte[]>();

            ListenThread.Start();
        }

        public void Send(dynamic message, int priority)
        {
            lock (SendQueue)
            {
                string messageJson = JsonConvert.SerializeObject(message, SerializerSettings);
                byte[] messageBytes = Encoding.UTF8.GetBytes(messageJson + '\n');

                SendQueue.Enqueue(messageBytes, priority);

                WriteDebug("SENT: " + messageJson);
            }
        }

        public void SendOrder(CurrencyOrder order)
        {
            if (!UpdateFilter.Allows(order)) return;

            Send(new
            {
                type = "update",
                exchangeName = order.Exchange.Name,
                symbol = order.Symbol,
                pair = order.Pair,
                side = order.Side == OrderSide.Bid ? "bid" : "ask",
                price = order.Price,
                amount = order.Amount,
                time = order.Time
            }, 2);
        }

        private void WriteDebug(string text)
        {
            // Console.WriteLine("[TcpServer] " + text);
        }

        private void Listen()
        {
            if (Server != null) return;
            
            CurrencyOrdered += order => SendOrder(order);
            Network.MergedOrderStream.Subscribe((order) => CurrencyOrdered(order));

            Server = new TcpListener(IP, Port);
            Server.Start();

            byte[] readBuffer = new byte[256];
            string readData = string.Empty;

            while (true)
            {
                TcpClient client = Server.AcceptTcpClient();
                var stream = client.GetStream();

                OnConnectionEstablished();

                try
                {
                    while (true)
                    {
                        lock (SendQueue)
                        {
                            Read(readBuffer, readData, stream);
                            Write(stream);
                        }

                        Thread.Sleep(10);
                    }
                }
                catch { }

                OnConnectionClosed();
            }
        }

        private async void Write(NetworkStream stream)
        {
            WriteDebug("BEGIN WRITE");

            if (SendQueue.Count > 0) {
                byte[] writeBuffer = SendQueue.Dequeue();
                await stream.WriteAsync(writeBuffer, 0, writeBuffer.Length);
                Console.WriteLine("SENT: " + Encoding.UTF8.GetString(writeBuffer));
            }
            
            WriteDebug("END WRITE");
        }

        private async void Read(byte[] readBuffer, string readData, NetworkStream stream)
        {
            WriteDebug("BEGIN READ");

            int readLength = 0;

            while (true)
            {
                stream.ReadTimeout = 100;
                readLength = await stream.ReadAsync(readBuffer, 0, readBuffer.Length);
                WriteDebug("READ " + readLength.ToString() + " BYTES");

                if (readLength == 0) break;

                string readChunk = Encoding.UTF8.GetString(readBuffer, 0, readLength);
                readData += readChunk;

                if (readChunk[readLength - 1] == '\n')
                {
                    string messageStr = readData.Substring(0, readData.Length - 1);
                    readData = String.Empty;

                    var message = JsonConvert.DeserializeObject<TcpRequest>(messageStr);
                    
                    WriteDebug("RECEIVED: " + messageStr);

                    OnMessageReceived(message);
                    break;
                }
            }

            WriteDebug("END READ");
        }

        private void OnMessageReceived(TcpRequest message)
        {
            WriteDebug("BEGIN PROCESS REQUEST");

            if (!TcpRequest.BodyTypes.ContainsKey(message.Type)) return;

            Type bodyType = TcpRequest.BodyTypes[message.Type];
            dynamic body = JsonConvert.DeserializeObject(message.Body, bodyType, SerializerSettings);

            WriteDebug("END PROCESS REQUEST");
            WriteDebug("BEGIN FORM RESPONSE");

            var response = GetResponse(message.Type, body);

            if (response != null)
            {
                var responsePriority = TcpResponse.Priority[message.Type];
                Send(response, responsePriority);
            }

            WriteDebug("END FORM RESPONSE");
        }

        private TcpResponse GetResponse(string type, dynamic body)
        {
            switch (type)
            {
                case "exchanges": return new ExchangesResponse(Network);
                case "snapshot" : return new SnapshotResponse(Network, body);
                case "filter"   : return UpdateFilter.SetFrom(body);
            }

            throw new Exception(string.Format("Message type \"{0}\" not supported", type));
        }

        private void OnConnectionEstablished() { }
        private void OnConnectionClosed() { }
    }
}