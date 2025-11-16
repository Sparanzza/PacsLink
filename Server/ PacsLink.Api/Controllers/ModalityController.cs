using Microsoft.AspNetCore.Mvc;
using PacsLink.Core.DicomServices;

namespace PacsLink.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ModalityController : ControllerBase
{
    private readonly ILogger<ModalityController> _logger;
    private readonly IStudyService _studyService;

    public ModalityController(ILogger<ModalityController> logger, IStudyService studyService)
    {
        _logger = logger;
        _studyService = studyService;
    }

    [HttpGet]
    public List<string> GetStudies()
    {
        return _studyService.GetAllStudyUids();
    }
}