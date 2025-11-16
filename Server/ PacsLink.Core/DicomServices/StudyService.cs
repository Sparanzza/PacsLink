using FellowOakDicom;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacsLink.Core.Configuration;
using PacsLink.Core.DTOs;

namespace PacsLink.Core.DicomServices;

/// <summary>
/// Defines the contract for a service that handles DICOM study operations.
/// </summary>
public interface IStudyService
{
    /// <summary>
    /// Asynchronously gets a list of all available studies with their metadata.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of study DTOs.</returns>
    Task<List<StudyDto>> GetAllStudies();
}

/// <summary>
/// Service to handle DICOM study-related operations, like querying study information.
/// </summary>
public class StudyService : IStudyService
{
    private readonly StorageSettings _storageSettings;
    private readonly ILogger<StudyService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StudyService"/> class.
    /// </summary>
    /// <param name="storageSettings">The storage configuration, injected via IOptions.</param>
    /// <param name="logger">The logger for logging events, injected by the framework.</param>
    public StudyService(IOptions<StorageSettings> storageSettings, ILogger<StudyService> logger)
    {
        _storageSettings = storageSettings.Value;
        _logger = logger;
    }

    public async Task<List<StudyDto>> GetAllStudies()
    {
        var storagePath = _storageSettings.DicomStoragePath;
        var studies = new List<StudyDto>();

        if (string.IsNullOrEmpty(storagePath) || !Directory.Exists(storagePath))
        {
            _logger.LogWarning("DICOM storage path '{Path}' is not valid or does not exist.", storagePath);
            return studies;
        }

        var studyDirectories = Directory.GetDirectories(storagePath);

        foreach (var studyDir in studyDirectories)
        {
            // Find the first .dcm file in the study directory.
            var firstDicomFile = Directory.EnumerateFiles(studyDir, "*.dcm", SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (firstDicomFile == null)
            {
                _logger.LogInformation("Study folder '{Directory}' does not contain any .dcm files.", studyDir);
                continue; // Move on to the next folder.
            }

            try
            {
                var dicomFile = await DicomFile.OpenAsync(firstDicomFile);
                var dataset = dicomFile.Dataset;

                var studyDto = new StudyDto
                {
                    // Read DICOM tags and assign them to the DTO.
                    PatientName = dataset.GetSingleValueOrDefault(DicomTag.PatientName, "N/A"),
                    StudyDate = dataset.GetSingleValueOrDefault(DicomTag.StudyDate, "N/A"),
                    StudyDescription = dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, "N/A"),
                    StudyInstanceUid = dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, Path.GetFileName(studyDir))
                };
                studies.Add(studyDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing DICOM file '{File}'.", firstDicomFile);
            }
        }

        return studies;
    }
}