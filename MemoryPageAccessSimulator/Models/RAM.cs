namespace MemoryPageAccessSimulator.Models;

using System.Collections;

public class RAM
{
    private readonly AppSettings _appSettings;
    private Dictionary<Guid, object> _cache;
    private int _maxCachePages;
    public BTreeNodePage? BTreeRootPage { get; set; }
    public Stack<(Guid PageID, uint Offset)> FreeSlots;

    public RAM(AppSettings appSettings)
    {
        _appSettings = appSettings;
        _maxCachePages = appSettings.RAMSizeInNumberOfPages - 2;
        _cache = new Dictionary<Guid, object>();
    }
    
    public void AddPageToCache(Guid pageID, object page)
    {
        if (_cache.Count >= _maxCachePages)
        {
            var lruPageID = _cache.First().Key;
            _cache.Remove(lruPageID);
        }

        _cache.Add(pageID, page);
    }
}
