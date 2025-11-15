using Microsoft.AspNetCore.Mvc;

namespace PacsLink.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ModalityController : ControllerBase
{
    
    private readonly ILogger<ModalityController> _logger;

    public ModalityController(ILogger<ModalityController> logger)
    {
        _logger = logger;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public bool Get()
    {
        return true;
    }
}