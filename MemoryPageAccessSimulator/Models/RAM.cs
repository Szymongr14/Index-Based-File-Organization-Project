namespace MemoryPageAccessSimulator.Models;

public class RAM
{
    private readonly AppSettings _appSettings;
    private Dictionary<int, object> cache;
    private int maxPages;

    public RAM(AppSettings appSettings)
    {
        cache = new Dictionary<int, object>();
        _appSettings = appSettings;
        maxPages = appSettings.RAMSizeInNumberOfPages;
    }

    public object GetPage(int pageID)
    {
        cache.TryGetValue(pageID, out var page);
        return page;
    }

    public void AddPage(int pageID, object page)
    {
        if (cache.Count >= maxPages)
        {
            RemoveOldestPage();
        }
        cache[pageID] = page;
    }

    private void RemoveOldestPage()
    {
        var oldestKey = cache.Keys.First();
        cache.Remove(oldestKey);
    }
}
