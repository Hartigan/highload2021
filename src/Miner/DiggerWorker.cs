using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Miner.Models;
using Priority_Queue;

namespace Miner
{
    public class DiggerWorker
    {
        private readonly Client _client;
        private readonly ILogger<DiggerWorker> _logger;
        private readonly ConcurrentBag<License> _licenses;
        private readonly ConcurrentBag<string> _treasures;
        private readonly ConcurrentBag<MyNode> _cells;
        public DiggerWorker(
            Client client,
            ILogger<DiggerWorker> logger,
            ConcurrentBag<License> licenses,
            ConcurrentBag<string> treasures,
            ConcurrentBag<MyNode> cells)
        {
            _client = client;
            _logger = logger;
            _licenses = licenses;
            _treasures = treasures;
            _cells = cells;
        }

        public async Task Doit()
        {
            License license;

            while(!_licenses.TryTake(out license))
            {
                await Task.Yield();
            }
            
            while(true) {
                MyNode node = null;
                while(!_cells.TryTake(out node))
                {
                    await Task.Yield();
                }
                
                if (license == null) {
                    while(!_licenses.TryTake(out license))
                    {
                        await Task.Yield();
                    }
                }

                var dig = new Dig()
                {
                    LicenseId = license.Id ?? 0,
                    PosX = node.Report.Area.PosX,
                    PosY = node.Report.Area.PosY,
                    Depth = node.Depth
                };

                var treasures = await _client.DigAsync(dig);

                node.Depth++;
                license.DigUsed++;

                if (treasures != null)
                {
                    node.Report.Amount -= treasures.Count;
                    foreach(var treasure in treasures)
                    {
                        _treasures.Add(treasure);
                    }
                }

                if (node.Report.Amount > 0 && node.Depth <= 10)
                {
                    _cells.Add(node);
                }

                if (license.DigUsed >= license.DigAllowed) {
                    license = null;
                }
            }
        }
    }
}