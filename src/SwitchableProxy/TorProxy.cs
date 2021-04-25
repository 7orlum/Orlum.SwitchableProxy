using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MihaZupan;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static System.Environment;


namespace Orlum.SwitchableProxy
{
    public class TorProxy : ISwitchableProxy, IDisposable
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly HttpClientHandler _httpClientHandler;


        public bool Disabled { get; private set; }
        public string Address { get; private set; }
        public int Port { get; private set; }
        public long ExitNodesChanged { get; private set; }
        public long ProxiesUsed => Disabled ? 0 : (ExitNodesChanged + 1);
        public int ControlPort { get; private set; }
        public string ControlPassword { get; private set; }
        public TimeSpan CircuitBiuldTimeoutSeconds { get; private set; }


        public TorProxy(IConfiguration configuration, ILogger logger)
        {
            Disabled = !configuration.GetValue<bool>("Enable", true);
            Address = configuration.GetValue<string>("Address", "127.0.0.1");
            Port = configuration.GetValue<int>("Port", 9050);
            ControlPort = configuration.GetValue<int>("ControlPort", 9051);
            ControlPassword = configuration.GetValue<string>("ControlPassword", string.Empty);
            CircuitBiuldTimeoutSeconds = TimeSpan.FromSeconds(configuration.GetValue<double>("CircuitBiuldTimeoutSeconds", 60));
            _logger = logger;

            if (Disabled)
            {
                _httpClient = new HttpClient();
            }
            else
            {
                _httpClientHandler = new HttpClientHandler
                {
                    Proxy = new HttpToSocks5Proxy(Address, Port),
                    CheckCertificateRevocationList = true,
                };
                _httpClient = new HttpClient(_httpClientHandler);
            }
        }


        public async Task<string> GetCurrentExitNodeAsync(CancellationToken cancellationToken = default)
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, "http://checkip.amazonaws.com");
            using var responce = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            return (await responce.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Trim();
        }


        public async Task ChangeExitNodeAsync(CancellationToken cancellationToken = default)
        {
            if (Disabled)
                return;

            _logger.LogTrace("Changing the exit node");

            var exitNode = await GetCurrentExitNodeAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogTrace($"The old exit node is {exitNode}");

            SendNewNymCommand();
            CloseStreams();

            for (var i = 0; i < CircuitBiuldTimeoutSeconds.TotalSeconds; i++)
            {
                Thread.Sleep((int)TimeSpan.FromSeconds(1).TotalMilliseconds);

                var newExitNode = await GetCurrentExitNodeAsync(cancellationToken).ConfigureAwait(false);

                if (exitNode != newExitNode)
                {
                    _logger.LogTrace($"The new exit node is {newExitNode}");

                    Thread.Sleep((int)TimeSpan.FromSeconds(10).TotalMilliseconds);

                    ExitNodesChanged++;
                    return;
                }
            }

            throw new ProxyException("Failed to change the exit node");
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
                _httpClientHandler?.Dispose();
            }
        }


        private void SendNewNymCommand()
        {
            var endPoint = new IPEndPoint(IPAddress.Parse(Address), ControlPort);
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.Connect(endPoint);
            SendCommand(socket, $"AUTHENTICATE \"{ControlPassword}\"");
            SendCommand(socket, "SIGNAL NEWNYM");
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
        }


        private void CloseStreams()
        {
            var endPoint = new IPEndPoint(IPAddress.Parse(Address), ControlPort);
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.Connect(endPoint);
            SendCommand(socket, $"AUTHENTICATE \"{ControlPassword}\"");

            foreach (var stream in GetStreams(socket))
            {
                SendCommand(socket, $"CLOSESTREAM {stream.StreamID} 1");
            }

            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
        }


        private static Stream[] GetStreams(Socket socket)
        {
            var result = new List<Stream>();

            var rows = GetInfo(socket, "stream-status");
            foreach (var row in rows)
            {
                if (row.Length < 4)
                    throw new ProxyException($"Wrong TOR stream status string: {string.Join(" ", row)}");

                result.Add(new Stream(StreamID: row[0], StreamStatus: row[1], CircuitID: row[2], Target: row[3]));
            }

            return result.ToArray();
        }


        private static string[][] GetInfo(Socket socket, string command)
        {
            var result = new List<string[]>();

            socket.Send(Encoding.ASCII.GetBytes($"GETINFO {command}{NewLine}"));

            var buffer = new byte[socket.ReceiveBufferSize];
            var count = socket.Receive(buffer, buffer.Length, SocketFlags.None);
            var responce = Encoding.ASCII.GetString(buffer, 0, count);

            var regex = new Regex(@$"^(250-{command}=(()|(?<row>[^{NewLine}]+)){NewLine}250 OK{NewLine})$|^(250\+{command}={NewLine}((?<row>[^{NewLine}]+){NewLine})*\.{NewLine}250 OK{NewLine})$");
            var match = regex.Match(responce);

            if (!match.Success)
                throw new ProxyException($"The TOR control responded:\n{responce.Trim()}\non the command GETINFO {command}");

            foreach (Capture capture in match.Groups["row"].Captures)
                result.Add(capture.Value.Split(" "));

            return result.ToArray();
        }


        private static void SendCommand(Socket socket, string command)
        {
            socket.Send(Encoding.ASCII.GetBytes($"{command}{Environment.NewLine}"));

            var buffer = new byte[socket.ReceiveBufferSize];
            var count = socket.Receive(buffer, buffer.Length, SocketFlags.None);
            var responce = Encoding.ASCII.GetString(buffer, 0, count);

            if (!responce.Equals($"250 OK{Environment.NewLine}", StringComparison.Ordinal))
                throw new ProxyException($"The TOR control responded:\n{responce.Trim()}\non the command {command}");
        }


        private record Stream(string StreamID, string StreamStatus, string CircuitID, string Target);
    }
}
