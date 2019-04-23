using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Reactive;
using System.Reactive.Linq;
using System.Collections.Generic;
using System.Reactive.Disposables;

namespace CryptoBot.Exchanges
{
    public class WebSocketFeed<T> : IDisposable
    {
        private IObservable<T> updateObservable;
        private List<IObserver<T>> observers;
        private ClientWebSocket webSocket;
        private bool connected;
        private CancellationTokenSource source;
        private CancellationToken token;
        private Uri feedUri;

        public bool Connected => connected;

        /// <summary>
        /// Constructs a new <c>WebSocketFeed</c>
        /// </summary>
        /// <param name="uri">
        /// URI of the remote web socket feed
        /// </param>
        public WebSocketFeed(string uri)
        {
            updateObservable = Observable.Create((IObserver<T> observer) => {
                observers.Add(observer);
                return Disposable.Empty;
            });

            observers = new List<IObserver<T>>();
            webSocket = new ClientWebSocket();
            connected = false;
            source = new CancellationTokenSource();
            token = source.Token;
            feedUri = new Uri(uri);
        }

        /// <summary>
        /// Connects the ClientWebSocket to the URI specified in the 
        /// <para>Sends the subscription message once connected</para>
        /// </summary>
        /// <param name="subMsg">
        /// Message to send when the connection established
        /// </param>
        /// <exception cref="System.Exception">
        /// Thrown when already connected
        /// </exception>
        public async void Connect(dynamic subMsg = null)
        {
            if (connected) throw new Exception("WebSocketFeed is already connected");

            connected = true;

            await webSocket.ConnectAsync(feedUri, token);

            if (subMsg != null) await Send(subMsg);

            while (webSocket.State == WebSocketState.Open)
            {
                string data = String.Empty;
                bool endOfMessage = false;

                while (!endOfMessage)
                {
                    var buffer = new ArraySegment<byte>(new byte[256]);
                    var result = await webSocket.ReceiveAsync(buffer, token);
                    
                    data += Encoding.UTF8.GetString(buffer);
                    endOfMessage = result.EndOfMessage;
                }

                T message = JsonConvert.DeserializeObject<T>(data);

                observers.ForEach(o => o.OnNext(message));
            }

            connected = false;
        }

        /// <summary>
        /// Subscribes to the internal update observable
        /// </summary>
        /// <param name="observer">
        /// Action to take when the observable emits
        /// </param>
        /// <returns>
        /// Returns a subscription to the internal update observable
        /// </returns>
        public IDisposable Subscribe(Action<T> observer)
        {
            return updateObservable.Subscribe(observer);
        }

        /// <summary>
        /// Serializes an object and sends it to the websocket server
        /// <para>(Used internally to send subscription requests)</para>
        /// </summary>
        /// <param name="message">
        /// Object to serialize and send
        /// </param>
        /// <returns></returns>
        public async Task Send(dynamic message)
        {
            string messageStr = JsonConvert.SerializeObject(message);
            byte[] messageBytes = Encoding.UTF8.GetBytes(messageStr);
            var messageSegment = new ArraySegment<byte>(messageBytes);

            await webSocket.SendAsync(
                messageSegment, 
                WebSocketMessageType.Text, 
                true, 
                token
            ); 
        }

        #region IDisposable implementation
        private bool isDisposed = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    webSocket.Abort();
                    webSocket.Dispose();
                }

                isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}