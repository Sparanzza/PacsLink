namespace PacsLink.Core.DicomServices;

public interface IStudyService
{
    public List<string> GetAllStudyUids();
}

public class StudyService : IStudyService
{
    public List<string> GetAllStudyUids()
    {
        return new List<string> { "UID1", "UID2", "UID3" };
    }
}