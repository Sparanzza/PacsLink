using System.Text;
using FellowOakDicom;
using FellowOakDicom.Network;
using Microsoft.Extensions.Logging;

namespace PacsLink.Core.DicomServices;

/// <summary>
/// This class implements the DICOM C-STORE SCP (Service Class Provider) functionality.
/// Its main responsibility is to receive and store DICOM files.
/// </summary>
public class ScpService : DicomService, IDicomServiceProvider, IDicomCEchoProvider, IDicomCStoreProvider
{
    private readonly ILogger<ScpService> _logger;

    private static readonly DicomTransferSyntax[] AcceptedTransferSyntaxes =
    {
        DicomTransferSyntax.ExplicitVRLittleEndian,
        DicomTransferSyntax.ImplicitVRLittleEndian,
        DicomTransferSyntax.ImplicitVRBigEndian,
        DicomTransferSyntax.JPEGLSLossless,
    };

    public ScpService(INetworkStream stream, Encoding fallbackEncoding, ILogger<ScpService> logger,
        DicomServiceDependencies dependencies)
        : base(stream, fallbackEncoding, logger, dependencies)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
    {
        _logger.LogInformation("Received association request from: {CallingAE} to {CalledAE}", association.CallingAE, association.CalledAE);

        if (association.CalledAE != "AnySCP")
        {
            _logger.LogError("Incorrect AE Title. Expected 'AnySCP' but received '{CalledAE}'. Rejecting.", association.CalledAE);
            return SendAssociationRejectAsync(
                DicomRejectResult.Permanent,
                DicomRejectSource.ServiceUser,
                DicomRejectReason.CalledAENotRecognized);
        }

        foreach (var pc in association.PresentationContexts)
        {
            if (pc.AbstractSyntax.StorageCategory != DicomStorageCategory.None)
            {
                var acceptedSyntax = pc.GetTransferSyntaxes()
                    .FirstOrDefault(ts => AcceptedTransferSyntaxes.Contains(ts));

                if (acceptedSyntax != null)
                {
                    pc.SetResult(DicomPresentationContextResult.Accept, acceptedSyntax);
                }
                else
                {
                    pc.SetResult(DicomPresentationContextResult.RejectAbstractSyntaxNotSupported);
                }
            }
            else
            {
                pc.SetResult(DicomPresentationContextResult.Accept);
            }
        }

        _logger.LogInformation("Association Accepted.");
        return SendAssociationAcceptAsync(association);
    }

    public async Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
    {
        var studyUid = request.Dataset.GetString(DicomTag.StudyInstanceUID);
        var instUid = request.SOPInstanceUID.UID;

        // TODO: This path should come from configuration
        var path = Path.Combine(@"C:\DicomStorage", studyUid);
        Directory.CreateDirectory(path);

        var filePath = Path.Combine(path, instUid + ".dcm");
        await request.File.SaveAsync(filePath);

        _logger.LogInformation("Image received and saved at: {FilePath}", filePath);

        // In a real application, you would now call a service to add this to a database.
        // e.g., await _studyService.AddImageToIndexAsync(filePath, studyUid, instUid);

        return new DicomCStoreResponse(request, DicomStatus.Success);
    }

    public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
    {
        _logger.LogInformation("Received C-ECHO request.");
        return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
    }

    public Task OnReceiveAssociationReleaseRequestAsync()
    {
        _logger.LogInformation("Received association release request.");
        return SendAssociationReleaseResponseAsync();
    }

    public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason) =>
        _logger.LogWarning("Received abort from {Source} with reason {Reason}", source, reason);

    public void OnConnectionClosed(Exception? exception) =>
        _logger.LogInformation("Connection closed. Exception: {Exception}", exception?.Message ?? "No error");

    public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e) =>
        Task.FromException(new Exception($"Error processing C-STORE request for file {tempFileName}.", e));
}