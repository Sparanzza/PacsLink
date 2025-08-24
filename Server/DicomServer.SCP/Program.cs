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
        services.AddImageManager<ImageSharpImageManager>();
    })
    .Build();

DicomSetupBuilder.UseServiceProvider(host.Services);

await host.RunAsync();

internal class Worker : IHostedService
{
    readonly ILogger<Worker> _logger;
    readonly IDicomServerFactory _dicomServerFactory;
    readonly IDicomClientFactory _dicomClientFactory;
    IDicomServer _dicomServer;

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
        _dicomServer = _dicomServerFactory.Create<MyDicomService>(104);
        _logger.LogInformation("DICOM server is running");

        var client = _dicomClientFactory.Create("127.0.0.1", 104, false, "STORESCU", "AnySCP");

        string dicomFileToSend = @"C:\DicomStorage\dicom_examples\image-000001.dcm"; 

        if (!File.Exists(dicomFileToSend))
        {
            _logger.LogError($"The DICOM file does not exist at path: {dicomFileToSend}");
            return;
        }

        _logger.LogInformation($"Sending C-STORE request for file: {dicomFileToSend}");

        var request = new DicomCStoreRequest(dicomFileToSend)
        {
            OnResponseReceived = (DicomCStoreRequest _, DicomCStoreResponse response) =>
            {
                if (response.Status == DicomStatus.Success)
                {
                    _logger.LogInformation("The SCP server has successfully accepted and saved the image.");
                }
                else
                {
                    _logger.LogError($"The SCP server returned an error: {response.Status}");
                }
            }
        };

        // Add the request to the client and send it.
        await client.AddRequestAsync(request);
        await client.SendAsync(cancellationToken);
        
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

public class MyDicomService : DicomService, IDicomServiceProvider, IDicomCEchoProvider, IDicomCStoreProvider
{
    private readonly ILogger _logger;

    private static readonly DicomTransferSyntax[] AcceptedTransferSyntaxes =
    {
        DicomTransferSyntax.ExplicitVRLittleEndian,
        DicomTransferSyntax.ImplicitVRLittleEndian,
        DicomTransferSyntax.ImplicitVRBigEndian,
        DicomTransferSyntax.JPEGLSLossless,
    };

    public MyDicomService(INetworkStream stream, Encoding fallbackEncoding, ILogger logger,
        DicomServiceDependencies dependencies)
        : base(stream, fallbackEncoding, logger, dependencies)
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
        
        // Now, for each service requested by the client...
        foreach (var pc in association.PresentationContexts)
        {
            // If the service is Storage (C-STORE)...
            if (pc.AbstractSyntax.StorageCategory != DicomStorageCategory.None)
            {
                // ...we accept it if the transfer syntax is one we support.
                var acceptedSyntax = pc.GetTransferSyntaxes()
                    .FirstOrDefault(ts => AcceptedTransferSyntaxes.Contains(ts));

                if(acceptedSyntax != null)
                {
                    pc.SetResult(DicomPresentationContextResult.Accept, acceptedSyntax);
                }
                else
                {
                    pc.SetResult(DicomPresentationContextResult.RejectAbstractSyntaxNotSupported);
                }
            }
            // Otherwise, we let it pass (in this simplified example, we accept the rest)
            else
            {
                pc.SetResult(DicomPresentationContextResult.Accept);
            }
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

    // Request CStore
    public async Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
    {
        var studyUid = request.Dataset.GetString(DicomTag.StudyInstanceUID);
        var instUid = request.SOPInstanceUID.UID;
        
        var path = Path.Combine(@"C:\DicomStorage", studyUid); // Save in a folder per study
        Directory.CreateDirectory(path);

        var filePath = Path.Combine(path, instUid + ".dcm");
        await request.File.SaveAsync(filePath);

        _logger.LogInformation($"Image received and saved at: {filePath}");

        // Returning a success response
        return new DicomCStoreResponse(request, DicomStatus.Success);
    }

    public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
    {
        throw new Exception(e.Message);
    }
}

public class DicomFolderSettings
{
    public string DataPath { get; set; } = "";
}