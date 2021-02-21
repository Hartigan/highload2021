using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Miner.Models;
using Priority_Queue;

namespace Miner
{
    public class ExplorerWorker
    {
        private readonly Client _client;
        private readonly ILogger<ExplorerWorker> _logger;
        private readonly FastPriorityQueue<MyNode> _queue = new FastPriorityQueue<MyNode>(100000);
        private readonly Action<MyNode> _pushCell;
        public ExplorerWorker(Client client, ILogger<ExplorerWorker> logger, Action<MyNode> pushCell)
        {
            _client = client;
            _logger = logger;
            _pushCell = pushCell;
        }

        public async Task Doit()
        {
            for(int x = 0; x< 7; ++x) {
                for(int y = 0; y < 7;++y) {
                    var area = new Area() {
                        PosX = x*500,
                        PosY = y*500,
                        SizeX = 500,
                        SizeY = 500
                    };
                    Explore initialReport = null;
                    do
                    {
                        initialReport = await _client.ExploreAsync(area);
                    } while(initialReport == null);
                    MyNode initialNode = new MyNode(){
                        Report = initialReport
                    };
                    _queue.Enqueue(initialNode, initialNode.CalculatePriority());
                }
            }
            

            while(_queue.Count > 0)
            {
                var node = _queue.Dequeue();

                Area area1, area2;

                if (node.Report.Area.SizeX > node.Report.Area.SizeY) {
                    var newSizeX1 = node.Report.Area.SizeX / 2;
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
                    var newSizeY1 = node.Report.Area.SizeY / 2;
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

                Explore report = null;

                do
                {
                    report = await _client.ExploreAsync(area2);
                } while(report == null);

                MyNode node1 = new MyNode() {
                    Report = new Explore() {
                        Area = area1,
                        Amount = node.Report.Amount - report.Amount
                    }
                };

                MyNode node2 = new MyNode() {
                    Report = report
                };

                if (!node1.IsEmpty()) {
                    if (node1.IsCell()) {
                        _pushCell(node1);
                    }
                    else {
                        _queue.Enqueue(node1, node1.CalculatePriority());
                    }
                }

                if (!node2.IsEmpty()) {
                    if (node2.IsCell()) {
                        _pushCell(node2);
                    }
                    else {
                        _queue.Enqueue(node2, node2.CalculatePriority());
                    }
                }
            }
        }
    }
}