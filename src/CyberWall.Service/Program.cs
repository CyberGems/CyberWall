using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CyberWall.Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(o => o.ServiceName = "CyberWall");
builder.Services.AddHostedService<Worker>();
var host = builder.Build();
host.Run();
