using Akr.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Terp.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Setups()
    .Add<McpSetup>();

await builder.Build().RunAsync();
