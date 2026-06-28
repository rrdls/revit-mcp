using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.UI;

namespace RevitMcpAddin;

public sealed class McpWebSocketClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ConcurrentQueue<RevitCommandRequest> _commandQueue;
    private readonly ExternalEvent _externalEvent;
    private readonly Uri _serverUri;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private ClientWebSocket? _socket;
    private Task? _runTask;

    public McpWebSocketClient(ConcurrentQueue<RevitCommandRequest> commandQueue, ExternalEvent externalEvent)
    {
        _commandQueue = commandQueue;
        _externalEvent = externalEvent;
        var url = Environment.GetEnvironmentVariable("REVIT_MCP_WS_URL") ?? "ws://127.0.0.1:8765";
        _serverUri = new Uri(url);
    }

    public void Start()
    {
        _runTask = Task.Run(RunAsync);
    }

    public async Task SendAsync(RevitCommandResponse response)
    {
        var socket = _socket;
        if (socket is null || socket.State != WebSocketState.Open)
        {
            return;
        }

        var json = JsonSerializer.Serialize(response, JsonOptions);
        var buffer = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(_cts.Token).ConfigureAwait(false);
        try
        {
            await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // The receive loop handles reconnects. Dropping a response is preferable to blocking Revit.
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _socket?.Dispose();
        _sendLock.Dispose();
        _cts.Dispose();
    }

    private async Task RunAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                _socket = socket;
                McpLog.Info($"Connecting to MCP server at {_serverUri}.");
                await socket.ConnectAsync(_serverUri, _cts.Token).ConfigureAwait(false);
                McpLog.Info("Connected to MCP server.");
                await SendHelloAsync(socket).ConfigureAwait(false);
                await ReceiveLoopAsync(socket).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                McpLog.Error("MCP server connection failed. Retrying.", ex);
                await Task.Delay(TimeSpan.FromSeconds(2), _cts.Token).ConfigureAwait(false);
            }
            finally
            {
                _socket = null;
            }
        }
    }

    private async Task SendHelloAsync(ClientWebSocket socket)
    {
        var token = Environment.GetEnvironmentVariable("REVIT_MCP_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            token = RevitMcpRuntime.LoadSettings().McpAuthToken;
        }

        var hello = JsonSerializer.Serialize(new { type = "hello", token }, JsonOptions);
        var buffer = Encoding.UTF8.GetBytes(hello);
        await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cts.Token).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket)
    {
        var buffer = new byte[64 * 1024];

        while (!_cts.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", _cts.Token).ConfigureAwait(false);
                    return;
                }

                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(message.ToArray());
            var request = JsonSerializer.Deserialize<RevitCommandRequest>(json, JsonOptions);
            if (request is null || string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Code))
            {
                McpLog.Error($"Ignored invalid MCP request payload: {json}");
                continue;
            }

            McpLog.Info($"Queued MCP command {request.Id}.");
            _commandQueue.Enqueue(request);
            _externalEvent.Raise();
        }
    }
}
