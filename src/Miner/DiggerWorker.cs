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

        private Task<License> GetLicenseAsync(ConcurrentBag<int> myCoins, Client client)
        {
            List<int> coins = _empty;

            if (myCoins.Count > 0)
            {
                coins = new List<int>();
                // if (myCoins.Count > 21)
                // {
                //     for(int i = 0; i < 21; ++i)
                //     {
                //         int c = 0;
                //         if (!myCoins.TryTake(out c))
                //         {
                //             _logger.LogDebug("buy license wtf 21");
                //         }
                //         coins.Add(c);
                //     }
                // }
                // else if (myCoins.Count > 11)
                // {
                //     for(int i = 0; i < 11; ++i)
                //     {
                //         int c = 0;
                //         if (!myCoins.TryTake(out c))
                //         {
                //             _logger.LogDebug("buy license wtf 11");
                //         }
                //         coins.Add(c);
                //     }
                // }
                // else if (myCoins.Count > 6)
                // {
                //     for(int i = 0; i < 6; ++i)
                //     {
                //         int c = 0;
                //         if (!myCoins.TryTake(out c))
                //         {
                //             _logger.LogDebug("buy license wtf 6");
                //         }
                //         coins.Add(c);
                //     }
                // }
                // else
                {
                    int c = 0;
                    if (!myCoins.TryTake(out c))
                    {
                        _logger.LogDebug("buy license wtf 1");
                    }
                    coins.Add(c);
                }
            }
            return client.BuyLicenseAsync(coins);
        }

        private async Task SellAsync(string treasure, ConcurrentBag<int> myCoins, Client client)
        {
            List<int> coins = await client.CashAsync(treasure);

            if (myCoins.Count > 1000) {
                return;
            }

            foreach(var coin in coins) {
                myCoins.Add(coin);
            }
        }

        public async Task Doit()
        {
            ConcurrentBag<int> myCoins = new ConcurrentBag<int>();
            Stack<Task> cashTasks = new Stack<Task>();

            int[] counts = new int[10];

            Client licenseClient = _clientFactory.Create();
            Client cashClient = _clientFactory.Create();
            Client client = _clientFactory.Create();

            int waitCellCounter = 0;

            List<MyNode> currentNodes = new List<MyNode>(5);
            
            while(true) {

                License license = await GetLicenseAsync(myCoins, client);

                while(currentNodes.Count < license.DigAllowed)
                {
                    MyNode node = null;
                    while(!_cells.TryDequeue(out node))
                    {
                        ++waitCellCounter;
                        if (waitCellCounter % 400000 == 0) {
                            //_logger.LogDebug($"Wait cell total count = {waitCellCounter}");
                            //var c = String.Join(" ", counts);
                            //_logger.LogDebug(c);
                        }
                        await Task.Yield();
                    }
                    currentNodes.Add(node);
                }

                var results = await Task.WhenAll(
                    currentNodes
                        .Take(license.DigAllowed)
                        .Select(node => new Dig()
                            {
                                LicenseId = license.Id ?? 0,
                                PosX = node.Report.Area.PosX,
                                PosY = node.Report.Area.PosY,
                                Depth = node.Depth
                        })
                        .Select(client.DigAsync));


                for(int i = 0; i < results.Length; ++i)
                {
                    counts[currentNodes[i].Depth - 1] += results[i].Count;
                    currentNodes[i].Depth++;
                    currentNodes[i].Report.Amount -= results[i].Count;
                }

                var treasures = results.SelectMany(x => x);

                currentNodes.RemoveAll(x => x.Depth > 10 || x.Report.Amount <= 0);

                foreach(var treasure in treasures)
                {
                    cashTasks.Push(SellAsync(treasure, myCoins, cashClient));
                }
            }
        }
    }
}