using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Miner.Models;

namespace Miner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await Doit();
        }

        static async Task Doit() {
            string baseUrl = Environment.GetEnvironmentVariable("ADDRESS");

            var factory = LoggerFactory.Create(builder => {
                builder.AddConsole(opt => {
                    opt.FormatterName = ConsoleFormatterNames.Systemd;
                });
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            Client client = new Client(baseUrl, factory.CreateLogger<Client>());

            MainWorker worker = new MainWorker(factory, client);

            bool ready = false;
            do
            {
                await Task.Delay(100);
                ready = await client.HealthCheckAsync();
            } while(!ready);

            await worker.Doit();

            // var area = new Area();
            // area.SizeX = 1;
            // area.SizeY = 1;
            // var licence = await client.BuyLicenseAsync(new System.Collections.Generic.List<int>());
            // for(int x = 0; x < 3500; ++x) {
            //     for(int y = 0; y < 3500; ++y) {
            //         area.PosX = x;
            //         area.PosY = y;
            //         var explore = await client.ExploreAsync(area);
            //         if (explore == null || explore.Amount == 0) {
            //             continue;
            //         }

            //         var left = explore.Amount;
            //         var depth = 1;

            //         while (left > 0 && depth <= 10) {
            //             if (licence.DigUsed >= licence.DigAllowed) {
            //                 do
            //                 {
            //                     licence = await client.BuyLicenseAsync(new System.Collections.Generic.List<int>());
            //                 } while(licence == null);
                            
            //             }
            //             var dig = new Dig() {
            //                 LicenseId = licence.Id ?? 0,
            //                 PosX = area.PosX,
            //                 PosY = area.PosY,
            //                 Depth = depth 
            //             };

            //             var treasures = await client.DigAsync(dig);
            //             licence.DigUsed++;
            //             depth++;
            //             if (treasures != null) {
            //                 left-= treasures.Count;
            //                 foreach(var treasure in treasures) {
            //                     var res = await client.CashAsync(treasure);
            //                 }

            //             }
            //         }
            //     }
            // }
        }
    }
}
