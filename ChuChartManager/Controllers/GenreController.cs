using ChuChartManager.Models;
using Microsoft.AspNetCore.Mvc;

namespace ChuChartManager.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class GenreController : ControllerBase
{
    public record GenreItem(int Id, string Name, string AssetDir, int ColorR, int ColorG, int ColorB, bool IsCustom);
    public record AddGenreRequest(int Id, string AssetDir, string Name = "New Genre", int ColorR = 110, int ColorG = 217, int ColorB = 67);
    public record EditGenreRequest(string Name, int ColorR, int ColorG, int ColorB);

    [HttpGet]
    public ActionResult<List<GenreItem>> GetAllGenres()
    {
        var gamePath = StaticSettings.GamePath;
        if (string.IsNullOrEmpty(gamePath)) return Ok(new List<GenreItem>());
        return Ok(BuildGenreItems(gamePath, StaticSettings.Scanner));
    }

    [HttpPost]
    public ActionResult AddGenre([FromBody] AddGenreRequest req)
    {
        var gamePath = StaticSettings.GamePath;
        if (string.IsNullOrEmpty(gamePath)) return BadRequest("GamePath not set");
        if (string.IsNullOrWhiteSpace(req.AssetDir)) return BadRequest("Opt 不能为空");
        if (req.AssetDir == "A000") return BadRequest("不能在 A000 创建自定义流派");

        var existing = BuildGenreItems(gamePath, StaticSettings.Scanner);
        var sort = GenreSortXml.LoadOrCreate(gamePath, req.AssetDir);
        if (existing.Any(g => g.Id == req.Id) && sort.Contains(req.Id)) return BadRequest($"ID {req.Id} 已存在");

        sort.Add(req.Id, req.Name);
        sort.Save();
        return Ok();
    }

    [HttpPost("{id:int}")]
    public ActionResult EditGenre(int id, [FromBody] EditGenreRequest req)
    {
        var gamePath = StaticSettings.GamePath;
        if (string.IsNullOrEmpty(gamePath)) return BadRequest("GamePath not set");

        var edited = false;
        foreach (var sort in GenreSortXml.ScanAll(gamePath).Where(s => s.Contains(id)))
        {
            sort.SetName(id, req.Name);
            sort.Save();
            edited = true;
        }

        foreach (var music in EnumerateMusics(StaticSettings.Scanner))
        {
            if (music.GenreId != id) continue;
            var root = music.XmlDoc.SelectSingleNode("/MusicData");
            var genreStrNode = root?.SelectSingleNode("genreNames/list/StringID/str");
            if (genreStrNode == null) continue;
            genreStrNode.InnerText = req.Name;
            music.Genres = [req.Name];
            music.Save();

            var sort = GenreSortXml.LoadOrCreate(gamePath, music.AssetDir);
            sort.Add(id, req.Name);
            sort.Save();
            edited = true;
        }

        return edited ? Ok() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public ActionResult DeleteGenre(int id)
    {
        var gamePath = StaticSettings.GamePath;
        if (string.IsNullOrEmpty(gamePath)) return BadRequest("GamePath not set");

        var deleted = false;
        foreach (var sort in GenreSortXml.ScanAll(gamePath).Where(s => s.Contains(id)))
        {
            sort.Remove(id);
            sort.Save();
            deleted = true;
        }

        return deleted ? Ok() : NotFound();
    }

    private static List<GenreItem> BuildGenreItems(string gamePath, MusicScanner? scanner)
    {
        var map = new Dictionary<int, GenreItem>();
        foreach (var music in EnumerateMusics(scanner))
        {
            if (music.GenreId < 0 || map.ContainsKey(music.GenreId)) continue;
            var name = music.Genres.Count > 0 ? music.Genres[0] : "";
            if (!string.IsNullOrEmpty(name))
                map[music.GenreId] = new GenreItem(music.GenreId, name, music.AssetDir, 110, 217, 67, music.AssetDir != "A000");
        }

        foreach (var sort in GenreSortXml.ScanAll(gamePath))
        {
            foreach (var (id, name) in sort.Entries)
            {
                if (map.ContainsKey(id)) continue;
                map[id] = new GenreItem(id, string.IsNullOrWhiteSpace(name) ? $"Genre {id}" : name, sort.AssetDir, 110, 217, 67, sort.AssetDir != "A000");
            }
        }

        return map.Values.OrderBy(g => g.IsCustom).ThenBy(g => g.Id).ToList();
    }

    private static IEnumerable<MusicXml> EnumerateMusics(MusicScanner? scanner)
    {
        if (scanner == null) yield break;
        foreach (var (_, musics) in scanner.MusicBySource)
        foreach (var music in musics)
            yield return music;
    }
}
