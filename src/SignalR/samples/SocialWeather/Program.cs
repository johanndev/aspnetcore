// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.W3C;

namespace SocialWeather
{
    public class Program
    {
        public static Task Main(string[] args)
        {
            var loggerFactory = LoggerFactory.Create(logging =>
            {
                logging.AddConsole();
            });
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureWebHost(webHostBuilder =>
                {
                    webHostBuilder
                    .UseSetting(WebHostDefaults.PreventHostingStartupKey, "true")
                    .ConfigureLogging(factory =>
                    {
                        factory.ClearProviders();
                        factory.AddConsole();
                        factory.AddFilter("Console", level => level >= LogLevel.Information);
                        factory.AddW3CLogger(logging =>
                        {
                            logging.LoggingFields = W3CLoggingFields.All;
                        });
                    })
                    .UseKestrel()
                    .UseContentRoot(Directory.GetCurrentDirectory())
                    .UseIISIntegration()
                    .UseStartup<Startup>();
                })
                .Build();

            return host.RunAsync();
        }
    }
}
