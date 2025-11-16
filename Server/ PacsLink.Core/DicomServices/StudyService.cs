using Microsoft.Extensions.Options;
using PacsLink.Core.Configuration;

namespace PacsLink.Core.DicomServices;

public interface IStudyService
{
    public List<string> GetAllStudyUids();
}

public class StudyService : IStudyService
{
    private readonly StorageSettings _storageSettings;

    public StudyService(IOptions<StorageSettings> storageSettings)
    {
        _storageSettings = storageSettings.Value;
    }

    public List<string> GetAllStudyUids()
    {
        var storagePath = _storageSettings.DicomStoragePath;

        if (string.IsNullOrEmpty(storagePath) || !Directory.Exists(storagePath))
        {
            return new List<string>();
        }

        var studyDirectories = Directory.GetDirectories(storagePath);
        return studyDirectories.Select(Path.GetFileName).ToList()!;
    }
}