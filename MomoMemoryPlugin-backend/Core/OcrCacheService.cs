using System.Collections.Concurrent;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;

namespace MomoBackend.Core;

/// <summary>
/// OCR 缓存服务 - 提供 OCR 结果和图像的缓存机制
/// 减少重复 OCR 操作，提高高频场景性能
/// </summary>
public class OcrCacheService : IDisposable
{
    private static OcrCacheService? _instance;
    private static readonly object _instanceLock = new();

    // 缓存项结构
    private class CacheItem
    {
        public OcrResult Result { get; }
        public DateTime CreatedTime { get; }
        public DateTime LastAccessedTime { get; set; }
        public int AccessCount { get; set; }

        public CacheItem(OcrResult result)
        {
            Result = result;
            CreatedTime = DateTime.Now;
            LastAccessedTime = DateTime.Now;
            AccessCount = 0;
        }
    }

    // 内存缓存，使用 ConcurrentDictionary 确保线程安全
    private readonly ConcurrentDictionary<string, CacheItem> _cache = new();
    private readonly object _cleanupLock = new();

    // 缓存配置
    private const int MaxCacheSize = 1000; // 最大缓存项数量
    private const int DefaultCacheExpiryMs = 3000; // 默认缓存过期时间（毫秒）
    private const int CleanupIntervalMs = 5000; // 缓存清理间隔（毫秒）

    // 缓存统计
    private int _cacheHits = 0;
    private int _cacheMisses = 0;
    private int _cacheEvictions = 0;

    // 后台清理任务
    private System.Threading.Timer? _cleanupTimer;

    /// <summary>
    /// 获取单例实例
    /// </summary>
    public static OcrCacheService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new OcrCacheService();
                }
            }
            return _instance;
        }
    }

    private OcrCacheService()
    {
        // 启动后台清理任务
        _cleanupTimer = new System.Threading.Timer(
            (state) => CleanupExpiredItems(),
            null,
            CleanupIntervalMs,
            CleanupIntervalMs
        );

        Console.WriteLine("[OcrCache] 缓存服务初始化完成");
    }

    /// <summary>
    /// 生成缓存键
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="x">区域 X 坐标</param>
    /// <param name="y">区域 Y 坐标</param>
    /// <param name="width">区域宽度</param>
    /// <param name="height">区域高度</param>
    /// <param name="language">语言设置</param>
    /// <returns>缓存键</returns>
    public string GenerateCacheKey(long hwnd, int x, int y, int width, int height, string language = "auto")
    {
        // 使用窗口句柄、区域坐标、语言和时间戳（按秒）生成缓存键
        // 时间戳按秒取整，确保相同内容在短时间内的请求能命中缓存
        string baseKey = $"{hwnd}:{x}:{y}:{width}:{height}:{language}:{DateTime.Now.ToString("yyyyMMddHHmmss")}";
        
        // 使用 MD5 生成固定长度的键，避免键过长
        using var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(baseKey));
        return Convert.ToBase64String(hash).Replace("/", "_").Replace("+", "-").TrimEnd('=');
    }

    /// <summary>
    /// 获取缓存的 OCR 结果
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <returns>缓存的 OCR 结果，如果不存在则返回 null</returns>
    public OcrResult? Get(string key)
    {
        if (_cache.TryGetValue(key, out var item))
        {
            // 更新访问时间和访问计数
            item.LastAccessedTime = DateTime.Now;
            item.AccessCount++;
            
            _cacheHits++;
            Console.WriteLine($"[OcrCache] 缓存命中: {key} (访问次数: {item.AccessCount})");
            return item.Result;
        }

        _cacheMisses++;
        Console.WriteLine($"[OcrCache] 缓存未命中: {key}");
        return null;
    }

    /// <summary>
    /// 设置缓存的 OCR 结果
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="result">OCR 结果</param>
    public void Set(string key, OcrResult result)
    {
        if (!result.Success) return; // 只缓存成功的结果

        // 检查缓存大小，超过限制时清理
        if (_cache.Count >= MaxCacheSize)
        {
            CleanupOldestItems(MaxCacheSize / 2);
        }

        _cache[key] = new CacheItem(result);
        Console.WriteLine($"[OcrCache] 缓存设置: {key}");
    }

    /// <summary>
    /// 移除指定缓存项
    /// </summary>
    /// <param name="key">缓存键</param>
    public void Remove(string key)
    {
        if (_cache.TryRemove(key, out _))
        {
            _cacheEvictions++;
            Console.WriteLine($"[OcrCache] 缓存移除: {key}");
        }
    }

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    public void Clear()
    {
        int count = _cache.Count;
        _cache.Clear();
        _cacheEvictions += count;
        Console.WriteLine($"[OcrCache] 缓存清空: {count} 项");
    }

    /// <summary>
    /// 清理过期的缓存项
    /// </summary>
    private void CleanupExpiredItems()
    {
        lock (_cleanupLock)
        {
            var now = DateTime.Now;
            var expiredKeys = _cache.Where(kv => 
                (now - kv.Value.LastAccessedTime).TotalMilliseconds > DefaultCacheExpiryMs
            ).Select(kv => kv.Key).ToList();

            foreach (var key in expiredKeys)
            {
                if (_cache.TryRemove(key, out _))
                {
                    _cacheEvictions++;
                }
            }

            if (expiredKeys.Count > 0)
            {
                Console.WriteLine($"[OcrCache] 清理过期项: {expiredKeys.Count} 项");
            }
        }
    }

    /// <summary>
    /// 清理最旧的缓存项
    /// </summary>
    /// <param name="count">要清理的数量</param>
    private void CleanupOldestItems(int count)
    {
        lock (_cleanupLock)
        {
            var oldestKeys = _cache
                .OrderBy(kv => kv.Value.LastAccessedTime)
                .Take(count)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in oldestKeys)
            {
                if (_cache.TryRemove(key, out _))
                {
                    _cacheEvictions++;
                }
            }

            Console.WriteLine($"[OcrCache] 清理最旧项: {oldestKeys.Count} 项");
        }
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    /// <returns>缓存统计信息</returns>
    public string GetStats()
    {
        double hitRate = (_cacheHits + _cacheMisses) > 0 
            ? (double)_cacheHits / (_cacheHits + _cacheMisses) * 100 
            : 0;

        return $"OcrCache: {_cache.Count}/{MaxCacheSize} 项 | 命中率: {hitRate:F2}% | 命中: {_cacheHits} | 未命中: {_cacheMisses} | 淘汰: {_cacheEvictions}";
    }

    /// <summary>
    /// 获取当前缓存大小
    /// </summary>
    public int CacheSize => _cache.Count;

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _cache.Clear();
        Console.WriteLine("[OcrCache] 缓存服务已释放");
    }
}
