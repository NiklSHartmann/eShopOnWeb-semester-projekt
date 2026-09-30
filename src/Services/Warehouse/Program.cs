using Messaging.Shared;
using Microsoft.Extensions.Options;
using Warehouse;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRabbitMqMessaging(builder.Configuration);
builder.Services.Configure<WarehouseOptions>(builder.Configuration.GetSection(WarehouseOptions.SectionName));
builder.Services.AddSingleton(sp =>
    new StockLedger(sp.GetRequiredService<IOptions<WarehouseOptions>>().Value.InitialStockPerItem));
builder.Services.AddHostedService<WarehouseWorker>();

builder.Build().Run();
