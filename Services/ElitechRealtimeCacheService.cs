using System.Collections.Concurrent;

namespace Elitech.Services;

public class ElitechRealtimeCacheService
{
    private readonly ConcurrentDictionary<string, CacheItem> _map = new(StringComparer.OrdinalIgnoreCase);

    public record CacheItem(DateTime Utc, object Row);

    public void Upsert(string deviceGuid, object row)
    {
        if (string.IsNullOrWhiteSpace(deviceGuid)) return;
        _map[deviceGuid.Trim()] = new CacheItem(DateTime.UtcNow, row);
    }

    public IReadOnlyList<object> GetMany(IEnumerable<string> deviceGuids, TimeSpan? maxAge = null)
    {
        var now = DateTime.UtcNow;
        var res = new List<object>();

        foreach (var g in deviceGuids)
        {
            if (string.IsNullOrWhiteSpace(g)) continue;

            if (_map.TryGetValue(g.Trim(), out var item))
            {
                if (maxAge == null || (now - item.Utc) <= maxAge.Value)
                    res.Add(item.Row);
            }
        }

        return res;
    }

    public int Count => _map.Count;
}