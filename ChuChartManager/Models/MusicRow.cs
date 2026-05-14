using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ChuChartManager.Models;

namespace ChuChartManager;

public class MusicRow
{
    public int Id { get; set; }
    public string IdDisplay { get; set; } = "";
    public string Name { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Genre { get; set; } = "";
    public string? JacketPath { get; set; }
    public ObservableCollection<DiffBadge> Badges { get; } = [];
    public MusicXml? Music { get; set; }

    private BitmapSource? _jacketSource;
    private bool _jacketLoaded;

    public BitmapSource? JacketSource
    {
        get
        {
            if (_jacketLoaded) return _jacketSource;
            _jacketLoaded = true;
            if (JacketPath == null) return null;
            try
            {
                if (JacketPath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                {
                    using var pfim = Pfim.Pfimage.FromFile(JacketPath);
                    if (pfim.Compressed) pfim.Decompress();
                    var fmt = pfim.Format == Pfim.ImageFormat.Rgba32
                        ? PixelFormats.Bgra32 : PixelFormats.Bgr24;
                    _jacketSource = BitmapSource.Create(
                        pfim.Width, pfim.Height, 96, 96, fmt, null,
                        pfim.Data, pfim.Stride);
                    _jacketSource.Freeze();
                }
                else
                {
                    var img = new BitmapImage(new Uri(JacketPath));
                    img.Freeze();
                    _jacketSource = img;
                }
            }
            catch { _jacketSource = null; }
            return _jacketSource;
        }
    }
}
