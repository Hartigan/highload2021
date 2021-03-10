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

        public enum LicenseType
        {
            Free,
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

        private int _pendingTreasures = 0;

        class Treasure
        {
            public string Value { get; set; }
            public int Depth { get; set; }
        }

        private int[] _treasureCoins = new int[10];
        private int[] _treasureCounts = new int[10];

        private async Task SellAsync(Treasure treasure, ConcurrentBag<int> myCoins, Client client)
        {
            List<int> coins = await client.CashAsync(treasure.Value);

            _treasureCoins[treasure.Depth - 1] += coins.Count;
            _treasureCounts[treasure.Depth - 1]++;
            System.Threading.Interlocked.Decrement(ref _pendingTreasures);

            if (myCoins.Count > 1000) {
                return;
            }

            foreach(var coin in coins) {
                myCoins.Add(coin);
            }
        }

        public async Task CheckTreasures()
        {
            while(true)
            {
                await Task.Delay(30000);
                float[] s = new float[10];
                for(int i = 0; i < 10; i++)
                {
                    s[i] = _treasureCoins[i] * 1.0f / _treasureCounts[i];
                }
                _logger.LogDebug($"P: {_pendingTreasures}, S: {s[0]:F2} {s[1]:F2} {s[2]:F2} {s[3]:F2} {s[4]:F2} {s[5]:F2} {s[6]:F2} {s[7]:F2} {s[8]:F2} {s[9]:F2}");
            }
        } 

        private void MoveNodes(List<MyNode> source, List<MyNode> destination, int depth)
        {
            destination.AddRange(source.Where(x => x.Depth > depth));
            source.RemoveAll(x => x.Depth > depth);
        }

        public async Task Doit(LicenseType licenseType)
        {
            ConcurrentBag<int> myCoins = new ConcurrentBag<int>();
            Stack<Task> cashTasks = new Stack<Task>();

            int[] counts = new int[10];

            Client client = _clientFactory.Create();

            List<MyNode> nodes = new List<MyNode>();

            List<Treasure> treasures = new List<Treasure>();

            while(true) {

                License license = await GetLicenseAsync(myCoins, client, licenseType);

                if (nodes.Count < license.DigAllowed)
                {
                    await _explorerWorker.FindCells(nodes, license.DigAllowed);
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


                treasures.Clear();
                for(int i = 0; i < results.Length; ++i)
                {
                    if (results[i].Count > 0)
                    {
                        foreach(var treasure in results[i])
                        {
                            treasures.Add(new Treasure() {
                                Value = treasure,
                                Depth = nodes[i].Depth
                            });
                        }
                    }

                    counts[nodes[i].Depth - 1] += results[i].Count;
                    nodes[i].Depth++;
                    nodes[i].Report.Amount -= results[i].Count;
                }

                nodes.RemoveAll(x => x.Depth > 10 || x.Report.Amount <= 0);

                System.Threading.Interlocked.Add(ref _pendingTreasures, treasures.Count());
                foreach(var treasure in treasures)
                {
                    cashTasks.Push(SellAsync(treasure, myCoins, client));
                }
            }
        }
    }
}