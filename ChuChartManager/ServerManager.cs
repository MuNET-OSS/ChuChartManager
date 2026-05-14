using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.FileProviders;

namespace ChuChartManager;

public static class ServerManager
{
    public static WebApplication? App { get; private set; }

    public static bool IsRunning => App != null;

    public static async Task StopAsync()
    {
        if (App == null) return;
        await App.StopAsync();
        await App.DisposeAsync();
        App = null;
    }

    private static X509Certificate2 GetCert()
    {
        var path = Path.Combine(StaticSettings.AppDataDir, "cert.pfx");
        if (File.Exists(path))
        {
            try
            {
                return new X509Certificate2(path, (string?)null, X509KeyStorageFlags.EphemeralKeySet);
            }
            catch
            {
                File.Delete(path);
            }
        }

        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=ChuChartManager", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], true));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("ChuChartManager");
        san.AddDnsName("localhost");
        req.CertificateExtensions.Add(san.Build());

        var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));
        var pfxBytes = cert.Export(X509ContentType.Pfx);
        File.WriteAllBytes(path, pfxBytes);
        return new X509Certificate2(pfxBytes, (string?)null, X509KeyStorageFlags.EphemeralKeySet);
    }

    public static void StartApp(bool export = false, Action<string>? onStart = null)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Listen(IPAddress.Loopback, 0);
            if (export)
            {
                serverOptions.Listen(IPAddress.Any, 5001);
            }
        });

        builder.Services
            .AddSingleton(new MusicScannerService())
            .AddEndpointsApiExplorer()
            .Configure<FormOptions>(x =>
            {
                x.ValueLengthLimit = int.MaxValue;
                x.MultipartBodyLengthLimit = long.MaxValue;
            })
            .AddCors(options => options.AddPolicy("ccm", policy =>
            {
                policy.WithOrigins("https://ccm.invalid")
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            }))
            .AddProblemDetails()
            .AddControllers()
            .AddApplicationPart(typeof(ServerManager).Assembly)
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        if (StaticSettings.Config.UseAuth && export)
        {
            builder.Services.AddAuthentication("BasicAuth")
                .AddScheme<BasicAuthOptions, BasicAuthHandler>("BasicAuth", null);
            builder.Services.AddAuthorization();
        }

        App = builder.Build();

        if (onStart != null)
            App.Lifetime.ApplicationStarted.Register(() =>
            {
                var url = GetLoopbackUrl();
                if (url != null) onStart(url);
            });

        if (StaticSettings.Config.UseAuth && export)
        {
            App.UseAuthentication();
            App.UseAuthorization();
        }

        App
            .UseExceptionHandler()
            .UseStatusCodePages()
            .UseCors("ccm");
        if (export)
            App.UseFileServer(new FileServerOptions
            {
                FileProvider = new PhysicalFileProvider(StaticSettings.Wwwroot),
            });
        App.MapControllers();
        Task.Run(App.Run);
    }

    public static string? GetLoopbackUrl()
    {
        var server = App?.Services.GetRequiredService<IServer>();
        var addressesFeature = server?.Features.Get<IServerAddressesFeature>();
        return addressesFeature?.Addresses.FirstOrDefault();
    }
}

public class MusicScannerService
{
    public MusicScanner? Scanner => StaticSettings.Scanner;
}
