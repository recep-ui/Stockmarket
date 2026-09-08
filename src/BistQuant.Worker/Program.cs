using BistQuant.Application;
using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.Services.MarketData;
using BistQuant.Infrastructure;
using BistQuant.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddSingleton<IMarketScanScheduler, MarketScanScheduler>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
