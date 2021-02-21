using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Miner
{
    public class ExchangerWorker
    {
        private readonly Client _client;
        private readonly ILogger<ExchangerWorker> _logger;
        private readonly ConcurrentBag<int> _coins;
        private readonly ConcurrentBag<string> _treasures;
        public ExchangerWorker(
            Client client,
            ILogger<ExchangerWorker> logger,
            ConcurrentBag<int> coins,
            ConcurrentBag<string> treasures)
        {
            _client = client;
            _logger = logger;
            _coins = coins;
            _treasures = treasures;
        }

        public async Task Doit()
        {
            while(true) {
                string treasure = null;
                while(!_treasures.TryTake(out treasure))
                {
                    await Task.Delay(1000);
                }

                List<int> coins = null;
                do
                {
                    coins = await _client.CashAsync(treasure);
                } while(coins == null);

                foreach(var coin in coins) {
                    _coins.Add(coin);
                }

            }
        }
    }
}