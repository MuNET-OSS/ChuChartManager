using ChuChartManager.Models;
using ChuChartManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class MateController(
    MateCatalogService mateCatalog,
    MateThumbnailService mateThumbnails,
    EmoteWebGlService emoteWebGl) : ControllerBase
{
    [HttpGet]
    public ActionResult<List<MateEntry>> GetMateList([FromQuery] string? source = null)
    {
        return Ok(mateCatalog.GetMates(source));
    }

    [HttpGet]
    public ActionResult GetMateThumbnail([FromQuery] string assetDir, [FromQuery] string mateId)
    {
        var mate = mateCatalog.FindMate(assetDir, mateId);
        if (mate?.ThumbnailPath == null)
            return NotFound();

        var image = mateThumbnails.GetPng(mate.ThumbnailPath);
        return image == null ? NotFound() : File(image, "image/png");
    }

    [HttpGet]
    public ActionResult GetMateWebGLData([FromQuery] string assetDir, [FromQuery] string mateId)
    {
        var mate = mateCatalog.FindMate(assetDir, mateId);
        if (mate == null)
            return NotFound();

        if (!emoteWebGl.TryConvert(mate.EmotePath, out var data, out var error))
            return BadRequest(error);

        return File(data, "application/octet-stream", mate.Entry.Id + ".pure.psb");
    }
}
