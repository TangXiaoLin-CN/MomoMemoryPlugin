using System.Text.Json;
using MomoBackend.Models;

namespace MomoBackend.Core;

public class CoordinateManager
{
    private readonly string _configPath;
    private List<CoordinatePoint> _coordinates = new();

    public CoordinateManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appData, "MomoBackend");
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "coordinates.json");
        Load();
    }

    /// <summary>
    /// 获取所有坐标
    /// </summary>
    public List<CoordinatePoint> GetAll()
    {
        return _coordinates.ToList();
    }

    /// <summary>
    /// 根据别名获取坐标
    /// </summary>
    public CoordinatePoint? GetByAlias(string alias)
    {
        return _coordinates.FirstOrDefault(c => c.Alias == alias);
    }

    /// <summary>
    /// 添加或更新坐标
    /// </summary>
    public void Add(CoordinatePoint point)
    {
        var existing = _coordinates.FindIndex(c => c.Alias == point.Alias);
        if (existing >= 0)
        {
            _coordinates[existing] = point;
        }
        else
        {
            _coordinates.Add(point);
        }
        Save();
    }

    /// <summary>
    /// 删除坐标
    /// </summary>
    public bool Remove(string alias)
    {
        var removed = _coordinates.RemoveAll(c => c.Alias == alias) > 0;
        if (removed) Save();
        return removed;
    }

    /// <summary>
    /// 保存到文件
    /// </summary>
    private void Save()
    {
        var json = JsonSerializer.Serialize(_coordinates, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_configPath, json);
    }

    /// <summary>
    /// 从文件加载
    /// </summary>
    private void Load()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                _coordinates = JsonSerializer.Deserialize<List<CoordinatePoint>>(json) ?? new();
            }
            catch
            {
                _coordinates = new();
            }
        }
    }
}
