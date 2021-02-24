using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            };


            _httpClient = new HttpClient(socketsHandler);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

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

                        return await Parse<List<int>>(response.Content);
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

                        return await Parse<License>(response.Content);
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

                    return await Parse<List<License>>(response.Content);
                }
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
                        using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                        {
                            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                            {
                                //var error = await Parse<Error>(response.Content);
                                //_logger.LogError($"DigAsync Code = {error.Code}, Message = {error.Message}");
                                return null;
                            }


                            return await Parse<List<string>>(response.Content);
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

                        return await Parse<Explore>(response.Content);
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