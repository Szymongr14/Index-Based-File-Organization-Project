namespace MemoryPageAccessSimulator.Models;

public class RAM
{
    private readonly AppSettings _appSettings;
    private Dictionary<Guid, BTreeNodePage> _cache = new();
    private int _maxCachePages;
    public BTreeNodePage? BTreeRootPage { get; set; }
    private LinkedList<BTreeNodePage> _lruList = [];
    public Stack<(Guid PageID, uint Offset)> FreeSlots { get; set; } = new();

    public RAM(AppSettings appSettings)
    {
        _appSettings = appSettings;
        _maxCachePages = appSettings.RAMSizeInNumberOfPages - 1;
        BTreeRootPage = null;
    }
    
    public void AddPageToCache(BTreeNodePage bTreeNodePage)
    {
        if (_cache.Count >= _maxCachePages)
        {
            var lruNode = _lruList.Last();
            _lruList.RemoveLast();
            _cache.Remove(lruNode.PageID);
        }

        _cache.Add(bTreeNodePage.PageID, bTreeNodePage);
        _lruList.AddFirst(bTreeNodePage);
    }

    public bool CheckCacheForSpecificPage(Guid pageID)
    {
        return _cache.ContainsKey(pageID);
    }
    
    public BTreeNodePage GetPageFromCache(Guid pageID)
    {
        return _cache[pageID];
    }
}
