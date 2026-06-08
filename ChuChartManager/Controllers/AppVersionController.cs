using Microsoft.AspNetCore.Mvc;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class AppVersionController : ControllerBase
{
    public record AppVersionResult(string Version, int GameVersion, string GameVersionStr);

    [HttpGet]
    public ActionResult<AppVersionResult> GetAppVersion()
    {
        return Ok(new AppVersionResult(
            AppMain.Version,
            StaticSettings.GameVersion,
            StaticSettings.GameVersionStr
        ));
    }
}