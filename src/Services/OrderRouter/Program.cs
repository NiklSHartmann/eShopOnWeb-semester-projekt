using Messaging.Shared;
using OrderRouter;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRabbitMqMessaging(builder.Configuration);
builder.Services.Configure<RoutingOptions>(builder.Configuration.GetSection(RoutingOptions.SectionName));
builder.Services.AddHostedService<OrderRouterWorker>();

builder.Build().Run();
