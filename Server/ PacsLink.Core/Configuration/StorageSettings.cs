namespace PacsLink.Core.Configuration;

public class StorageSettings
{
    public const string SectionName = "Storage";
    public string DicomStoragePath { get; init; } = string.Empty;
}