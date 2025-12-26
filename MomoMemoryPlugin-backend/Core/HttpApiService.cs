using System.Net;
using System.Text;
using System.Text.Json;
using MomoBackend.Models;

namespace MomoBackend.Core;

/// <summary>
/// HTTP API 服务 - 供 VS Code/Cursor 插件调用
/// </summary>
public class HttpApiService : IDisposable
{
    private readonly HttpListener _listener;
    private readonly WindowManager _windowManager;
    private readonly MouseController _mouseController;
    private readonly ScreenshotService _screenshotService;
    private readonly ConfigService _configService;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _isRunning;

    public int Port { get; }
    public bool IsRunning => _isRunning;

    public event Action<string>? OnLog;

    public HttpApiService(int port = 5678)
    {
        Port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _windowManager = new WindowManager();
        _mouseController = new MouseController();
        _screenshotService = new ScreenshotService();
        _configService = new ConfigService();

        // 连接 MouseController 的日志到本服务的日志
        _mouseController.OnLog += (msg) => Log(msg);

        // 将配置同步到 MouseController
        SyncMouseControllerSettings();
    }

    /// <summary>
    /// 将配置同步到 MouseController
    /// </summary>
    private void SyncMouseControllerSettings()
    {
        var settings = _configService.Config.FastBackground;
        Log($"[配置同步] HideCursor={settings.HideCursor}, Alpha={settings.WindowAlpha}, Minimize={settings.MinimizeAfterClick}");
        _mouseController.SetFastBackgroundSettings(settings);
    }

    public void Start()
    {
        if (_isRunning) return;

        try
        {
            _listener.Start();
            _isRunning = true;
            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => ListenAsync(_cts.Token));
            Log($"HTTP API 服务已启动: http://localhost:{Port}/");
        }
        catch (Exception ex)
        {
            Log($"HTTP API 服务启动失败: {ex.Message}");
            throw;
        }
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _cts?.Cancel();
        _listener.Stop();
        _isRunning = false;
        Log("HTTP API 服务已停止");
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _isRunning)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context), ct);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"请求处理错误: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // CORS headers
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = 200;
            response.Close();
            return;
        }

        try
        {
            var path = request.Url?.AbsolutePath ?? "/";
            object? result = path switch
            {
                "/api/windows" => HandleGetWindows(),
                "/api/window/rect" => HandleGetWindowRect(request),
                "/api/click" => await HandleClick(request),
                "/api/ocr" => await HandleOcr(request),
                "/api/config" => HandleConfig(request),
                "/api/status" => HandleStatus(),
                "/api/cursor" => HandleGetCursor(),
                _ => new { error = "Unknown endpoint" }
            };

            await SendJsonResponse(response, result);
        }
        catch (Exception ex)
        {
            Log($"API 错误: {ex.Message}");
            response.StatusCode = 500;
            await SendJsonResponse(response, new { error = ex.Message });
        }
    }

    private object HandleGetWindows()
    {
        var windows = _windowManager.GetAllWindows();
        return new
        {
            success = true,
            windows = windows.Select(w => new
            {
                hwnd = w.Hwnd,
                title = w.Title,
                processName = w.ProcessName,
                processId = w.ProcessId,
                rect = w.Rect != null ? new
                {
                    x = w.Rect.X,
                    y = w.Rect.Y,
                    width = w.Rect.Width,
                    height = w.Rect.Height
                } : null
            })
        };
    }

    private object HandleGetWindowRect(HttpListenerRequest request)
    {
        var hwndStr = request.QueryString["hwnd"];
        if (!long.TryParse(hwndStr, out var hwnd))
        {
            return new { success = false, error = "Invalid hwnd" };
        }

        var isValid = _windowManager.IsWindowValid((IntPtr)hwnd);
        if (!isValid)
        {
            return new { success = false, error = "Window not found" };
        }

        var clientOrigin = _windowManager.GetClientAreaOrigin((IntPtr)hwnd);
        NativeMethods.GetWindowRect((IntPtr)hwnd, out var rect);

        return new
        {
            success = true,
            valid = true,
            rect = new
            {
                x = rect.Left,
                y = rect.Top,
                width = rect.Right - rect.Left,
                height = rect.Bottom - rect.Top
            },
            clientOrigin = clientOrigin != null ? new
            {
                x = clientOrigin.Value.X,
                y = clientOrigin.Value.Y
            } : null
        };
    }

    private async Task<object> HandleClick(HttpListenerRequest request)
    {
        var body = await ReadRequestBody(request);
        var clickRequest = JsonSerializer.Deserialize<ClickApiRequest>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (clickRequest == null)
        {
            return new { success = false, error = "Invalid request body" };
        }

        // 重新加载配置并同步设置（确保使用最新配置）
        _configService.Load();
        SyncMouseControllerSettings();

        // 获取客户区原点
        var clientOrigin = _windowManager.GetClientAreaOrigin((IntPtr)clickRequest.Hwnd);
        if (clientOrigin == null)
        {
            return new { success = false, error = "Cannot get window client area" };
        }

        var result = _mouseController.Click(
            (IntPtr)clickRequest.Hwnd,
            clickRequest.X,
            clickRequest.Y,
            clientOrigin.Value.X,
            clientOrigin.Value.Y,
            clickRequest.Mode ?? "fast_background",
            clickRequest.Button ?? "left"
        );

        Log($"点击: ({clickRequest.X}, {clickRequest.Y}) 模式={clickRequest.Mode} 结果={result.Success}");

        return new
        {
            success = result.Success,
            message = result.Message
        };
    }

    private async Task<object> HandleOcr(HttpListenerRequest request)
    {
        var body = await ReadRequestBody(request);
        var ocrRequest = JsonSerializer.Deserialize<OcrApiRequest>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (ocrRequest == null)
        {
            return new { success = false, error = "Invalid request body" };
        }

        // 截取窗口区域
        var rect = new System.Drawing.Rectangle(
            ocrRequest.X,
            ocrRequest.Y,
            ocrRequest.Width,
            ocrRequest.Height
        );

        var bitmap = _screenshotService.CaptureRegion((IntPtr)ocrRequest.Hwnd, rect);
        if (bitmap == null)
        {
            return new { success = false, error = "Screenshot failed" };
        }

        try
        {
            // 使用 PaddleOCR
            var result = await PaddleOcrService.Instance.RecognizeAsync(bitmap, ocrRequest.Language ?? "auto");

            Log($"OCR: 区域({ocrRequest.X},{ocrRequest.Y},{ocrRequest.Width}x{ocrRequest.Height}) 结果={result.Text}");

            return new
            {
                success = result.Success,
                text = result.Text,
                confidence = result.Confidence,
                errorMessage = result.ErrorMessage
            };
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    private object HandleConfig(HttpListenerRequest request)
    {
        if (request.HttpMethod == "GET")
        {
            return new
            {
                success = true,
                config = _configService.Config
            };
        }

        return new { success = false, error = "Use GET to retrieve config" };
    }

    private object HandleStatus()
    {
        var ocrStatus = PaddleOcrService.Instance.GetStatus();
        return new
        {
            success = true,
            status = "running",
            port = Port,
            ocrEngine = ocrStatus,
            ocrAvailable = PaddleOcrService.Instance.IsAvailable
        };
    }

    private object HandleGetCursor()
    {
        var pos = _mouseController.GetCursorPosition();
        return new
        {
            success = true,
            x = pos.X,
            y = pos.Y
        };
    }

    private static async Task<string> ReadRequestBody(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        return await reader.ReadToEndAsync();
    }

    private static async Task SendJsonResponse(HttpListenerResponse response, object? data)
    {
        response.ContentType = "application/json; charset=utf-8";
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var buffer = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    private void Log(string message)
    {
        OnLog?.Invoke(message);
    }

    public void Dispose()
    {
        Stop();
        _listener.Close();
    }
}

// API 请求模型
public class ClickApiRequest
{
    public long Hwnd { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string? Mode { get; set; }
    public string? Button { get; set; }
}

public class OcrApiRequest
{
    public long Hwnd { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Language { get; set; }
}
