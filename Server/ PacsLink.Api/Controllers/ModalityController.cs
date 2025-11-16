using Microsoft.AspNetCore.Mvc;
using PacsLink.Core.DicomServices;
using PacsLink.Core.DTOs; // Add this using for StudyDto

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

    /// <summary>
    /// Gets a list of all available DICOM studies.
    /// </summary>
    /// <returns>A list of studies with their metadata.</returns>
    [HttpGet]
    public async Task<ActionResult<List<StudyDto>>> GetStudies()
    {
        var studies = await _studyService.GetAllStudies();
        return Ok(studies);
    }
}