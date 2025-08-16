using System.Text;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
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

DicomSetupBuilder
    .UseServiceProvider(host.Services);

new DicomSetupBuilder()
    .RegisterServices(s => s.AddImageManager<ImageSharpImageManager>())
    .Build();


await host.RunAsync();

internal class Worker : IHostedService
{
    private readonly ILogger<Worker> _logger;
    private readonly IDicomServerFactory _dicomServerFactory;
    private readonly IDicomClientFactory _dicomClientFactory;
    private IDicomServer _dicomServer;

    public Worker(ILogger<Worker> logger, IDicomServerFactory dicomServerFactory,
        IDicomClientFactory dicomClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dicomServerFactory = dicomServerFactory ?? throw new ArgumentNullException(nameof(dicomServerFactory));
        _dicomClientFactory = dicomClientFactory ?? throw new ArgumentNullException(nameof(dicomClientFactory));
        _dicomServer = null!;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting DICOM server");
        _dicomServer = _dicomServerFactory.Create<EchoService>(104);
        _logger.LogInformation("DICOM server is running");

        var client = _dicomClientFactory.Create("127.0.0.1", 104, false, "AnySCU", "AnySCP");

        _logger.LogInformation("Sending C-ECHO request");
        DicomCEchoResponse? response = null;
        await client.AddRequestAsync(new DicomCEchoRequest { OnResponseReceived = (_, r) => response = r });
        await client.SendAsync(cancellationToken);
        if (response != null)
        {
            _logger.LogInformation("C-ECHO response received");
        }
        else
        {
            _logger.LogError("No C-ECHO response received");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping DicomServer.SCP...");
        return Task.CompletedTask;
    }
}

public class EchoService : DicomService, IDicomServiceProvider, IDicomCEchoProvider
{
    private readonly ILogger<EchoService> _logger;

    public EchoService(INetworkStream stream,
        Encoding fallbackEncoding,
        ILogger<EchoService> logger,
        DicomServiceDependencies dependencies) : base(stream, fallbackEncoding, logger, dependencies)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason) =>
        _logger.LogInformation("Received abort");

    public void OnConnectionClosed(Exception exception) => _logger.LogInformation("Connection closed");

    public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
    {
        _logger.LogInformation($"Received association request from: {association.CallingAE}");
        _logger.LogInformation($"Client is trying to connect to our AE: {association.CalledAE}");

        // "AnySCP" is the AE Title we have defined for our server.
        if (association.CalledAE != "AnySCP")
        {
            // If the AE Title is not ours, we reject the association.
            _logger.LogError(
                $"Incorrect AE Title. Expected 'AnySCP' but received '{association.CalledAE}'. Rejecting.");
            return SendAssociationRejectAsync(
                DicomRejectResult.Permanent,
                DicomRejectSource.ServiceUser,
                DicomRejectReason.CalledAENotRecognized);
        }

        _logger.LogInformation("AE Title is correct. Accepting association.");
        // We accept the service proposals (Presentation Contexts) offered by the client.
        foreach (DicomPresentationContext presentationContext in association.PresentationContexts)
        {
            // In a real server, we would also check here if we support the requested service
            // (e.g., CT Image Storage) before accepting it.
            presentationContext.SetResult(DicomPresentationContextResult.Accept);
        }

        return SendAssociationAcceptAsync(association);
    }

    public Task OnReceiveAssociationReleaseRequestAsync()
    {
        _logger.LogInformation("Received association release");
        return Task.CompletedTask;
    }

    public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request) =>
        Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
}

public class DicomFolderSettings
{
    public string DataPath { get; set; } = "";
}