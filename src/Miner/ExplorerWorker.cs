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
        public ExplorerWorker(
            ClientFactory clientFactory,
            ILogger<ExplorerWorker> logger)
        {
            _client = clientFactory.Create();
            _logger = logger;

            int size = 3500;

            int stepX = 1;
            int stepY = 7;

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
        }

        private List<Area> Split(Area a)
        {
            Area area1, area2;

            if (a.SizeX > a.SizeY) {
                var div = 2;
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
                var div = 2;
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

        public async Task FindCells(List<MyNode> cells, int count)
        {
            List<Area> areas = new List<Area>(count - cells.Count);
            while(cells.Count < count)
            {
                areas.Clear();
                while(areas.Count < (count - cells.Count) * 16)
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