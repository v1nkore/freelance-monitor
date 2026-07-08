using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FreelanceMonitor.State;

/// <summary>
/// Хранит id уже показанных заказов, чтобы не слать дубли.
/// Персистится в JSON рядом с exe — переживает перезапуск службы.
/// Потокобезопасность не нужна: Worker дёргает его из одного потока.
/// </summary>
public sealed class SeenStore
{
    private readonly string _path;
    private readonly ILogger<SeenStore> _logger;
    private HashSet<string> _seen = [];

    public SeenStore(ILogger<SeenStore> logger)
    {
        _logger = logger;
        var dir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "seen.json");
        Load();
    }

    public bool IsNew(string id) => !_seen.Contains(id);

    public void Add(string id) => _seen.Add(id);

    public int Count => _seen.Count;

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                _seen = JsonSerializer.Deserialize<HashSet<string>>(json) ?? [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать {Path}, начинаю с пустого состояния", _path);
        }
    }

    public void Save()
    {
        try
        {
            // Ограничиваем рост файла: держим последние 5000 id.
            var trimmed = _seen.TakeLast(5000).ToHashSet();
            File.WriteAllText(_path, JsonSerializer.Serialize(trimmed));
            _seen = trimmed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось сохранить {Path}", _path);
        }
    }
}
