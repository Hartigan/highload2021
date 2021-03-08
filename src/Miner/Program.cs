using System;
using System.Linq;
using System.Net;
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
            baseUrl = Dns.GetHostEntry(baseUrl).AddressList.First(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();
            //baseUrl = "127.0.0.1";
            System.Console.WriteLine("address: " + baseUrl);

            var factory = LoggerFactory.Create(builder => {
                builder.AddConsole(opt => {
                    opt.FormatterName = ConsoleFormatterNames.Systemd;
                });
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            ClientFactory clientFactory = new ClientFactory(baseUrl, factory.CreateLogger<Client>());

            MainWorker worker = new MainWorker(factory, clientFactory);


            // Client client = clientFactory.Create();
            // bool ready = false;
            // do
            // {
            //     await Task.Delay(100);
            //     ready = await client.HealthCheckAsync();
            // } while(!ready);

            await worker.Doit();
        }
    }
}
