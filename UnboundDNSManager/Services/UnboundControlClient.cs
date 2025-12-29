using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using UnboundDNSManager.Models;

namespace UnboundDNSManager.Services;

public class UnboundControlClient : IDisposable
{
    private const int UnboundControlVersion = 1;
    private const int ConnectTimeoutMs = 5000;

    private readonly UnboundConnectionOptions _options;
    private readonly ILogger<UnboundControlClient>? _logger;

    private TcpClient? _client;
    private Stream? _stream;

    public UnboundControlClient(UnboundConnectionOptions options, ILogger<UnboundControlClient>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        ValidateOptions();
    }

    /// <summary>
    /// Indicates whether the client is currently connected to the server.
    /// </summary>
    public bool IsConnected => _client?.Connected ?? false;

    public async Task ConnectAsync()
    {
        _client = new TcpClient();

        using var cts = new System.Threading.CancellationTokenSource(ConnectTimeoutMs);
        try
        {
            if (IsConnected)
            {
                _logger?.LogDebug("Already connected to Unbound server");
                return;
            }

            await _client.ConnectAsync(_options.Host, _options.Port, cts.Token);
            _logger?.LogInformation("Successfully connected to Unbound server at {Host}:{Port}",
                _options.Host, _options.Port);

        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Connection timeout: could not connect to server");
        }

        if (_options.UseSSL)
        {
            await SetupSSLAsync();
        }
        else
        {
            _stream = _client.GetStream();
        }
    }

    private async Task SetupSSLAsync()
    {
        if (string.IsNullOrEmpty(_options.ServerCertFile) ||
                string.IsNullOrEmpty(_options.ControlCertFile) ||
                string.IsNullOrEmpty(_options.ControlKeyFile))
        {
            throw new InvalidOperationException(
                "SSL certificates must be configured when UseSSL is true");
        }

        var sslStream = new SslStream(
            _client!.GetStream(),
            false,
            ValidateServerCertificate,
            null
        );

        var clientCert = LoadCertificateWithKey(_options.ControlCertFile, _options.ControlKeyFile);
        var clientCerts = new X509CertificateCollection { clientCert };

        await sslStream.AuthenticateAsClientAsync(
            _options.Host,
            clientCerts,
            System.Security.Authentication.SslProtocols.Tls12 |
            System.Security.Authentication.SslProtocols.Tls13,
            false
        );

        _stream = sslStream;
    }

    private bool ValidateServerCertificate(
            object sender,
            X509Certificate? certificate,
            X509Chain? chain,
            SslPolicyErrors sslPolicyErrors)
    {
        if (certificate == null)
        {
            _logger?.LogWarning("Server presented no certificate");
            return false;
        }

        if (string.IsNullOrEmpty(_options.ServerCertFile))
            return false;

        try
        {
            using var serverCert = X509CertificateLoader.LoadCertificateFromFile(_options.ServerCertFile);
            var isValid = certificate.GetCertHashString() == serverCert.GetCertHashString();

            if (!isValid)
            {
                _logger?.LogWarning("Server certificate validation failed: thumbprint mismatch");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error validating server certificate");
            return false;
        }
    }

    private static X509Certificate2 LoadCertificateWithKey(string certPath, string keyPath)
    {
        if (!File.Exists(certPath))
            throw new FileNotFoundException($"Certificate file not found: {certPath}");

        if (!File.Exists(keyPath))
            throw new FileNotFoundException($"Key file not found: {keyPath}");

        try
        {
            var certPem = File.ReadAllText(certPath);
            var keyPem = File.ReadAllText(keyPath);

            return X509Certificate2.CreateFromPem(certPem, keyPem);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load certificate from {certPath} and {keyPath}", ex);
        }
    }

    public async Task<(bool success, string output)> SendCommandAsync(string[] command, CancellationToken cancellationToken, string? stdinData = null)
    {
        if (_stream == null || !_stream.CanWrite)
        {
            _logger?.LogError("Not connected to server");
            throw new InvalidOperationException("Not connected to server");
        }

        _logger?.LogDebug("Sending command: {Command}", string.Join(" ", command));

        // Build command string
        var commandBuilder = new StringBuilder(256);
        commandBuilder.Append($"UBCT{UnboundControlVersion} ");
        commandBuilder.AppendJoin(' ', command);
        commandBuilder.Append('\n');

        var commandBytes = Encoding.ASCII.GetBytes(commandBuilder.ToString());
        await _stream.WriteAsync(commandBytes, cancellationToken);
        await _stream.FlushAsync(cancellationToken);

        if (!string.IsNullOrEmpty(stdinData) && RequiresStdinData(command[0]))
        {
            _logger?.LogDebug("Sending stdin data for command: {Command}", command[0]);
            byte[] dataBytes = Encoding.ASCII.GetBytes(stdinData);
            await _stream.WriteAsync(dataBytes);

            // Send EOF marker for certain commands
            if (command[0] != "load_cache")
            {
                await SendEOFMarkerAsync(cancellationToken);
            }

            await _stream.FlushAsync(cancellationToken);
        }

        var response = new StringBuilder();
        bool wasError = false;
        bool firstLine = true;

        byte[] buffer = new byte[4096];
        while (true)
        {
            int bytesRead = await _stream.ReadAsync(buffer);
            if (bytesRead == 0)
                break;

            string chunk = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            response.Append(chunk);

            if (firstLine && chunk.StartsWith("error"))
            {
                wasError = true;
                _logger?.LogWarning("Command returned error: {Command}", string.Join(" ", command));
            }
            firstLine = false;

            if (bytesRead < buffer.Length)
                break;
        }

        _logger?.LogDebug("Command {Status}: {Command}",
                wasError ? "failed" : "succeeded",
                string.Join(" ", command));

        return (!wasError, response.ToString());
    }

    private async Task SendEOFMarkerAsync(CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> eof = new byte[] { 0x04, 0x0a };
        await _stream!.WriteAsync(eof, cancellationToken);
    }

    private static bool RequiresStdinData(string command)
    {
        return command switch
        {
            "verbosity" => true,
            "local_zone" => true,
            "local_zone_remove" => true,
            "local_data" => true,
            "local_data_remove" => true,
            "lookup" => true,
            "flush" => true,
            "flush_type" => true,
            "flush_zone" => true,
            "flush_infra" => true,
            "set_option" => true,
            "get_option" => true,
            "forward_add" => true,
            "forward_remove" => true,
            "stub_add" => true,
            "stub_remove" => true,
            "forward" => true,
            _ => false
        };
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
            throw new ArgumentException("Host cannot be null or empty", nameof(_options.Host));

        if (_options.Port <= 0 || _options.Port > 65535)
            throw new ArgumentException("Port must be between 1 and 65535", nameof(_options.Port));

        if (_options.ConnectTimeoutMs <= 0)
            throw new ArgumentException("Connect timeout must be positive", nameof(_options.ConnectTimeoutMs));

        if (_options.UseSSL)
        {
            if (string.IsNullOrEmpty(_options.ServerCertFile))
                throw new ArgumentException("ServerCertFile is required when UseSSL is true");

            if (string.IsNullOrEmpty(_options.ControlCertFile))
                throw new ArgumentException("ControlCertFile is required when UseSSL is true");

            if (string.IsNullOrEmpty(_options.ControlKeyFile))
                throw new ArgumentException("ControlKeyFile is required when UseSSL is true");
        }
    }
    public void Dispose()
    {
        _logger?.LogDebug("Disposing UnboundControlClient");
        _stream?.Dispose();
        _client?.Dispose();
    }
}