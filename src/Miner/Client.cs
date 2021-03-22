using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Miner.Models;

namespace Miner
{
    public class ClientFactory
    {
        private string _baseUrl;
        private ILogger<Client> _logger;
        private Client _client;

        public ClientFactory(string baseUrl, ILogger<Client> logger)
        {
            _baseUrl = "http://" + baseUrl + ":8000";
            _logger = logger;
            _client = new Client(_baseUrl, _logger);
        }

        public Client Create()
        {
            return _client;
        }
    }



    public class Client
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly ILogger<Client> _logger;

        public Client(string baseUrl, ILogger<Client> logger)
        {
            var socketsHandler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.Zero,
                PooledConnectionIdleTimeout = TimeSpan.Zero,
                ConnectCallback = async (ctx, cancellationToken) =>
                {
                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    socket.NoDelay = true;
                    socket.UseOnlyOverlappedIO = true;
                    await socket.ConnectAsync(ctx.DnsEndPoint);
                    var networkStream = new NetworkStream(socket, true);
                    return networkStream;
                },
            };

            _httpClient = new HttpClient(socketsHandler);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestVersion = HttpVersion.Version10;
            _httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

            _baseUrl = baseUrl;
            _logger = logger;
        }

        private Task<T> DoRequest<T>(Func<Task<T>> func)
        {
            return Task.Run(async () => {
                T result = default(T);
                do
                {
                    try
                    {
                        result = await func();
                    }
                    catch(Exception)
                    {
                    }
                } while(result == null);
                return result;
            });
        }

        private async Task<T> Parse<T>(HttpContent content)
        {
            using(var stream = await content.ReadAsStreamAsync())
            {
                return await JsonSerializer.DeserializeAsync<T>(stream);
            }
        }

        private HttpContent ToHttpContent<T>(T obj)
        {
            return JsonContent.Create(obj);
        }

        private static int _cashFailCounter = 0;
        public Task<List<int>> CashAsync(string treasure)
        {
            return DoRequest(async () => {
                using (var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/cash"))
                {
                    request.Content = ToHttpContent(treasure);
                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            ++_cashFailCounter;
                            if (_cashFailCounter % 100 == 0)
                            {
                                var error = await Parse<Error>(response.Content);
                                _logger.LogError($"CashAsync Code = {error.Code}, Total = {_cashFailCounter}");
                            }
                            return null;
                        }

                        return await response.Content.ReadFromJsonAsync<List<int>>();
                    }
                    
                }
            });
        }

        private static int _licensesTotalFailes = 0;

        public Task<License> BuyLicenseAsync(List<int> coins)
        {
            return DoRequest(async () => {
                using (var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/licenses"))
                {
                    request.Content = ToHttpContent(coins);
                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            ++_licensesTotalFailes;
                            if (_licensesTotalFailes % 100 == 0)
                            {
                                var error = await Parse<Error>(response.Content);
                                _logger.LogError($"BuyLicenseAsync Code = {error.Code}, Message = {error.Message}, Total = {++_licensesTotalFailes}");
                            }
                            
                            return null;
                        }

                        return await response.Content.ReadFromJsonAsync<License>();
                    }
                }
            });
            }
            
        public async Task<List<License>> GetLicensesAsync()
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + "/licenses"))
            {
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        var error = await Parse<Error>(response.Content);
                        _logger.LogError($"GetLicensesAsync Code = {error.Code}, Message = {error.Message}");
                        return null;
                    }

                    return await response.Content.ReadFromJsonAsync<List<License>>();
                }
            }
        }

        private readonly List<string> _empty = new List<string>(0);

        private TimeSpan[] _digTimings = new TimeSpan[10];
        private int[] _digCounts = new int[10];

        public async Task PrintStats()
        {
            while(true)
            {
                await Task.Delay(30000);
                double[] f = new double[10];
                for(int i = 0 ; i < 10; ++i)
                {
                    f[i] = _digTimings[i].TotalMilliseconds / _digCounts[i];
                }
                _logger.LogDebug($"DR: {f[0]:F2} {f[1]:F2} {f[2]:F2} {f[3]:F2} {f[4]:F2} {f[5]:F2} {f[6]:F2} {f[7]:F2} {f[8]:F2} {f[9]:F2}");
            }
        }

        public Task<List<string>> DigAsync(Dig dig)
        {
            return Task.Run(async () => {
                donext:
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/dig"))
                    {
                        request.Content = ToHttpContent(dig);
                        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                        sw.Start();
                        using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                        {
                            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                            {
                                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                                {
                                    sw.Stop();
                                    _digTimings[dig.Depth - 1] += sw.Elapsed;
                                    _digCounts[dig.Depth - 1]++;
                                    return _empty;
                                }

                                //var error = await Parse<Error>(response.Content);
                                //_logger.LogError($"DigAsync Code = {error.Code}, Message = {error.Message}");
                                return null;
                            }

                            sw.Stop();
                            _digTimings[dig.Depth - 1] += sw.Elapsed;
                            _digCounts[dig.Depth - 1]++;
                            return await response.Content.ReadFromJsonAsync<List<string>>();
                        }
                    }
                }
                catch
                {
                    goto donext;
                }
            });
        }

        private static int _exploreFailCounter = 0;

        public Task<Explore> ExploreAsync(Area area)
        {
            return DoRequest(async () => {
                using (var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/explore"))
                {
                    request.Content = ToHttpContent(area);
                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            ++_exploreFailCounter;
                            if (_exploreFailCounter % 100 == 0)
                            {
                                var error = await Parse<Error>(response.Content);
                                _logger.LogError($"ExploreAsync Code = {error.Code}, Message = {error.Message}, Total = {_exploreFailCounter}");
                            }
                            return null;
                        }

                        return await response.Content.ReadFromJsonAsync<Explore>();
                    }
                }
            });
        }

        public async Task<Wallet> GetBalanceAsync()
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + "/balance"))
            {
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        var error = await Parse<Error>(response.Content);
                        _logger.LogError($"GetBalanceAsync Code = {error.Code}, Message = {error.Message}");
                        return null;
                    }

                    return await Parse<Wallet>(response.Content);
                }
            }
        }

        public async Task<bool> HealthCheckAsync()
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + "/health-check"))
            {
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        var error = await Parse<Error>(response.Content);
                        _logger.LogError($"HealthCheckAsync Code = {error.Code}, Message = {error.Message}");
                        return false;
                    }

                    return true;
                }
            }
            
        }
    }
}