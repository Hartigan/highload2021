using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Miner.Models;

namespace Miner
{
    public class DiggerWorker
    {
        private readonly ClientFactory _clientFactory;
        private readonly ILogger<DiggerWorker> _logger;
        private readonly ConcurrentQueue<MyNode> _cells;
        private readonly List<int> _empty = new List<int>();
        public DiggerWorker(
            ClientFactory clientFactory,
            ILogger<DiggerWorker> logger,
            ConcurrentQueue<MyNode> cells)
        {
            _clientFactory = clientFactory;
            _logger = logger;
            _cells = cells;
        }

        private async Task<License> GetLicenseAsync(Stack<int> myCoins, Client client, bool free)
        {
            List<int> coins = _empty;

            if (!free) {
                if (myCoins.Count > 0)
                {
                    coins = new List<int>();
                    coins.Add(myCoins.Pop());
                }
            }

            return await client.BuyLicenseAsync(coins);
        }

        private async Task SellAsync(string treasure, Stack<int> myCoins, Client client)
        {
            List<int> coins = await client.CashAsync(treasure);

            if (myCoins.Count > 100) {
                return;
            }

            foreach(var coin in coins) {
                myCoins.Push(coin);
            }
        }

        public async Task Doit()
        {
            Stack<int> myCoins = new Stack<int>();
            Stack<Task> cashTasks = new Stack<Task>();

            Client licenseClient = _clientFactory.Create();
            Client cashClient = _clientFactory.Create();

            Client[] diggerClients = {
                _clientFactory.Create(),
                _clientFactory.Create(),
                _clientFactory.Create(),
                _clientFactory.Create(),
                _clientFactory.Create(),
            };

            License license = null; 
            int waitCellCounter = 0;

            List<MyNode> currentNodes = new List<MyNode>(5);
            
            while(true) {

                while (currentNodes.Count > 3)
                {
                    _cells.Enqueue(currentNodes[currentNodes.Count - 1]);
                    currentNodes.RemoveAt(currentNodes.Count - 1);
                }

                while(currentNodes.Count < 3)
                {
                    MyNode node = null;
                    while(!_cells.TryDequeue(out node))
                    {
                        ++waitCellCounter;
                        if (waitCellCounter % 400000 == 0) {
                            _logger.LogDebug($"Wait cell total count = {waitCellCounter}");
                        }
                        await Task.Yield();
                    }
                    currentNodes.Add(node);
                }

                {
                    MyNode node = null;

                    if (_cells.TryDequeue(out node))
                    {
                        currentNodes.Add(node);

                        while(!_cells.TryDequeue(out node))
                        {
                            ++waitCellCounter;
                            if (waitCellCounter % 400000 == 0) {
                                _logger.LogDebug($"Wait cell total count = {waitCellCounter}");
                            }
                            await Task.Yield();
                        }

                        currentNodes.Add(node);
                    }

                }

                switch(currentNodes.Count)
                {
                    case 5:
                        license = await GetLicenseAsync(myCoins, licenseClient, false);
                        break;
                    case 3:
                        license = await GetLicenseAsync(myCoins, licenseClient, true);
                        break;
                    default:
                        _logger.LogCritical($"Stupid error");
                        break;
                }

                while (currentNodes.Count > license.DigAllowed)
                {
                    _cells.Enqueue(currentNodes[currentNodes.Count - 1]);
                    currentNodes.RemoveAt(currentNodes.Count - 1);
                }


                var results = await Task.WhenAll(currentNodes.Select(async (node, index) => {
                    var dig = new Dig()
                    {
                        LicenseId = license.Id ?? 0,
                        PosX = node.Report.Area.PosX,
                        PosY = node.Report.Area.PosY,
                        Depth = node.Depth
                    };
                    node.Depth++;
                    var result = await diggerClients[index].DigAsync(dig);
                    if (result != null) {
                        node.Report.Amount -= result.Count;
                    }
                    return result;
                }));

                var treasures = results.SelectMany(x => x == null ? Enumerable.Empty<string>() : x);

                currentNodes.RemoveAll(x => x.Depth > 10 || x.Report.Amount <= 0);

                foreach(var treasure in treasures)
                {
                    cashTasks.Push(SellAsync(treasure, myCoins, cashClient));
                }
            }
        }
    }
}