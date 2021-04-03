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
        private readonly ConcurrentBag<Area> _areas = new ConcurrentBag<Area>();

        const int stepX = 3;
        const int stepY = 10;

        public ExplorerWorker(
            ClientFactory clientFactory,
            ILogger<ExplorerWorker> logger)
        {
            _client = clientFactory.Create();
            _logger = logger;

            int size = 3500;

            Console.WriteLine($"Steps: x - {stepX}, y - {stepY}");

            var areas = new List<Area>(size * size / (stepX * stepY));

            for(int x = 0; x< size/stepX; ++x) {
                for(int y = 0; y < size/stepY;++y) {
                    var area = new Area() {
                        PosX = x*stepX,
                        PosY = y*stepY,
                        SizeX = stepX,
                        SizeY = stepY
                    };

                    areas.Add(area);
                }
            }

            Random rng = new Random();
            foreach(var a in areas.OrderBy(x => rng.Next()))
            {
                _areas.Add(a);
            }

            for(int i = 1; i < requestsCount.Length; ++i)
            {
                GetRequestsCount(i);
            }
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

        private int[] requestsCount = new int[50];

        private int GetRequestsCount(int k)
        {
            if (requestsCount[k] > 0)
            {
                return requestsCount[k];
            }

            const int blockSize = stepX * stepY;
            const double confidence = 0.95;
            const double precision = 0.001;

            double currentConfidence = 0;
            int currentN = k;

            do
            {
                currentN++;
                currentConfidence = 0;
                for(int i = k; i <= currentN; ++i)
                {
                    double b = Bernoulli(currentN, i);

                    currentConfidence += b;
                    if (b < precision)
                    {
                        break;
                    }
                }
            }
            while(currentConfidence < confidence);

            if (currentN % blockSize == 0)
            {
                requestsCount[k] = currentN / blockSize;
            }
            else
            {
                requestsCount[k] = currentN / blockSize + 1;
            }

            return requestsCount[k];
        }

        public async Task FindCells(List<MyNode> cells, int count)
        {
            List<Area> areas = new List<Area>(count - cells.Count);
            while(cells.Count < count)
            {
                areas.Clear();
                while(areas.Count < GetRequestsCount(count - cells.Count))
                {
                    Area area = null;
                    if (_areas.TryTake(out area))
                    {
                        areas.Add(area);
                    }
                }

                var newCells = await Task.WhenAll(areas.Select(async area => {
                    var report = await _client.ExploreAsync(area);

                    if (report.Amount == 0)
                    {
                        return Enumerable.Empty<MyNode>();
                    }

                    return await ProcessNode(new MyNode() {
                        Report = report
                    });
                }));

                cells.AddRange(newCells.SelectMany(x => x));
            }
        }
    }
}