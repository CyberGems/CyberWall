using System.IO;
using System.Text.Json;
using CyberWall.Common.Models;

namespace CyberWall.Common.Notifications;

public sealed class NotificationStore
{
    private readonly string _path;
    private readonly List<AppNotification> _items = new();
    private readonly object _lock = new();
    private const int MaxItems = 150;

    public event Action? Changed;

    public NotificationStore(string? path = null)
    {
        _path = path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "CyberWall", "notifications.json");
        Load();
    }

    public IReadOnlyList<AppNotification> All
    {
        get { lock (_lock) return _items.ToList(); }
    }

    public int UnreadCount
    {
        get { lock (_lock) return _items.Count(n => !n.Read); }
    }

    public AppNotification Add(AppNotificationKind kind, string? appPath = null, string? appName = null, string? detail = null)
    {
        AppNotification item;
        lock (_lock)
        {
            var existing = _items.FirstOrDefault(n =>
                !n.Read && n.Kind == kind &&
                string.Equals(n.AppPath ?? "", appPath ?? "", StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Timestamp = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(appName)) existing.AppName = appName;
                if (!string.IsNullOrEmpty(detail)) existing.Detail = detail;
                item = existing;
            }
            else
            {
                item = new AppNotification
                {
                    Kind = kind,
                    AppPath = appPath,
                    AppName = appName,
                    Detail = detail
                };
                _items.Insert(0, item);
                while (_items.Count > MaxItems)
                    _items.RemoveAt(_items.Count - 1);
            }
            Save();
        }
        Changed?.Invoke();
        return item;
    }

    public void MarkRead(Guid id)
    {
        lock (_lock)
        {
            var n = _items.FirstOrDefault(i => i.Id == id);
            if (n == null || n.Read) return;
            n.Read = true;
            Save();
        }
        Changed?.Invoke();
    }

    public void MarkAllRead()
    {
        lock (_lock)
        {
            var any = false;
            foreach (var n in _items)
            {
                if (n.Read) continue;
                n.Read = true;
                any = true;
            }
            if (!any) return;
            Save();
        }
        Changed?.Invoke();
    }

    public void MarkRelatedRead(AppNotificationKind kind, string? appPath)
    {
        lock (_lock)
        {
            var any = false;
            foreach (var n in _items)
            {
                if (n.Read || n.Kind != kind) continue;
                if (!string.Equals(n.AppPath ?? "", appPath ?? "", StringComparison.OrdinalIgnoreCase)) continue;
                n.Read = true;
                any = true;
            }
            if (!any) return;
            Save();
        }
        Changed?.Invoke();
    }

    public void Remove(Guid id)
    {
        lock (_lock)
        {
            var n = _items.FirstOrDefault(i => i.Id == id);
            if (n == null) return;
            _items.Remove(n);
            Save();
        }
        Changed?.Invoke();
    }

    public void Clear()
    {
        lock (_lock)
        {
            if (_items.Count == 0) return;
            _items.Clear();
            Save();
        }
        Changed?.Invoke();
    }

    public void PurgeObsoleteUpdateNotifications(Version currentVersion)
    {
        lock (_lock)
        {
            var removed = _items.RemoveAll(n =>
            {
                if (n.Kind != AppNotificationKind.UpdateAvailable) return false;
                var verStr = (n.Detail ?? n.AppName ?? "").Trim().TrimStart('v', 'V');
                if (Version.TryParse(verStr, out var v))
                {
                    return currentVersion >= v;
                }
                return false;
            });

            if (removed <= 0) return;
            Save();
        }
        Changed?.Invoke();
    }

    private void Load()
    {
        try
        {
            if (!System.IO.File.Exists(_path)) return;
            var json = System.IO.File.ReadAllText(_path);
            var list = JsonSerializer.Deserialize<List<AppNotification>>(json);
            if (list == null) return;
            lock (_lock)
            {
                _items.Clear();
                _items.AddRange(list.OrderByDescending(n => n.Timestamp).Take(MaxItems));
            }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
            List<AppNotification> snap;
            lock (_lock) snap = _items.ToList();
            System.IO.File.WriteAllText(_path, JsonSerializer.Serialize(snap, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
