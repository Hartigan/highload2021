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
        private readonly ConcurrentQueue<MyNode> _cells = new ConcurrentQueue<MyNode>();
        public MainWorker(
            ILoggerFactory loggerFactory,
            ClientFactory clientFactory)
        {
            var logger = loggerFactory.CreateLogger<MainWorker>();
            _diggerWorker = new DiggerWorker(
                clientFactory,
                loggerFactory.CreateLogger<DiggerWorker>(),
                _cells);
            _explorerWorker = new ExplorerWorker(
                clientFactory,
                loggerFactory.CreateLogger<ExplorerWorker>(),
                _cells
            );
        }


        private List<Task> _workers = new List<Task>();

        public async Task Doit()
        {
            _workers.Add(Task.Run(_explorerWorker.Doit));

            await Task.Delay(1000);

            for (int i = 0; i < 10; ++i) {
                
                _workers.Add(Task.Run(_diggerWorker.Doit));
            }

            await Task.WhenAll(_workers);
        }
    }
}