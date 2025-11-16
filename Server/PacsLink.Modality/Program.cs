using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Network;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacsLink.Core.Configuration;
using PacsLink.Core.DicomServices;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddDebug();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddFellowOakDicom();
        services.AddHostedService<ModalityWorker>();
        services.AddImageManager<ImageSharpImageManager>();

        services.Configure<StorageSettings>(context.Configuration.GetSection(StorageSettings.SectionName));
        services.AddSingleton<IScuService, ScuService>();
    })
    .Build();

DicomSetupBuilder.UseServiceProvider(host.Services);

Console.WriteLine("Starting PacsLink.Modality...");
await host.RunAsync();

/// <summary>
/// This worker service manages all background tasks for the Modality.
/// 1. It starts a DICOM SCP server to listen for incoming associations.
/// 2. It periodically triggers a SCU task to send files.
/// </summary>
internal class ModalityWorker : IHostedService, IDisposable
{
    private readonly ILogger<ModalityWorker> _logger;
    private readonly IDicomServerFactory _dicomServerFactory;
    private readonly IScuService _scuService;
    private readonly StorageSettings _storageSettings;
    private IDicomServer? _dicomServer;
    private Timer? _scuTimer;
    private const int Port = 104; // TODO: Move to configuration

    public ModalityWorker(ILogger<ModalityWorker> logger, IDicomServerFactory dicomServerFactory,
        IScuService scuService, IOptions<StorageSettings> storageSettings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dicomServerFactory = dicomServerFactory ?? throw new ArgumentNullException(nameof(dicomServerFactory));
        _scuService = scuService;
        _storageSettings = storageSettings.Value ?? throw new ArgumentNullException(nameof(storageSettings));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Modality DICOM services (SCP) on port {Port}...", Port);
        _dicomServer = _dicomServerFactory.Create<ScpService>(Port);
        _logger.LogInformation("Modality is now listening for incoming DICOM associations.");

        _logger.LogInformation("Starting SCU sender task timer.");
        _scuTimer = new Timer(DoScuWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(30)); // Runs every 30 seconds

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Modality DICOM services...");
        _dicomServer?.Dispose();
        _scuTimer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async void DoScuWork(object? state)
    {
        try
        {
            _logger.LogInformation("SCU Worker woke up to send a file.");

            string fileToSend = Path.Combine(_storageSettings.DicomStoragePath ?? "", "dicom_examples", "image-000001.dcm");

            if (File.Exists(fileToSend))
            {
                bool success =
                    await _scuService.SendFileAsync(fileToSend, "127.0.0.1", Port, false, "MODALITY_SCU", "AnySCP");
                _logger.LogInformation("SCU Send task finished. Success: {Success}", success);
            }
            else
            {
                _logger.LogWarning("SCU Worker: File to send not found at {FilePath}", fileToSend);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred in DoScuWork.");
        }
    }

    public void Dispose()
    {
        _scuTimer?.Dispose();
        _dicomServer?.Dispose();
    }
}