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

        private void AddDiggers(List<Task> tasks, DiggerWorker.LicenseType licenseType, int count)
        {
            for (int i = 0; i < count; ++i) {
                tasks.Add(_diggerWorker.Doit(licenseType));
            }
        }

        public Task Doit()
        {
            _workers.Add(_client.PrintStats());
            _workers.Add(_diggerWorker.CheckTreasures());

            AddDiggers(_workers, DiggerWorker.LicenseType.One, 10);
            AddDiggers(_workers, DiggerWorker.LicenseType.Six, 0);
            AddDiggers(_workers, DiggerWorker.LicenseType.Eleven, 0);
            AddDiggers(_workers, DiggerWorker.LicenseType.TwentyOne, 0);

            return Task.WhenAll(_workers);
        }
    }
}