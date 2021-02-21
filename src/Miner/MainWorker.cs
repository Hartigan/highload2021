using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Miner.Models;
using Priority_Queue;

namespace Miner
{
    public class MainWorker
    {
        private readonly DiggerWorker _diggerWorker;
        private readonly ExchangerWorker _exchangerWorker;
        private readonly ExplorerWorker _explorerWorker;
        private readonly LicenseWorker _licenseWorker;

        private readonly ConcurrentBag<License> _licenses = new ConcurrentBag<License>();
        private readonly ConcurrentBag<string> _treasures = new ConcurrentBag<string>();
        private readonly ConcurrentBag<int> _coins = new ConcurrentBag<int>();
        private readonly ConcurrentBag<MyNode> _cells = new ConcurrentBag<MyNode>();
        public MainWorker(
            ILoggerFactory loggerFactory,
            Client client)
        {
            var logger = loggerFactory.CreateLogger<MainWorker>();
            _diggerWorker = new DiggerWorker(
                client,
                loggerFactory.CreateLogger<DiggerWorker>(),
                _licenses,
                _treasures,
                _cells);
            _exchangerWorker = new ExchangerWorker(
                client,
                loggerFactory.CreateLogger<ExchangerWorker>(),
                _coins,
                _treasures
            );
            _explorerWorker = new ExplorerWorker(
                client,
                loggerFactory.CreateLogger<ExplorerWorker>(),
                cell => {
                    _cells.Add(cell);
                }
            );
            _licenseWorker = new LicenseWorker(
                client,
                loggerFactory.CreateLogger<LicenseWorker>(),
                _coins,
                _licenses
            );
        }

        public async Task Doit()
        {
            await Task.WhenAll(
                _explorerWorker.Doit(),
                _licenseWorker.Doit(),
                _diggerWorker.Doit(),
                _diggerWorker.Doit(),
                _diggerWorker.Doit(),
                _exchangerWorker.Doit()
            );
        }
    }
}