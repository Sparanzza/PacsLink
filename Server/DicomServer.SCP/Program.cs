// See https://aka.ms/new-console-template for more information

using FellowOakDicom;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddDebug();
    })
    .ConfigureServices(services =>
    {
        services.AddFellowOakDicom();
        services.AddHostedService<Worker>();
    })
    .Build();

// This is still necessary for now until fo-dicom has first-class AspNetCore integration
DicomSetupBuilder.UseServiceProvider(host.Services);

await host.RunAsync();

public class Worker(ILogger<Worker> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting DicomServer.SCP...");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping DicomServer.SCP...");
        return Task.CompletedTask;
    }
}