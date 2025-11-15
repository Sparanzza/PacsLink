using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Network;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PacsLink.Core.DicomServices;

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
        services.AddHostedService<ScpWorker>();
        services.AddImageManager<ImageSharpImageManager>();
    })
    .Build();

DicomSetupBuilder.UseServiceProvider(host.Services);

Console.WriteLine("Starting PacsLink.Modality...");
await host.RunAsync();

/// <summary>
/// This worker service is responsible for starting and stopping the DICOM SCP server.
/// </summary>
internal class ScpWorker : IHostedService
{
    private readonly ILogger<ScpWorker> _logger;
    private readonly IDicomServerFactory _dicomServerFactory;
    private IDicomServer? _dicomServer;
    private const int Port = 104; // TODO: Move to configuration

    public ScpWorker(ILogger<ScpWorker> logger, IDicomServerFactory dicomServerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dicomServerFactory = dicomServerFactory ?? throw new ArgumentNullException(nameof(dicomServerFactory));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Modality DICOM services (SCP) on port {Port}...", Port);
        _dicomServer = _dicomServerFactory.Create<ScpService>(Port);
        _logger.LogInformation("Modality is now listening for incoming DICOM associations.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Modality DICOM services...");
        _dicomServer?.Dispose();
        return Task.CompletedTask;
    }
}