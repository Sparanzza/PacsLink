using Microsoft.AspNetCore.Mvc;
using PacsLink.Core.DicomServices;

namespace PacsLink.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ModalityController : ControllerBase
{
    private readonly ILogger<ModalityController> _logger;
    private readonly IStudyService _studyService;
    private readonly IScuService _scuStudy;

    public ModalityController(ILogger<ModalityController> logger, IStudyService studyService, IScuService scuService)
    {
        _logger = logger;
        _studyService = studyService;
        _scuStudy = scuService;
    }

    [HttpGet]
    public List<string> GetStudies()
    {
        return _studyService.GetAllStudyUids();
    }
}