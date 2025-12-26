using System.Text.Json;
using System.Text.Json.Serialization;
using MomoBackend.Models;

namespace MomoBackend.Core;

/// <summary>
/// 配置服务 - 负责加载和保存应用程序配置
/// </summary>
public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _configPath;
    private AppConfig _config;

    /// <summary>
    /// 当前配置
    /// </summary>
    public AppConfig Config => _config;

    /// <summary>
    /// 配置文件路径
    /// </summary>
    public string ConfigPath => _configPath;

    public ConfigService(string? configPath = null)
    {
        // 默认配置文件路径：程序所在目录/momo-config.json
        _configPath = configPath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "momo-config.json"
        );

        _config = Load();
    }

    /// <summary>
    /// 加载配置
    /// </summary>
    public AppConfig Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (config != null)
                {
                    _config = config;
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}");
        }

        // 返回默认配置
        _config = new AppConfig();
        return _config;
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    public bool Save(AppConfig? config = null)
    {
        try
        {
            if (config != null)
            {
                _config = config;
            }

            var json = JsonSerializer.Serialize(_config, JsonOptions);
            File.WriteAllText(_configPath, json);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 添加点击区域
    /// </summary>
    public void AddClickPoint(ClickPoint point)
    {
        _config.ClickPoints.Add(point);
    }

    /// <summary>
    /// 移除点击区域
    /// </summary>
    public bool RemoveClickPoint(string alias)
    {
        var point = _config.ClickPoints.FirstOrDefault(p => p.Alias == alias);
        if (point != null)
        {
            _config.ClickPoints.Remove(point);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 更新点击区域
    /// </summary>
    public bool UpdateClickPoint(string alias, ClickPoint newPoint)
    {
        var index = _config.ClickPoints.FindIndex(p => p.Alias == alias);
        if (index >= 0)
        {
            _config.ClickPoints[index] = newPoint;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 获取点击区域
    /// </summary>
    public ClickPoint? GetClickPoint(string alias)
    {
        return _config.ClickPoints.FirstOrDefault(p => p.Alias == alias);
    }

    /// <summary>
    /// 添加 OCR 区域
    /// </summary>
    public void AddOcrRegion(OcrRegion region)
    {
        _config.OcrRegions.Add(region);
    }

    /// <summary>
    /// 移除 OCR 区域
    /// </summary>
    public bool RemoveOcrRegion(string alias)
    {
        var region = _config.OcrRegions.FirstOrDefault(r => r.Alias == alias);
        if (region != null)
        {
            _config.OcrRegions.Remove(region);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 更新 OCR 区域
    /// </summary>
    public bool UpdateOcrRegion(string alias, OcrRegion newRegion)
    {
        var index = _config.OcrRegions.FindIndex(r => r.Alias == alias);
        if (index >= 0)
        {
            _config.OcrRegions[index] = newRegion;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 获取 OCR 区域
    /// </summary>
    public OcrRegion? GetOcrRegion(string alias)
    {
        return _config.OcrRegions.FirstOrDefault(r => r.Alias == alias);
    }

    /// <summary>
    /// 设置目标窗口
    /// </summary>
    public void SetTargetWindow(string title, string processName)
    {
        _config.TargetWindowTitle = title;
        _config.TargetProcessName = processName;
    }

    /// <summary>
    /// 导出配置为 JSON 字符串
    /// </summary>
    public string ExportToJson()
    {
        return JsonSerializer.Serialize(_config, JsonOptions);
    }

    /// <summary>
    /// 从 JSON 字符串导入配置
    /// </summary>
    public bool ImportFromJson(string json)
    {
        try
        {
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (config != null)
            {
                _config = config;
                return true;
            }
        }
        catch { }
        return false;
    }
}
