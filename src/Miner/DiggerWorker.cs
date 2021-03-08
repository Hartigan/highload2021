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
        private readonly ExplorerWorker _explorerWorker;
        private readonly List<int> _empty = new List<int>();
        public DiggerWorker(
            ClientFactory clientFactory,
            ILogger<DiggerWorker> logger,
            ExplorerWorker explorerWorker)
        {
            _clientFactory = clientFactory;
            _logger = logger;
            _explorerWorker = explorerWorker;
        }

        enum LicenseType
        {
            One,
            Six,
            Eleven,
            TwentyOne
        }

        private Task<License> GetLicenseAsync(ConcurrentBag<int> myCoins, Client client, LicenseType type)
        {
            List<int> coins = _empty;

            if (myCoins.Count > 0)
            {
                coins = new List<int>();
                int count = 0;
                switch(type)
                {
                    case LicenseType.TwentyOne:
                        if (myCoins.Count >= 21)
                        {
                            count = 21;
                        }
                        else
                        {
                            return GetLicenseAsync(myCoins, client, LicenseType.Eleven);
                        }
                        break;
                    case LicenseType.Eleven:
                        if (myCoins.Count >= 11)
                        {
                            count = 11;
                        }
                        else
                        {
                            return GetLicenseAsync(myCoins, client, LicenseType.Six);
                        }
                        break;
                    case LicenseType.Six:
                        if (myCoins.Count >= 6)
                        {
                            count = 6;
                        }
                        else
                        {
                            return GetLicenseAsync(myCoins, client, LicenseType.One);
                        }
                        break;
                    case LicenseType.One:
                        if (myCoins.Count >= 1)
                        {
                            count = 1;
                        }
                        break;
                }
                
                for(int i = 0; i < count; ++i)
                {
                    int c = 0;
                    if (!myCoins.TryTake(out c))
                    {
                        _logger.LogDebug("buy license wtf");
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

            List<MyNode> currentNodes = new List<MyNode>();
            List<MyNode> deepNodes = new List<MyNode>();

            while(true) {

                License license = null;
                List<MyNode> nodes = null;
                if (deepNodes.Count >= 5)
                {
                    nodes = deepNodes;
                    license = await GetLicenseAsync(myCoins, client, LicenseType.One);
                }
                else
                {
                    nodes = currentNodes;
                    license = await GetLicenseAsync(myCoins, client, LicenseType.One);

                    if (nodes.Count < license.DigAllowed)
                    {
                        await _explorerWorker.FindCells(nodes, license.DigAllowed);
                    }
                }


                var results = await Task.WhenAll(
                    nodes
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
                    counts[nodes[i].Depth - 1] += results[i].Count;
                    nodes[i].Depth++;
                    nodes[i].Report.Amount -= results[i].Count;
                }

                var treasures = results.SelectMany(x => x);

                nodes.RemoveAll(x => x.Depth > 10 || x.Report.Amount <= 0);

                if (nodes == currentNodes)
                {
                    deepNodes.AddRange(currentNodes.Where(x => x.Report.Amount > 7));
                    currentNodes.RemoveAll(x => x.Report.Amount > 7);
                }

                foreach(var treasure in treasures)
                {
                    cashTasks.Push(SellAsync(treasure, myCoins, cashClient));
                }
            }
        }
    }
}