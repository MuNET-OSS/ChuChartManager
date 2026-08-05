using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using Tomlyn;
using Tomlyn.Model;

namespace ChuChartManager.Services;

public sealed class AppleChuMetadataService
{
    private static readonly byte[] Magic = "ACMANI\0\0"u8.ToArray();
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private const ushort ContainerVersion = 1;
    private const ushort HeaderLength = 64;
    private const int MaximumPayloadLength = 4 * 1024 * 1024;

    public sealed record Metadata(string ManifestToml, string DefaultConfigToml, TomlTable Manifest);

    public Metadata ReadInstalled(string gamePath)
    {
        var path = Path.Combine(gamePath, "bin", "winhttp.dll");
        if (!File.Exists(path))
            throw new FileNotFoundException("AppleChu winhttp.dll not found", path);
        return Read(path);
    }

    public Metadata Read(string path) => Decode(File.ReadAllBytes(path));

    public Metadata Decode(byte[] image)
    {
        try
        {
            using var stream = new MemoryStream(image, writable: false);
            using var peReader = new PEReader(stream);
            var sections = peReader.PEHeaders.SectionHeaders
                .Where(section => string.Equals(section.Name, ".acmani", StringComparison.Ordinal))
                .ToArray();
            if (sections.Length != 1)
                throw new InvalidDataException($"Expected one .acmani section, found {sections.Length}");

            var section = sections[0];
            var offset = section.PointerToRawData;
            var size = section.SizeOfRawData;
            if (offset < 0 || size < HeaderLength || (long)offset + size > image.Length)
                throw new InvalidDataException("The .acmani section is outside the PE image");

            var raw = image.AsSpan(offset, size);
            if (!raw[..Magic.Length].SequenceEqual(Magic))
                throw new InvalidDataException("The .acmani magic is invalid");

            var version = BinaryPrimitives.ReadUInt16LittleEndian(raw[8..10]);
            var headerLength = BinaryPrimitives.ReadUInt16LittleEndian(raw[10..12]);
            if (version != ContainerVersion)
                throw new InvalidDataException($"Unsupported .acmani version: {version}");
            if (headerLength != HeaderLength)
                throw new InvalidDataException($"Unsupported .acmani header length: {headerLength}");
            if (raw[52..HeaderLength].IndexOfAnyExcept((byte)0) >= 0)
                throw new InvalidDataException("The .acmani reserved header bytes are not zero");

            var manifestLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(raw[12..16]));
            var defaultConfigLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(raw[16..20]));
            var payloadLength = checked(manifestLength + defaultConfigLength);
            if (payloadLength > MaximumPayloadLength || HeaderLength + payloadLength > raw.Length)
                throw new InvalidDataException("The .acmani payload length is invalid");

            var payload = raw.Slice(HeaderLength, payloadLength);
            var actualHash = SHA256.HashData(payload);
            if (!CryptographicOperations.FixedTimeEquals(actualHash, raw[20..52]))
                throw new InvalidDataException("The .acmani SHA-256 checksum is invalid");

            var manifestToml = StrictUtf8.GetString(payload[..manifestLength]);
            var defaultConfigToml = StrictUtf8.GetString(payload[manifestLength..]);
            var manifest = TomlSerializer.Deserialize<TomlTable>(manifestToml)
                ?? throw new InvalidDataException("The embedded manifest is empty");
            _ = TomlSerializer.Deserialize<TomlTable>(defaultConfigToml)
                ?? throw new InvalidDataException("The embedded default config is empty");
            ValidateManifest(manifest);
            return new Metadata(manifestToml, defaultConfigToml, manifest);
        }
        catch (Exception error) when (error is not InvalidDataException
                                      and not IOException
                                      and not OutOfMemoryException
                                      and not OperationCanceledException)
        {
            throw new InvalidDataException("AppleChu metadata is invalid", error);
        }
    }

    private static void ValidateManifest(TomlTable manifest)
    {
        if (!manifest.TryGetValue("config", out var configValue) || configValue is not TomlTable config
            || !config.TryGetValue("sections", out var sectionsValue) || sectionsValue is not TomlTableArray sections)
            throw new InvalidDataException("The embedded manifest has no config.sections array");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in sections)
        {
            if (!section.TryGetValue("id", out var idValue) || idValue is not string id || string.IsNullOrWhiteSpace(id))
                throw new InvalidDataException("The embedded manifest contains a section without an ID");
            if (!ids.Add(id))
                throw new InvalidDataException($"Duplicate AppleChu section: {id}");
        }
    }
}
