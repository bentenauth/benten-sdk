using System.Net;
using System.Net.WebSockets;
using System.Text;
using Benten.SDK;
using Benten.SDK.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Benten.SDK.Tests;

/// <summary>
/// Tests for how the SDK sends a request and reads the reply over a WebSocket.
/// Each test starts a small local WebSocket server that behaves the way the
/// test needs: replying in pieces, closing early, never replying, and so on.
/// </summary>
public class WebSocketTransportTests
{
    private static LoginRequest Login() => new()
    {
        Country = "United States",
        PhoneNumber = "6501234567",
        Username = "alice@example.com"
    };

    // ── Replies that must be read completely ────────────────────────────────

    [Fact]
    public async Task ReplyInSeveralFrames_IsReassembled()
    {
        await using var server = await TestServer.StartAsync(async (ws, request) =>
        {
            await TestServer.SendFramesAsync(ws, Encoding.UTF8.GetBytes("""{ "_id": "abc", "Response": "Allow" }"""), 4);
        });

        var result = await server.Client().RequestLoginApprovalAsync("jwt", Login());

        Assert.True(result.IsApproved);
        Assert.Equal("abc", result.RequestId);
    }

    [Fact]
    public async Task ReplyLargerThan4KB_IsReadCompletely()
    {
        string longText = new string('x', 20_000);
        string reply = $$"""{ "_id": "abc", "Line1": "{{longText}}", "Response": "BSC4019" }""";

        await using var server = await TestServer.StartAsync(async (ws, request) =>
        {
            await TestServer.SendFramesAsync(ws, Encoding.UTF8.GetBytes(reply), 7);
        });

        var result = await server.Client().RequestLoginApprovalAsync("jwt", Login());

        Assert.True(result.HasError);
        Assert.Equal("BSC4019", result.ErrorCode);
        Assert.Equal(reply, result.RawResponse);
    }

    [Fact]
    public async Task MultiByteCharacterSplitAcrossFrames_IsDecodedCorrectly()
    {
        string reply = """{ "_id": "abc", "Line1": "Café 漢字 🙂", "Response": "Deny" }""";
        byte[] bytes = Encoding.UTF8.GetBytes(reply);
        int split = Array.IndexOf(bytes, (byte)0xE6) + 1;   // inside the first byte sequence of "漢"

        await using var server = await TestServer.StartAsync(async (ws, request) =>
        {
            await ws.SendAsync(bytes.AsMemory(0, split), WebSocketMessageType.Text, false, CancellationToken.None);
            await ws.SendAsync(bytes.AsMemory(split), WebSocketMessageType.Text, true, CancellationToken.None);
        });

        var result = await server.Client().RequestLoginApprovalAsync("jwt", Login());

        Assert.True(result.IsDenied);
        Assert.Equal(reply, result.RawResponse);
    }

    // ── Request and close ───────────────────────────────────────────────────

    [Fact]
    public async Task Request_IsSentAsJson_AndConnectionIsClosedAfterReply()
    {
        string? receivedRequest = null;
        WebSocketMessageType? afterReply = null;

        await using var server = await TestServer.StartAsync(async (ws, request) =>
        {
            receivedRequest = request;
            await ws.SendAsync(Encoding.UTF8.GetBytes("""{ "_id": "abc", "Response": "Allow" }"""),
                               WebSocketMessageType.Text, true, CancellationToken.None);

            // The SDK should now send a close message.
            var buffer = new byte[256];
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            afterReply = (await ws.ReceiveAsync(buffer, cts.Token)).MessageType;
        });

        var result = await server.Client().RequestLoginApprovalAsync("my-jwt", Login());
        await server.WaitForHandlerAsync();

        Assert.True(result.IsApproved);
        Assert.NotNull(receivedRequest);
        Assert.Contains("\"RequestType\":\"LoginRequest\"", receivedRequest!);
        Assert.Contains("\"JWT\":\"my-jwt\"", receivedRequest!);
        Assert.Contains("\"_id\":", receivedRequest!);
        Assert.Equal(WebSocketMessageType.Close, afterReply);
    }

    // ── Server closes or stays silent ───────────────────────────────────────

    [Fact]
    public async Task ServerClosesWithoutReplying_ThrowsWithReasonAndCode()
    {
        await using var server = await TestServer.StartAsync(async (ws, request) =>
        {
            await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "BSC4019 Invalid Token", CancellationToken.None);
        });

        var ex = await Assert.ThrowsAsync<BentenException>(
            () => server.Client().RequestLoginApprovalAsync("jwt", Login()));

        Assert.Equal("BSC4019", ex.ErrorCode);
        Assert.Contains("closed the connection", ex.Message);
        Assert.Contains("BSC4019 Invalid Token", ex.Message);
    }

    [Fact]
    public async Task ServerClosesWithoutReason_Throws()
    {
        await using var server = await TestServer.StartAsync(async (ws, request) =>
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        });

        var ex = await Assert.ThrowsAsync<BentenException>(
            () => server.Client().RequestLoginApprovalAsync("jwt", Login()));

        Assert.Equal("", ex.ErrorCode);
        Assert.Contains("closed the connection", ex.Message);
    }

    [Fact]
    public async Task NoReply_TimesOutWithBentenException()
    {
        await using var server = await TestServer.StartAsync((ws, request) => TestServer.WaitForClientToLeaveAsync(ws));

        var client = server.Client();
        client.ResponseTimeout = TimeSpan.FromMilliseconds(500);

        var started = DateTime.UtcNow;
        var ex = await Assert.ThrowsAsync<BentenException>(() => client.RequestLoginApprovalAsync("jwt", Login()));

        Assert.Contains("No reply", ex.Message);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CallerCancellation_ThrowsOperationCanceled()
    {
        await using var server = await TestServer.StartAsync((ws, request) => TestServer.WaitForClientToLeaveAsync(ws));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => server.Client().RequestLoginApprovalAsync("jwt", Login(), cts.Token));
    }

    [Fact]
    public async Task OversizedReply_Throws()
    {
        await using var server = await TestServer.StartAsync(async (ws, request) =>
        {
            await TestServer.SendFramesAsync(ws, Encoding.UTF8.GetBytes(new string('x', 5_000)), 3);
        });

        var client = server.Client();
        client.MaxResponseBytes = 1024;

        var ex = await Assert.ThrowsAsync<BentenException>(() => client.RequestLoginApprovalAsync("jwt", Login()));
        Assert.Contains("larger than", ex.Message);
    }

    [Fact]
    public async Task UnreachableServer_ThrowsBentenException()
    {
        int freePort = TestServer.GetUnusedPort();
        var client = new BentenClient($"ws://127.0.0.1:{freePort}/v2/");

        var ex = await Assert.ThrowsAsync<BentenException>(() => client.RequestLoginApprovalAsync("jwt", Login()));
        Assert.Contains("connect", ex.Message);
    }
}

/// <summary>
/// A local WebSocket server for the transport tests. It accepts one kind of
/// request on /v2/clients/loginrequest/phonenumber, reads the request text and
/// hands the socket to the test's handler.
/// </summary>
internal sealed class TestServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly TaskCompletionSource _handlerDone = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string BaseUrl { get; }

    private TestServer(WebApplication app, string baseUrl)
    {
        _app = app;
        BaseUrl = baseUrl;
    }

    public BentenClient Client() => new(BaseUrl);

    public Task WaitForHandlerAsync() => _handlerDone.Task.WaitAsync(TimeSpan.FromSeconds(10));

    public static async Task<TestServer> StartAsync(Func<WebSocket, string, Task> handler)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel(o => o.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();
        TestServer? server = null;

        app.UseWebSockets();
        app.Map("/v2/clients/loginrequest/phonenumber", async (HttpContext ctx) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                return;
            }

            using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            try
            {
                string request = await ReadTextAsync(ws);
                await handler(ws, request);
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            finally
            {
                server!._handlerDone.TrySetResult();
            }
        });

        await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>()
                            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
        server = new TestServer(app, address.Replace("http://", "ws://") + "/v2/");
        return server;
    }

    public static async Task SendFramesAsync(WebSocket ws, byte[] message, int frames)
    {
        int size = Math.Max(1, (int)Math.Ceiling(message.Length / (double)frames));
        for (int offset = 0; offset < message.Length; offset += size)
        {
            int count = Math.Min(size, message.Length - offset);
            bool last = offset + count >= message.Length;
            await ws.SendAsync(message.AsMemory(offset, count), WebSocketMessageType.Text, last, CancellationToken.None);
        }
    }

    /// <summary>Never replies; returns once the client closes or drops the connection.</summary>
    public static async Task WaitForClientToLeaveAsync(WebSocket ws)
    {
        var buffer = new byte[256];
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
                return;
        }
    }

    public static int GetUnusedPort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<string> ReadTextAsync(WebSocket ws)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await _app.StopAsync(cts.Token);
        await _app.DisposeAsync();
    }
}
