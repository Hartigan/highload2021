using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Miner.Models;
using Priority_Queue;

namespace Miner
{
    public class MainWorker
    {
        private readonly DiggerWorker _diggerWorker;
        private readonly ExplorerWorker _explorerWorker;
        private readonly Client _client;
        public MainWorker(
            ILoggerFactory loggerFactory,
            ClientFactory clientFactory)
        {
            _client = clientFactory.Create();
            var logger = loggerFactory.CreateLogger<MainWorker>();
            _explorerWorker = new ExplorerWorker(
                clientFactory,
                loggerFactory.CreateLogger<ExplorerWorker>()
            );
            _diggerWorker = new DiggerWorker(
                clientFactory,
                loggerFactory.CreateLogger<DiggerWorker>(),
                _explorerWorker
            );
            
        }


        private List<Task> _workers = new List<Task>();

        public Task Doit()
        {
            _workers.Add(_client.PrintStats());
            _workers.Add(_diggerWorker.CheckTreasures());


            double[] w = new double[] { 0.0, 1.0, 1.0, 1.0, 1.0 };
            const int limit = 3;
            System.Console.WriteLine($"L: {w[0]} {w[1]} {w[2]} {w[3]} {w[4]}, C = {limit}");

            for(int i = 0; i < 10; ++i)
            {
                _workers.Add(_diggerWorker.Doit(w, limit, i));
            }

            return Task.WhenAll(_workers);
        }
    }
}