using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Miner.Models;
using Priority_Queue;

namespace Miner
{
    public class ExplorerWorker
    {
        private readonly ClientFactory _clientFactory;
        private readonly ILogger<ExplorerWorker> _logger;
        private readonly FastPriorityQueue<MyNode> _queue = new FastPriorityQueue<MyNode>(100000);
        private readonly ConcurrentQueue<MyNode> _cells;
        public ExplorerWorker(
            ClientFactory clientFactory,
            ILogger<ExplorerWorker> logger,
            ConcurrentQueue<MyNode> cells)
        {
            _clientFactory = clientFactory;
            _logger = logger;
            _cells = cells;
        }

        private async Task Process()
        {
            var client = _clientFactory.Create();
            while(true)
            {
                if (_cells.Count > 200){
                    while (_cells.Count > 60)
                    {
                        await Task.Yield();
                    }
                }

                MyNode node = null;
                while (_queue.Count == 0)
                {
                    await Task.Yield();
                }
                lock(_queue)
                {
                    if (_queue.Count > 0)
                    {
                        node = _queue.Dequeue();
                    }
                    else
                    {
                        continue;
                    }
                }

                if (node.IsCell())
                {
                    _cells.Enqueue(node);
                    continue;
                }

                Area area1, area2;

                if (node.Report.Area.SizeX > node.Report.Area.SizeY) {
                    var div = node.Report.Area.SizeX > 2 ? 3 : 2;
                    var newSizeX1 = node.Report.Area.SizeX / div;
                    var newSizeX2 = node.Report.Area.SizeX - newSizeX1;
                    area1 = new Area() {
                        PosX = node.Report.Area.PosX,
                        PosY = node.Report.Area.PosY,
                        SizeX = newSizeX1,
                        SizeY = node.Report.Area.SizeY
                    };
                    area2 = new Area() {
                        PosX = node.Report.Area.PosX + newSizeX1,
                        PosY = node.Report.Area.PosY,
                        SizeX = newSizeX2,
                        SizeY = node.Report.Area.SizeY
                    };
                }
                else {
                    var div = node.Report.Area.SizeY > 2 ? 3 : 2;
                    var newSizeY1 = node.Report.Area.SizeY / div;
                    var newSizeY2 = node.Report.Area.SizeY - newSizeY1;
                    area1 = new Area() {
                        PosX = node.Report.Area.PosX,
                        PosY = node.Report.Area.PosY,
                        SizeX = node.Report.Area.SizeX,
                        SizeY = newSizeY1
                    };
                    area2 = new Area() {
                        PosX = node.Report.Area.PosX,
                        PosY = node.Report.Area.PosY + newSizeY1,
                        SizeX = node.Report.Area.SizeX,
                        SizeY = newSizeY2
                    };
                }

                Explore report = await client.ExploreAsync(area1);

                MyNode node1 = new MyNode() {
                    Report = new Explore() {
                        Area = area2,
                        Amount = node.Report.Amount - report.Amount
                    }
                };

                MyNode node2 = new MyNode() {
                    Report = report
                };

                lock(_queue)
                {
                    if (!node1.IsEmpty()) {
                        _queue.Enqueue(node1, node1.CalculatePriority());
                    }

                    if (!node2.IsEmpty()) {
                        _queue.Enqueue(node2, node2.CalculatePriority());
                    }
                }
                
            }
        }

        private async Task AddInitial(Area area, Client client)
        {
            var report = await client.ExploreAsync(area);
            MyNode node = new MyNode() {
                Report = report
            };

            lock(_queue)
            {
                _queue.Enqueue(node, node.CalculatePriority());
            }
        }

        public async Task Doit()
        {

            Client client = _clientFactory.Create();

            List<Task> tasks = new List<Task>(100);

            for(int x = 0; x< 5; ++x) {
                for(int y = 0; y < 5;++y) {
                    var area = new Area() {
                        PosX = x*700,
                        PosY = y*700,
                        SizeX = 700,
                        SizeY = 700
                    };

                    tasks.Add(AddInitial(area, client));
                    
                }
            }

            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());
            tasks.Add(Process());

            await Task.WhenAll(tasks);
        }
    }
}