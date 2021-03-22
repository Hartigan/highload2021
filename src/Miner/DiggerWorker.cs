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
        private readonly Client _client;
        private readonly ILogger<DiggerWorker> _logger;
        private readonly ExplorerWorker _explorerWorker;
        private readonly List<int> _empty = new List<int>();
        public DiggerWorker(
            ClientFactory clientFactory,
            ILogger<DiggerWorker> logger,
            ExplorerWorker explorerWorker)
        {
            _client = clientFactory.Create();
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

        private Task<License> GetLicenseAsync(ConcurrentBag<int> myCoins, LicenseType type)
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
                            return GetLicenseAsync(myCoins, LicenseType.Eleven);
                        }
                        break;
                    case LicenseType.Eleven:
                        if (myCoins.Count >= 11)
                        {
                            count = 11;
                        }
                        else
                        {
                            return GetLicenseAsync(myCoins, LicenseType.Six);
                        }
                        break;
                    case LicenseType.Six:
                        if (myCoins.Count >= 6)
                        {
                            count = 6;
                        }
                        else
                        {
                            return GetLicenseAsync(myCoins, LicenseType.One);
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
            return _client.BuyLicenseAsync(coins);
        }

        private int _pendingTreasures = 0;

        class Treasure
        {
            public int PosX { get; set; }
            public int PosY { get; set; }
            public string Value { get; set; }
            public int Depth { get; set; }
        }

        private int[] _treasureCoins = new int[10];
        private int[] _treasureCounts = new int[10];

        private async Task SellAsync(Treasure treasure, ConcurrentBag<int> myCoins)
        {
            List<int> coins = await _client.CashAsync(treasure.Value);

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
                await Task.Delay(15000);
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

        private LicenseType GetLicenseType(Random rng, double[] w)
        {
            var v = rng.NextDouble();

            if (v < w[0])
            {
                return LicenseType.Free;
            }
            else if (v < w[1])
            {
                return LicenseType.One;
            }
            else if (v < w[2])
            {
                return LicenseType.Six;
            }
            else if (v < w[3])
            {
                return LicenseType.Eleven;
            }
            else
            {
                return LicenseType.TwentyOne;
            }
        }

        private async Task<List<MyNode>> ProcessExistedNode(MyNode node, int licenseId, ConcurrentBag<int> myCoins, int limit)
        {
            Dig dig = new Dig() {
                LicenseId = licenseId,
                PosX = node.Report.Area.PosX,
                PosY = node.Report.Area.PosY,
                Depth = node.Depth
            };

            var result = await _client.DigAsync(dig);

            foreach(var treasure in result)
            {
                if (node.Depth >= limit || myCoins.Count < 20)
                {
                    System.Threading.Interlocked.Increment(ref _pendingTreasures);
                    SellAsync(new Treasure() {
                        Value = treasure,
                        Depth = node.Depth,
                        PosX = node.Report.Area.PosX,
                        PosY = node.Report.Area.PosY
                    }, myCoins);
                }
            }

            node.Depth++;
            node.Report.Amount -= result.Count;
            return new List<MyNode> { node };
        }

        private async Task<List<MyNode>> ProcessNewNode(int licenseId, ConcurrentBag<int> myCoins, int limit)
        {
            List<MyNode> newNodes = new List<MyNode>();

            await _explorerWorker.FindCells(newNodes, 1);

            var processedNodes = await ProcessExistedNode(newNodes[0], licenseId, myCoins, limit);
            newNodes[0] = processedNodes[0];
            return newNodes;
        }

        public async Task Doit(double[] w, int cashLevel)
        {
            ConcurrentBag<int> myCoins = new ConcurrentBag<int>();
            List<Task> cashTasks = new List<Task>();

            List<MyNode> nodes = new List<MyNode>();

            List<Task<List<MyNode>>> digTasks = new List<Task<List<MyNode>>>();

            List<Treasure> treasures = new List<Treasure>();

            List<Treasure> toCash = new List<Treasure>();

            Random r = new Random();

            while(true) {
                License license = await GetLicenseAsync(myCoins, GetLicenseType(r, w));

                int neededCount = license.DigAllowed - nodes.Count;

                digTasks.Clear();

                foreach(var node in nodes.Take(license.DigAllowed))
                {
                    digTasks.Add(ProcessExistedNode(node, license.Id ?? 0, myCoins, cashLevel));
                }

                for(int i = 0; i < neededCount; ++i)
                {
                    digTasks.Add(ProcessNewNode(license.Id ?? 0, myCoins, cashLevel));
                }

                var nodeLists = await Task.WhenAll(digTasks);

                var skippedNodes = nodes.Skip(license.DigAllowed).ToList();

                nodes.Clear();
                nodes.AddRange(skippedNodes);
                nodes.AddRange(nodeLists.SelectMany(x => x).Where(x => x.Depth <= 10 && x.Report.Amount > 0));
            }
        }
    }
}