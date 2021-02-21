using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Miner.Models;
using Priority_Queue;

namespace Miner
{
    public class LicenseWorker
    {
        private readonly Client _client;
        private readonly ILogger<LicenseWorker> _logger;
        private readonly ConcurrentBag<int> _coins;
        private readonly ConcurrentBag<License> _licenses;
        private readonly List<int> _empty = new List<int>();
        public LicenseWorker(
            Client client,
            ILogger<LicenseWorker> logger,
            ConcurrentBag<int> coins,
            ConcurrentBag<License> licenses)
        {
            _client = client;
            _logger = logger;
            _coins = coins;
            _licenses = licenses;
        }

        public async Task Doit()
        {
            while(true) {
                if (_licenses.Count >= 7) {
                    await Task.Yield();
                    continue;
                }

                License license = null;
                List<int> coins = _empty;
                if (_licenses.Count < 3 && _coins.Count > 0)
                {
                    coins = new List<int>();
                    int coin = 0;
                    while (!_coins.TryTake(out coin))
                    {
                        await Task.Yield();
                    }
                    coins.Add(coin);
                }

                do
                {
                    license = await _client.BuyLicenseAsync(coins);
                } while(license == null);

                _licenses.Add(license);
            }
        }
    }
}