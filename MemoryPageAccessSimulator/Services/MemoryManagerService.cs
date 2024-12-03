using MemoryPageAccessSimulator.Interfaces;
using MemoryPageAccessSimulator.Models;
using MemoryPageAccessSimulator.Utilities;

namespace MemoryPageAccessSimulator.Services;

public class MemoryManagerService : IMemoryManagerService
{
    private readonly AppSettings _appSettings;
    private RAM _ram;
    private int PageSizeInBytes { get; init; }
    private uint PageSizeInNumberOfRecords { get; init; }
    public PageIOStatistics PageIOStatistics { get; init; } = new();

    
    public MemoryManagerService(AppSettings appSettings)
    {
        _appSettings = appSettings;
        _ram = new RAM(appSettings);
        PageSizeInBytes = appSettings.PageSizeInNumberOfRecords * appSettings.RecordSizeInBytes;
        PageSizeInNumberOfRecords = (uint)appSettings.PageSizeInNumberOfRecords;
    }
    
    public void SavePageToFile(byte[] pageAsByteStream, Guid pageID)
    {
        var filePath = $"Disk/{pageID.ToString()}.bin";
        File.WriteAllBytes(filePath, pageAsByteStream);
    }

    public BTreeNodePage? GetRootPage()
    {
        return _ram.BTreeRootPage;
    }

    public void SetRootPage(BTreeNodePage? nodePage)
    {
        _ram.BTreeRootPage = nodePage;
    }

    public (Guid pageID, uint offset) GetFreeSpaceForRecord()
    {
        return _ram.FreeSlots.Count == 0 ? ((Guid pageID, uint offset))(Guid.Empty, 0) : _ram.FreeSlots.Pop();
    }

    public void AddFreeSpaceForRecord((Guid pageID, uint offset) freeSpace)
    {
        _ram.FreeSlots.Push(freeSpace);
    }

    public BTreeNodePage GetBTreePageFromDisk(Guid pageID)
    {
        if(_ram.CheckCacheForSpecificPage(pageID) && _appSettings.EnableCaching)
        {
            return _ram.GetPageFromCache(pageID);
        }

        var filePath = $"Disk/{pageID}.bin";
        var data = File.ReadAllBytes(filePath);
        var node = BTreeNodePageSerializer.Deserialize(data);
        _ram.AddPageToCache(node);
        return node;
    }
    
    public RecordsPage GetRecordsPageFromDisk(Guid pageID)
    {
        var filePath = $"Disk/{pageID}.bin";
        var data = File.ReadAllBytes(filePath);
        var node = RecordsPageSerializer.Deserialize(data);
        return node;
    }
}