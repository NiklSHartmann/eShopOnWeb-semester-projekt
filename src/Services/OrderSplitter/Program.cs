using Messaging.Shared;
using OrderSplitter;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRabbitMqMessaging(builder.Configuration);
builder.Services.AddHostedService<OrderSplitterWorker>();

var host = builder.Build();
host.Run();
