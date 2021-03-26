using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Miner.Models;

namespace Miner
{
    public class SmartExplorerWorker : ICellProvider
    {
        private readonly Client _client;
        private readonly ILogger<SmartExplorerWorker> _logger;
        private readonly ConcurrentBag<Area> _areas = new ConcurrentBag<Area>();

        const int side = 15;
        public SmartExplorerWorker(
            ClientFactory clientFactory,
            ILogger<SmartExplorerWorker> logger)
        {
            _client = clientFactory.Create();
            _logger = logger;

            int size = 3500;

            Console.WriteLine($"Side: side - {side}");

            var areas = new List<Area>(size * size / (size * size));

            for(int x = 0; x < size / 2 / side; ++x) {
                for(int y = 0; y < size/side;++y) {
                    var area = new Area() {
                        PosX = size / 2 + x*side,
                        PosY = y*side,
                        SizeX = side,
                        SizeY = side
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

        private List<Area> SplitInRows(Area a)
        {
            List<Area> result = new List<Area>(side);
            for(int y = 0; y < a.SizeY; ++y)
            {
                result.Add(new Area()
                {
                    PosX = a.PosX,
                    PosY = a.PosY + y,
                    SizeX = a.SizeX,
                    SizeY = 1
                });
            }

            return result;
        }

        private List<Area> SplitInCols(Area a)
        {
            List<Area> result = new List<Area>(side);
            for(int x = 0; x < a.SizeX; ++x)
            {
                result.Add(new Area()
                {
                    PosX = a.PosX + x,
                    PosY = a.PosY,
                    SizeX = 1,
                    SizeY = a.SizeY
                });
            }

            return result;
        }

        struct Point
        {
            public int X, Y, Amount;
        }

        private bool CheckByVerified(int[,] map, List<Point> verifiedList)
        {
            foreach(var p in verifiedList)
            {
                if (map[p.Y, p.X] != p.Amount)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task RemoveCandidates(
            List<int[,]> candidates,
            int[,] verified,
            List<Point> verfiedList,
            Explore[] rows,
            Explore[] cols)
        {
            candidates.RemoveAll(x => !CheckByVerified(x, verfiedList));

            if (candidates.Count < 1)
            {
                return;
            }

            var candidate = candidates.First();

            for(int row = 0; row < side; ++row)
            for(int col = 0; col < side; ++col)
            {
                if (verified[row, col] < 0 && candidate[row, col] > 0)
                {
                    var report = await _client.ExploreAsync(new Area()
                    {
                        PosX = cols[col].Area.PosX,
                        PosY = rows[row].Area.PosY,
                        SizeX = 1,
                        SizeY = 1
                    });

                    verified[row, col] = report.Amount;
                    verfiedList.Add(new Point() {
                        X = col,
                        Y = row,
                        Amount = report.Amount
                    });

                    candidates.RemoveAll(x => x[row, col] != report.Amount);

                    if (candidates.Count < 2)
                    {
                        return;
                    }

                    candidate = candidates.First();
                }
            }
        }

        class Context
        {
            public int[,] map;
            public int[] rows;
            public int[] cols;
            public List<int[,]> candidates;
            public int[,] verified;
            public List<Point> verifiedList;
            public Explore[] rawRows;
            public Explore[] rawCols;
        }

        private void FillByRows(
            int rowIndex,
            int initialColIndex,
            Context context
        )
        {
            if (rowIndex == context.rows.Length)
            {
                context.candidates.Add((int[,])context.map.Clone());

                if (context.candidates.Count > 4)
                {
                    RemoveCandidates(context.candidates, context.verified, context.verifiedList, context.rawRows, context.rawCols).Wait();
                }
                
                return;
            }

            if (context.rows[rowIndex] == 0)
            {
                FillByRows(rowIndex + 1, 0, context);
                return;
            }

            for(int colIndex = initialColIndex; colIndex < side; ++colIndex)
            {
                context.map[rowIndex, colIndex]++;
                context.rows[rowIndex]--;
                context.cols[colIndex]--;

                var source = context.verifiedList.Count;
                if (context.map[rowIndex, colIndex] < 3 &&
                    context.cols[colIndex] >= 0 && context.rows[rowIndex] >= 0 &&
                    (context.verified[rowIndex, colIndex] >= 0 && context.verified[rowIndex, colIndex] == context.map[rowIndex, colIndex] || context.verified[rowIndex, colIndex] < 0)) {
                    FillByRows(rowIndex, colIndex, context);
                }

                context.map[rowIndex, colIndex]--;
                context.rows[rowIndex]++;
                context.cols[colIndex]++;

                if (source != context.verifiedList.Count)
                {
                    for(int i = source; i < context.verifiedList.Count; ++i)
                    {
                        var p = context.verifiedList[i];
                        if (p.Y < rowIndex)
                        {
                            if (context.map[p.Y, p.X] != p.Amount)
                            {
                                return;
                            }
                        }
                        else if (p.Y == rowIndex && p.X <= initialColIndex)
                        {
                            if (context.map[p.Y, p.X] != p.Amount)
                            {
                                return;
                            }
                        }
                    }
                }
            }
        }

        private async Task<int[,]> FindSolution(Explore[] rows, Explore[] cols)
        {
            int[,] map = new int[side, side];
            List<int[,]> candidates = new List<int[,]>();
            int[] amountsRows = rows.Select(x => x.Amount).ToArray();
            int[] amountsCols = cols.Select(x => x.Amount).ToArray();

            int[,] verified = new int[side, side];
            List<Point> verifiedList = new List<Point>();
            for(int i = 0; i < side; ++i)
            for(int j = 0; j < side; ++j)
            {
                verified[i, j] = -1;
            }

            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            var context = new Context()
            {
                map = map,
                rows = amountsRows,
                cols = amountsCols,
                candidates = candidates,
                verified = verified,
                verifiedList = verifiedList,
                rawRows = rows,
                rawCols = cols
            };
            FillByRows(0, 0, context);

            await RemoveCandidates(candidates, verified, verifiedList, rows, cols);

            sw.Stop();

            if (candidates.Count == 0)
            {
                Console.WriteLine("wtf");
            }

            Console.WriteLine($"Verified = {verifiedList.Count} Time = {sw.ElapsedMilliseconds}");

            if (sw.ElapsedMilliseconds > 2000)
            {
                Console.WriteLine("Long");
            }

            return candidates[0];
        }

        public async Task FindCells(List<MyNode> cells, int count)
        {
            while(cells.Count < count)
            {
                Area area = null;
                while(!_areas.TryTake(out area))
                {
                }

                var rowsAreas = SplitInRows(area);
                var colsAreas = SplitInCols(area);

                var result = await Task.WhenAll(
                    Task.WhenAll(rowsAreas.Select(_client.ExploreAsync)),
                    Task.WhenAll(colsAreas.Select(_client.ExploreAsync))
                );

                var rowsReports = result[0];
                var colsReports = result[1];

                
                var map = await FindSolution(rowsReports, colsReports);

                for(int x = 0; x < side; ++x)
                {
                    for(int y = 0; y < side; ++y)
                    {
                        if (map[y, x] > 0)
                        {
                            cells.Add(new MyNode()
                            {
                                Report = new Explore()
                                {
                                    Amount = map[y, x],
                                    Area = new Area()
                                    {
                                        SizeX = 1,
                                        SizeY = 1,
                                        PosX = area.PosX + x,
                                        PosY = area.PosY + y
                                    }
                                }
                            });
                        }
                    }
                }
            }
        }
    }
}