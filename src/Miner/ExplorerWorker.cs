using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Miner.Models;

namespace Miner
{
    public class ExplorerWorker
    {
        private readonly Client _client;
        private readonly ILogger<ExplorerWorker> _logger;

        const int stepX = 3;
        const int stepY = 5;

        const int size = 3500;

        const int sizeX = size/stepX;
        const int sizeY = size/stepY;
        const int WorkersX = 10;
        const int WorkersY = 5;

        private Block[,] _map = new Block[sizeX, sizeY];

        private LinkedList<Block>[,] _borders = new LinkedList<Block>[WorkersX, WorkersY];
        private class Block
        {
            public Area Area;
            public int X;
            public int Y;
            public int Amount = -1;
            public bool Used = false;
        }

        public ExplorerWorker(
            ClientFactory clientFactory,
            ILogger<ExplorerWorker> logger)
        {
            _client = clientFactory.Create();
            _logger = logger;

            for(int x = 0; x < WorkersX; x++)
            for(int y = 0; y < WorkersY; y++)
                _borders[x, y] = new LinkedList<Block>();

            Console.WriteLine($"Steps: x - {stepX}, y - {stepY}");

            for(int x = 0; x< sizeX; ++x) {
                for(int y = 0; y < sizeY;++y) {
                    _map[x, y] = new Block() {
                        Area = new Area() {
                            PosX = x*stepX,
                            PosY = y*stepY,
                            SizeX = stepX,
                            SizeY = stepY
                        },
                        X = x,
                        Y = y,
                    };
                }
            }
        }

        private Random _rng = new Random();

        private Block GetRandomBlock(int workerX, int workerY)
        {
            const int sx = sizeX / WorkersX;
            const int sy = sizeY / WorkersY;


            Block r = _map[_rng.Next()%(sizeX / WorkersX) + sx * workerX, _rng.Next() % (sizeY / WorkersY) + sy * workerY];
            while(r.Amount >= 0)
            {
                r = _map[_rng.Next()%(sizeX / WorkersX) + sx * workerX, _rng.Next() % (sizeY / WorkersY) + sy * workerY];
            }
            return r;
        }

        const double BaseAmount = stepX * stepY * 0.04;
        const int Radius = 3;
        

        private double Estimate(Block b)
        {
            List<Block> neighbors = new List<Block>();

            double megaBlock = 0;

            double sum = 0;

            for(int x = -Radius; x <= Radius; ++x)
            for(int y = -Radius; y <= Radius; ++y)
            {
                if (x + b.X < 0 || x + b.X >= sizeX || y + b.Y < 0 || y + b.Y >= sizeY)
                {
                    continue;
                }

                megaBlock += BaseAmount;

                if (x == 0 && y == 0)
                {
                    continue;
                }

                var n = _map[x + b.X, y + b.Y];

                if (n.Amount < 0)
                {
                    sum += BaseAmount;
                }
                else
                {
                    sum += n.Amount;
                }
            }

            return megaBlock - sum;
        }

        private List<Block> GetFreeNeighbors(Block b)
        {
            var result = new List<Block>();

            for(int x = -1; x <= 1; ++x)
            for(int y = -1; y <= 1; ++y)
            {
                if (x + b.X < 0 || x + b.X >= sizeX || y + b.Y < 0 || y + b.Y >= sizeY)
                {
                    continue;
                }

                if (x == 0 && y == 0)
                {
                    continue;
                }

                var n = _map[x + b.X, y + b.Y];

                if (n.Amount < 0)
                {
                    result.Add(n);
                }
            }

            return result;
        }

        private List<Tuple<Block, double>> GetBlocks(int workerX, int workerY)
        {
            var border = _borders[workerX, workerY];

            if (border.Count == 0)
            {
                return new List<Tuple<Block, double>>() {
                    new Tuple<Block, double>(GetRandomBlock(workerX, workerY), BaseAmount),
                };
            }

            var candidates = new Dictionary<int, Dictionary<int, Block>>();

            var current = border.First;

            while(current != null)
            {
                var c = current;
                current = current.Next;

                var neighbors = GetFreeNeighbors(c.Value);
                if (neighbors.Count == 0)
                {
                    border.Remove(c);
                }

                foreach(var n in neighbors)
                {
                    Dictionary<int, Block> yDict = null;
                    if (!candidates.TryGetValue(n.X, out yDict))
                    {
                        yDict = new Dictionary<int, Block>();
                        candidates[n.X] = yDict;
                    }
                    yDict[n.Y] = n;
                }
            }

            var blocks = candidates.SelectMany(x => x.Value.Values);

            if (!blocks.Any())
            {
                return new List<Tuple<Block, double>>() {
                    new Tuple<Block, double>(GetRandomBlock(workerX, workerY), BaseAmount)
                };
            }

            var result = blocks.Select(x => new Tuple<Block, double>(x, Estimate(x))).OrderByDescending(x => x.Item2).ToList();

            return result;
        }

        private List<Area> Split(Area a)
        {
            Area area1, area2;

            if (a.SizeX > a.SizeY) {
                var div = a.SizeX > 2 ? a.SizeX : 2;
                var newSizeX1 = a.SizeX / div;
                var newSizeX2 = a.SizeX - newSizeX1;
                area1 = new Area() {
                    PosX = a.PosX,
                    PosY = a.PosY,
                    SizeX = newSizeX1,
                    SizeY = a.SizeY
                };
                area2 = new Area() {
                    PosX = a.PosX + newSizeX1,
                    PosY = a.PosY,
                    SizeX = newSizeX2,
                    SizeY = a.SizeY
                };
            }
            else {
                var div = a.SizeY > 2 ? a.SizeY : 2;
                var newSizeY1 = a.SizeY / div;
                var newSizeY2 = a.SizeY - newSizeY1;
                area1 = new Area() {
                    PosX = a.PosX,
                    PosY = a.PosY,
                    SizeX = a.SizeX,
                    SizeY = newSizeY1
                };
                area2 = new Area() {
                    PosX = a.PosX,
                    PosY = a.PosY + newSizeY1,
                    SizeX = a.SizeX,
                    SizeY = newSizeY2
                };
            }

            return new List<Area>(){ area1, area2 };
        }

        private async Task<List<MyNode>> ProcessNode(MyNode node)
        {
            var areas = Split(node.Report.Area);

            var requestedReport = await _client.ExploreAsync(areas[0]);
            var generatedReport = new Explore()
            {
                Area = areas[1],
                Amount = node.Report.Amount - requestedReport.Amount
            };

            List<MyNode> cells = new List<MyNode>();
            List<MyNode> newNodes = new List<MyNode>();
            var n1 = new MyNode() { Report = requestedReport };
            var n2 = new MyNode() { Report = generatedReport };

            if (!n1.IsEmpty())
            {
                if (n1.IsCell())
                {
                    cells.Add(n1);
                }
                else
                {
                    newNodes.Add(n1);
                }
            }

            if (!n2.IsEmpty())
            {
                if (n2.IsCell())
                {
                    cells.Add(n2);
                }
                else
                {
                    newNodes.Add(n2);
                }
            }

            var newCells = await Task.WhenAll(newNodes.Select(ProcessNode));
            cells.AddRange(newCells.SelectMany(x => x));

            return cells;
        }

        private double Combinations(int n, int k)
        {
            double r = 1;

            for(int i = 1; i <= n; ++i)
            {
                r *= i;
                if (i <= k)
                {
                    r /= i;
                }

                if (i <= (n - k))
                {
                    r /= i;
                }
            }

            return r;
        }

        private double Bernoulli(int n, int k)
        {
            const double p = 0.04;
            return Combinations(n, k) * Math.Pow(p, k) * Math.Pow(1 - p, n - k);
        }

        public async Task FindCells(List<MyNode> cells, int count, int workerX, int workerY)
        {
            List<Area> areas = new List<Area>(count - cells.Count);
            while(cells.Count < count)
            {
                var blocks = GetBlocks(workerX, workerY);

                Block block = null;

                while(true)
                {
                    foreach(var b in blocks)
                    {
                        block = b.Item1;
                        if (!block.Used)
                        {
                            lock(block)
                            {
                                if (!block.Used)
                                {
                                    block.Used = true;
                                    goto exit;
                                }
                            }
                        }
                    }
                    
                    blocks = GetBlocks(workerX, workerY);
                }
                exit:
                var report = await _client.ExploreAsync(block.Area);
                block.Amount = report.Amount;
                _borders[workerX, workerY].AddFirst(block);

                if (block.Amount == 0)
                {
                    continue;
                }

                var newCells = await ProcessNode(new MyNode()
                {
                    Report = report
                });

                cells.AddRange(newCells);
            }
        }
    }
}