using ChuChartManager.Models;
using Microsoft.AspNetCore.Mvc;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class ReleaseTagController : ControllerBase
{
    public record ReleaseTagItem(int Id, string VersionStr, string TitleName, string AssetDir, bool IsCustom);
    public record AddReleaseTagRequest(int Id, string AssetDir, string VersionStr = "New Version", string TitleName = "");
    public record EditReleaseTagRequest(string VersionStr, string TitleName);

    [HttpGet]
    public ActionResult<List<ReleaseTagItem>> GetAllReleaseTags()
    {
        var gamePath = StaticSettings.GamePath;
        if (string.IsNullOrEmpty(gamePath)) return Ok(new List<ReleaseTagItem>());

        return Ok(ReleaseTagXml.ScanAll(gamePath)
            .Select(ToItem)
            .OrderBy(x => x.IsCustom)
            .ThenBy(x => x.Id)
            .ToList());
    }

    [HttpGet]
    public ActionResult<Dictionary<int, string>> GetReleaseTagMap()
    {
        var gamePath = StaticSettings.GamePath;
        if (string.IsNullOrEmpty(gamePath)) return Ok(new Dictionary<int, string>());

        return Ok(ReleaseTagXml.ScanAll(gamePath).ToDictionary(x => x.Id, x => x.VersionStr));
    }

    [HttpPost]
    public ActionResult AddReleaseTag([FromBody] AddReleaseTagRequest req)
    {
        var gamePath = StaticSettings.GamePath;
        if (string.IsNullOrEmpty(gamePath)) return BadRequest("GamePath not set");
        if (req.Id < 0) return BadRequest("ID 不能小于 0");
        if (string.IsNullOrWhiteSpace(req.AssetDir)) return BadRequest("Opt 不能为空");
        if (req.AssetDir == "A000") return BadRequest("不能在 A000 创建自定义版本标签");

        var optionDir = OptionPathResolver.ResolveExisting(gamePath, req.AssetDir);
        if (optionDir == null) return BadRequest("Opt 不存在");

        var existing = ReleaseTagXml.ScanAll(gamePath);
        if (existing.Any(x => x.Id == req.Id)) return BadRequest($"ID {req.Id} 已存在");

        ReleaseTagXml.CreateNew(gamePath, req.AssetDir, req.Id, req.VersionStr, req.TitleName);
        return Ok();
    }

    [HttpPost("{id:int}")]
    public ActionResult EditReleaseTag(int id, [FromBody] EditReleaseTagRequest req)
    {
        var gamePath = StaticSettings.GamePath;
        if (string.IsNullOrEmpty(gamePath)) return BadRequest("GamePath not set");

        var tag = ReleaseTagXml.ScanAll(gamePath).FirstOrDefault(x => x.Id == id);
        if (tag == null) return NotFound();
        if (!tag.IsCustom) return BadRequest("不能修改 A000 的版本标签");

        tag.VersionStr = req.VersionStr;
        tag.TitleName = req.TitleName;
        tag.Save();
        return Ok();
    }

    [HttpDelete("{id:int}")]
    public ActionResult DeleteReleaseTag(int id)
    {
        var gamePath = StaticSettings.GamePath;
        if (string.IsNullOrEmpty(gamePath)) return BadRequest("GamePath not set");

        var tag = ReleaseTagXml.ScanAll(gamePath).FirstOrDefault(x => x.Id == id);
        if (tag == null) return NotFound();
        if (!tag.IsCustom) return BadRequest("不能删除 A000 的版本标签");

        tag.Delete();
        return Ok();
    }

    private static ReleaseTagItem ToItem(ReleaseTagXml tag) => new(tag.Id, tag.VersionStr, tag.TitleName, tag.AssetDir, tag.IsCustom);
}
