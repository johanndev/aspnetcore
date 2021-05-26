using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.W3C;

namespace HttpLogging.Sample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //loggerFactory.CreateLogger()
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    // Json Logging
                    logging.ClearProviders();
                    logging.AddJsonConsole(options =>
                    {
                        options.JsonWriterOptions = new JsonWriterOptions()
                        {
                            Indented = true
                        };
                    });
                    //logging.AddW3CLogger();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
