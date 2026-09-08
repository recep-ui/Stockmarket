using BistQuant.Application;
using BistQuant.Infrastructure;
using BistQuant.Worker;
using BistQuant.Worker.Scheduling;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddSingleton<IMarketScanScheduler, MarketScanScheduler>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
