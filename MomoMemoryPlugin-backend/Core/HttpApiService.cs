using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private readonly OcrService _windowsOcrService;  // Windows OCR 服务（用于英文）
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
        _windowsOcrService = new OcrService();  // 初始化 Windows OCR

        // 连接 MouseController 的日志到本服务的日志
        _mouseController.OnLog += (msg) => Log(msg);

        // 将配置同步到 MouseController
        SyncMouseControllerSettings();

        // 记录 Windows OCR 状态
        Log($"[Windows OCR] {_windowsOcrService.GetStatus()}");
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
                "/api/ocr/batch" => await HandleOcrBatch(request),
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
            var language = ocrRequest.Language?.ToLower() ?? "auto";
            OcrResult result;
            string engineUsed;

            // 根据语言选择 OCR 引擎
            // 英文使用 Windows OCR（能正确处理单词空格）
            // 中文/自动使用 PaddleOCR（中文识别准确度更高）
            if (language == "en" || language == "english")
            {
                result = await _windowsOcrService.RecognizeAsync(bitmap, "en");
                engineUsed = "Windows";
            }
            else
            {
                result = await PaddleOcrService.Instance.RecognizeAsync(bitmap, language);
                engineUsed = "Paddle";
            }

            // 确保 Confidence 是有效的数值（双重保障）
            var safeConfidence = (double)result.Confidence;
            if (double.IsNaN(safeConfidence) || double.IsInfinity(safeConfidence))
            {
                safeConfidence = 0;
            }

            Log($"OCR[{engineUsed}]: 区域({ocrRequest.X},{ocrRequest.Y},{ocrRequest.Width}x{ocrRequest.Height}) lang={language} 结果={result.Text}");

            return new
            {
                success = result.Success,
                text = result.Text,
                confidence = safeConfidence,
                errorMessage = result.ErrorMessage
            };
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    /// <summary>
    /// 批量 OCR - 根据语言选择引擎：英文用 Windows OCR，中文用 PaddleOCR
    /// </summary>
    private async Task<object> HandleOcrBatch(HttpListenerRequest request)
    {
        var body = await ReadRequestBody(request);
        var batchRequest = JsonSerializer.Deserialize<OcrBatchApiRequest>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (batchRequest == null || batchRequest.Regions == null || batchRequest.Regions.Count == 0)
        {
            return new { success = false, error = "Invalid request body or empty regions" };
        }

        var hwnd = (IntPtr)batchRequest.Hwnd;
        var regions = batchRequest.Regions;

        Log($"[批量OCR] 开始处理 {regions.Count} 个区域");

        // 按语言分组区域
        var englishRegions = new List<(int index, OcrRegionRequest region, System.Drawing.Bitmap? bitmap)>();
        var chineseRegions = new List<(int index, OcrRegionRequest region, System.Drawing.Bitmap? bitmap)>();

        for (int i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            var rect = new System.Drawing.Rectangle(region.X, region.Y, region.Width, region.Height);
            var bitmap = _screenshotService.CaptureRegion(hwnd, rect);

            var language = region.Language?.ToLower() ?? "auto";
            if (language == "en" || language == "english")
            {
                englishRegions.Add((i, region, bitmap));
            }
            else
            {
                chineseRegions.Add((i, region, bitmap));
            }
        }

        Log($"[批量OCR] 英文区域: {englishRegions.Count}, 中文/自动区域: {chineseRegions.Count}");

        // 准备结果数组
        var results = new OcrResult[regions.Count];
        var regionInfos = new (string alias, int x, int y, int w, int h, string engine)[regions.Count];

        try
        {
            // 并行处理英文区域（使用 Windows OCR）
            var englishTasks = englishRegions
                .Where(r => r.bitmap != null)
                .Select(async r =>
                {
                    var result = await _windowsOcrService.RecognizeAsync(r.bitmap!, "en");
                    return (r.index, result);
                });

            // 处理中文区域（使用 PaddleOCR 批量处理）
            var chineseBitmaps = chineseRegions
                .Where(r => r.bitmap != null)
                .Select(r => r.bitmap!)
                .ToList();

            var chineseIndices = chineseRegions
                .Where(r => r.bitmap != null)
                .Select(r => r.index)
                .ToList();

            // 确保 OCR 池有足够的实例
            if (chineseBitmaps.Count > 0)
            {
                PaddleOcrPool.Instance.EnsurePoolSize(chineseBitmaps.Count);
            }

            // 并行执行
            var englishResultsTask = Task.WhenAll(englishTasks);
            var chineseResultsTask = chineseBitmaps.Count > 0
                ? PaddleOcrPool.Instance.RecognizeBatchAsync(chineseBitmaps)
                : Task.FromResult(new List<OcrResult>());

            await Task.WhenAll(englishResultsTask, chineseResultsTask);

            // 收集英文结果
            foreach (var (index, result) in await englishResultsTask)
            {
                results[index] = result;
                var region = regions[index];
                regionInfos[index] = (region.Alias ?? $"region_{index}", region.X, region.Y, region.Width, region.Height, "Windows");
            }

            // 收集中文结果
            var chineseResults = await chineseResultsTask;
            for (int i = 0; i < chineseResults.Count; i++)
            {
                var index = chineseIndices[i];
                results[index] = chineseResults[i];
                var region = regions[index];
                regionInfos[index] = (region.Alias ?? $"region_{index}", region.X, region.Y, region.Width, region.Height, "Paddle");
            }

            // 处理截图失败的区域
            for (int i = 0; i < regions.Count; i++)
            {
                if (results[i] == null)
                {
                    results[i] = new OcrResult { Success = false, ErrorMessage = "Screenshot failed" };
                    var region = regions[i];
                    regionInfos[i] = (region.Alias ?? $"region_{i}", region.X, region.Y, region.Width, region.Height, "N/A");
                }
            }

            // 构建返回结果
            var outputResults = new List<object>();
            for (int i = 0; i < results.Length; i++)
            {
                var ocrResult = results[i];
                var info = regionInfos[i];

                var safeConfidence = (double)ocrResult.Confidence;
                if (double.IsNaN(safeConfidence) || double.IsInfinity(safeConfidence))
                {
                    safeConfidence = 0;
                }

                Log($"[批量OCR][{info.engine}] {info.alias}: ({info.x},{info.y},{info.w}x{info.h}) => {(ocrResult.Success ? ocrResult.Text : "失败")}");

                outputResults.Add(new
                {
                    alias = info.alias,
                    success = ocrResult.Success,
                    text = ocrResult.Text,
                    confidence = safeConfidence,
                    errorMessage = ocrResult.ErrorMessage
                });
            }

            var allSuccess = results.All(r => r.Success);
            var successCount = results.Count(r => r.Success);

            Log($"[批量OCR] 完成: {successCount}/{results.Length} 成功");

            return new
            {
                success = true,
                allSuccess = allSuccess,
                successCount = successCount,
                totalCount = results.Length,
                results = outputResults
            };
        }
        finally
        {
            // 释放所有截图
            foreach (var (_, _, bitmap) in englishRegions)
            {
                bitmap?.Dispose();
            }
            foreach (var (_, _, bitmap) in chineseRegions)
            {
                bitmap?.Dispose();
            }
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
        var paddleOcrStatus = PaddleOcrService.Instance.GetStatus();
        var poolStatus = PaddleOcrPool.Instance.GetStatus();
        var windowsOcrStatus = _windowsOcrService.GetStatus();
        return new
        {
            success = true,
            status = "running",
            port = Port,
            ocrEngine = paddleOcrStatus,
            ocrPool = poolStatus,
            windowsOcr = windowsOcrStatus,
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
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
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

public class OcrBatchApiRequest
{
    public long Hwnd { get; set; }
    public List<OcrRegionRequest> Regions { get; set; } = new();
}

public class OcrRegionRequest
{
    public string? Alias { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Language { get; set; }
}
