using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using Microsoft.Extensions.Logging;

namespace PacsLink.Core.DicomServices;

/// <summary>
/// Define el contrato para un servicio de cliente DICOM (SCU).
/// </summary>
public interface IScuService
{
    /// <summary>
    /// Envía un archivo DICOM a un servidor remoto (SCP).
    /// </summary>
    /// <param name="filePath">Ruta del archivo a enviar.</param>
    /// <param name="host">Dirección IP o nombre del host del servidor.</param>
    /// <param name="port">Puerto del servidor.</param>
    /// <param name="useTls">Indica si se debe usar una conexión segura.</param>
    /// <param name="callingAe">Nuestro AE Title (quiénes somos).</param>
    /// <param name="calledAe">El AE Title del servidor al que nos conectamos.</param>
    /// <returns>True si el envío fue exitoso, False en caso contrario.</returns>
    Task<bool> SendFileAsync(string filePath, string host, int port, bool useTls, string callingAe, string calledAe);
}

/// <summary>
/// Implementación del servicio de cliente DICOM (SCU).
/// </summary>
public class ScuService : IScuService
{
    private readonly IDicomClientFactory _dicomClientFactory;
    private readonly ILogger<ScuService> _logger;

    public ScuService(IDicomClientFactory dicomClientFactory, ILogger<ScuService> logger)
    {
        _dicomClientFactory = dicomClientFactory;
        _logger = logger;
    }

    public async Task<bool> SendFileAsync(string filePath, string host, int port, bool useTls, string callingAe, string calledAe)
    {
        try
        {
            var client = _dicomClientFactory.Create(host, port, useTls, callingAe, calledAe);
            var request = new DicomCStoreRequest(filePath);

            var tcs = new TaskCompletionSource<bool>();
            request.OnResponseReceived = (req, response) =>
            {
                _logger.LogInformation("C-STORE response received with status: {Status}", response.Status);
                tcs.SetResult(response.Status == DicomStatus.Success);
            };

            await client.AddRequestAsync(request);
            await client.SendAsync();

            return await tcs.Task;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error sending file DICOM on C-STORE.");
            return false;
        }
    }
}