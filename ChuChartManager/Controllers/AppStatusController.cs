using Microsoft.AspNetCore.Mvc;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class AppStatusController : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<string>> GetStartupErrors()
    {
        return Ok(StaticSettings.Scanner?.Errors ?? []);
    }
}
