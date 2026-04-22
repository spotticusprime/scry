using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Scry.Core;
using Scry.Probes.Executors;

namespace Scry.Probes.Tests;

public class TlsProbeExecutorTests
{
    private static readonly Guid WsId = Guid.NewGuid();

    private static Probe MakeProbe(string yaml) => new()
    {
        WorkspaceId = WsId,
        Name = "test",
        Kind = "tls",
        Definition = yaml,
    };

    // Creates a self-signed cert with the given validity window.
    private static X509Certificate2 CreateCert(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=test.local", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return req.CreateSelfSigned(notBefore, notAfter);
    }

    // Starts a minimal TLS server on loopback; returns the port.
    // The returned CancellationTokenSource must be cancelled to shut down.
    private static (int port, CancellationTokenSource cts) StartTlsServer(X509Certificate2 cert)
    {
        var cts = new CancellationTokenSource();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        _ = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync(cts.Token); }
                catch { break; }
                _ = ServeOneAsync(client, cert);
            }
            listener.Stop();
        });

        return (port, cts);
    }

    private static async Task ServeOneAsync(TcpClient client, X509Certificate2 cert)
    {
        using var _ = client;
        try
        {
            using var ssl = new SslStream(client.GetStream(), false);
            await ssl.AuthenticateAsServerAsync(cert);
            await Task.Delay(500);
        }
        catch { }
    }

    [Fact]
    public async Task Returns_Ok_When_Cert_Has_Plenty_Of_Time()
    {
        using var cert = CreateCert(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(90));
        var (port, cts) = StartTlsServer(cert);
        try
        {
            var executor = new TlsProbeExecutor();
            var probe = MakeProbe($"host: 127.0.0.1\nport: {port}\nwarn_days: 30\ncrit_days: 7");

            var result = await executor.ExecuteAsync(probe, CancellationToken.None);

            Assert.Equal(ProbeOutcome.Ok, result.Outcome);
            Assert.Contains("days", result.Message);
            Assert.Equal("127.0.0.1", result.Attributes["host"]);
            Assert.Equal(port.ToString(), result.Attributes["port"]);
            Assert.True(int.Parse(result.Attributes["days_left"]) > 30);
        }
        finally { await cts.CancelAsync(); }
    }

    [Fact]
    public async Task Returns_Warn_When_Cert_Expires_Within_Warn_Days()
    {
        using var cert = CreateCert(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(15));
        var (port, cts) = StartTlsServer(cert);
        try
        {
            var executor = new TlsProbeExecutor();
            var probe = MakeProbe($"host: 127.0.0.1\nport: {port}\nwarn_days: 30\ncrit_days: 7");

            var result = await executor.ExecuteAsync(probe, CancellationToken.None);

            Assert.Equal(ProbeOutcome.Warn, result.Outcome);
        }
        finally { await cts.CancelAsync(); }
    }

    [Fact]
    public async Task Returns_Crit_When_Cert_Expires_Within_Crit_Days()
    {
        using var cert = CreateCert(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(3));
        var (port, cts) = StartTlsServer(cert);
        try
        {
            var executor = new TlsProbeExecutor();
            var probe = MakeProbe($"host: 127.0.0.1\nport: {port}\nwarn_days: 30\ncrit_days: 7");

            var result = await executor.ExecuteAsync(probe, CancellationToken.None);

            Assert.Equal(ProbeOutcome.Crit, result.Outcome);
        }
        finally { await cts.CancelAsync(); }
    }

    [Fact]
    public async Task Returns_Crit_When_Port_Is_Closed()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var executor = new TlsProbeExecutor();
        var probe = MakeProbe($"host: 127.0.0.1\nport: {port}");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.Equal(ProbeOutcome.Crit, result.Outcome);
    }

    [Fact]
    public async Task Returns_NotOk_On_Timeout()
    {
        // 192.0.2.0/24 is TEST-NET; may time out or refuse depending on routing.
        var executor = new TlsProbeExecutor();
        var probe = MakeProbe("host: 192.0.2.1\nport: 443\ntimeout: 00:00:00.100");

        var result = await executor.ExecuteAsync(probe, CancellationToken.None);

        Assert.NotEqual(ProbeOutcome.Ok, result.Outcome);
    }

    [Fact]
    public async Task Result_Has_Correct_Ids()
    {
        using var cert = CreateCert(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(90));
        var (port, cts) = StartTlsServer(cert);
        try
        {
            var executor = new TlsProbeExecutor();
            var probe = MakeProbe($"host: 127.0.0.1\nport: {port}");

            var result = await executor.ExecuteAsync(probe, CancellationToken.None);

            Assert.Equal(WsId, result.WorkspaceId);
            Assert.Equal(probe.Id, result.ProbeId);
            Assert.True(result.DurationMs >= 0);
            Assert.True(result.CompletedAt >= result.StartedAt);
        }
        finally { await cts.CancelAsync(); }
    }
}
