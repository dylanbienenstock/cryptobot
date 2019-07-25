using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Priority_Queue;

namespace CryptoBot.Exchanges
{
    public class UriParams : Dictionary<string, object> { }

    public class HttpBackoffClient
    {
        private string _baseUri;
        private HttpClient _httpClient;
        private Func<int, HttpResponseMessage, int?> _backoffCallback;
        private Func<int, HttpResponseMessage, int?> _defaultBackoffCallback;
        private Priority_Queue.SimplePriorityQueue<Tuple<HttpRequestMessage, TaskCompletionSource<string>, Type>> _requestQueue;
        private Thread _requestThread;
        private Random _random;
        private bool _running;

        public HttpBackoffClient(string baseUri)
        {
            _baseUri = baseUri;
            _httpClient = new HttpClient();
            _requestQueue = new Priority_Queue.SimplePriorityQueue<Tuple<HttpRequestMessage, TaskCompletionSource<string>, Type>>();
            _random = new Random();
            _requestThread = new Thread(SendRequests);

            _defaultBackoffCallback = (attempts, _) => 
                attempts ^ 2 * 256 + _random.Next(0, 128);

            _backoffCallback = _defaultBackoffCallback;

            if (!_baseUri.EndsWith('/')) _baseUri += '/';
        }

        public void SetBackoff(Func<int, HttpResponseMessage, int?> callback) => 
            _backoffCallback = callback;

        private async Task<T> SendRaw<T>(HttpRequestMessage requestMessage, int priority = 1)
        {
            requestMessage.RequestUri = new Uri(_baseUri + requestMessage.RequestUri);
            var httpCompletionSource = new TaskCompletionSource<string>();
            var request = Tuple.Create(requestMessage, httpCompletionSource, typeof(T));

            lock (_requestQueue)
            {
                _requestQueue.Enqueue(request, priority);

                if (!_running)
                {
                    _running = true;
                    _requestThread = new Thread(SendRequests);
                    _requestThread.Start();
                }
            }

            var responseBody = await request.Item2.Task;

            try
            {
                return JsonConvert.DeserializeObject<T>(responseBody);
            } catch (Exception ex) {
                Console.WriteLine(request.Item1.RequestUri);
                throw ex;
            }
        }

        public async Task<T> Get<T>(string endpoint, UriParams parameters = null, int priority = 1)
        {
            string fullUri = endpoint;
            char delimiter = '?';

            if (parameters != null)
            {
                foreach (var uriParam in parameters)
                {
                    fullUri += $"{delimiter}{uriParam.Key}={uriParam.Value.ToString()}";
                    delimiter = '&';
                }
            }

            var request = new HttpRequestMessage(HttpMethod.Get, fullUri);

            return await SendRaw<T>(request, priority);
        }

        private async void SendRequests()
        {
            while (_requestQueue.TryDequeue(out var request))
            {
                int attempts = 0;
                Exception lastException = null;
                HttpResponseMessage response = null;
                string responseBody = null;

                while (attempts <= 5 && response == null)
                {
                    try
                    {
                        var requestCopy = new HttpRequestMessage(request.Item1.Method, request.Item1.RequestUri);
                        foreach (var header in request.Item1.Headers)
                            requestCopy.Headers.Add(header.Key, header.Value);

                        response = await _httpClient.SendAsync(requestCopy);
                        response.EnsureSuccessStatusCode();
                        responseBody = await response.Content.ReadAsStringAsync();
                    }
                    catch
                    {
                        int? delay = _backoffCallback.Invoke(attempts++, response);
                        
                        if (delay == null) 
                            delay = _defaultBackoffCallback.Invoke(attempts++, response);

                        Thread.Sleep((int)delay);
                    }
                }

                if (response == null || responseBody == null)
                {
                    if (lastException != null)
                        throw lastException;
                }

                request.Item2.SetResult(responseBody);
            }

            _running = false;
        }
    }
}