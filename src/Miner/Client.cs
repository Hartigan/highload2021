using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Miner.Models;

namespace Miner
{
    public class Client
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _baseUrl;
        private readonly ILogger<Client> _logger;

        public Client(string baseUrl, ILogger<Client> logger)
        {
            logger.LogError(baseUrl);
            _baseUrl = "http://" + baseUrl + ":8000";
            _logger = logger;
        }

        private void AddHeaders(HttpRequestMessage msg)
        {
            msg.Headers.Add("Accept", "application/json");
            //msg.Headers.Add("Content-Type", "application/json");
        }

        private async Task<T> Parse<T>(HttpContent content)
        {
            var stream = await content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<T>(stream);
        }

        public async Task<List<int>> CashAsync(string treasure)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/cash");
            AddHeaders(request);
            request.Content = JsonContent.Create(treasure);
            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var error = await Parse<Error>(response.Content);
                _logger.LogError($"CashAsync Code = {error.Code}, Message = {error.Message}");
                return null;
            }

            return await Parse<List<int>>(response.Content);
        }

        public async Task<License> BuyLicenseAsync(List<int> coins)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/licenses");
            AddHeaders(request);
            request.Content = JsonContent.Create(coins);
            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var error = await Parse<Error>(response.Content);
                _logger.LogError($"BuyLicenseAsync Code = {error.Code}, Message = {error.Message}");
                return null;
            }

            return await Parse<License>(response.Content);
        }

        public async Task<List<License>> GetLicensesAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + "/licenses");
            AddHeaders(request);
            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var error = await Parse<Error>(response.Content);
                _logger.LogError($"GetLicensesAsync Code = {error.Code}, Message = {error.Message}");
                return null;
            }

            return await Parse<List<License>>(response.Content);
        }

        public async Task<List<string>> DigAsync(Dig dig)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/dig");
            AddHeaders(request);
            request.Content = JsonContent.Create(dig);
            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var error = await Parse<Error>(response.Content);
                _logger.LogError($"DigAsync Code = {error.Code}, Message = {error.Message}");
                return null;
            }


            return await Parse<List<string>>(response.Content);
        }

        public async Task<Explore> ExploreAsync(Area area)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/explore");
            AddHeaders(request);
            request.Content = JsonContent.Create(area);
            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var error = await Parse<Error>(response.Content);
                _logger.LogError($"ExploreAsync Code = {error.Code}, Message = {error.Message}");
                return null;
            }


            return await Parse<Explore>(response.Content);
        }

        public async Task<Wallet> GetBalanceAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + "/balance");
            AddHeaders(request);
            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var error = await Parse<Error>(response.Content);
                _logger.LogError($"GetBalanceAsync Code = {error.Code}, Message = {error.Message}");
                return null;
            }

            return await Parse<Wallet>(response.Content);
        }

        public async Task<bool> HealthCheckAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + "/health-check");
            AddHeaders(request);
            var response = await _httpClient.SendAsync(request);
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