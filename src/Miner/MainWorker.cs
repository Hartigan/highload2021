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
        public MainWorker(
            ILoggerFactory loggerFactory,
            ClientFactory clientFactory)
        {
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
            _workers.Add(_diggerWorker.CheckTreasures());
            for (int i = 0; i < 10; ++i) {
                _workers.Add(_diggerWorker.Doit());
            }

            return Task.WhenAll(_workers);
        }
    }
}