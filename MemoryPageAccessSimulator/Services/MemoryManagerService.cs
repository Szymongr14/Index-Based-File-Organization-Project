using MemoryPageAccessSimulator.Enums;
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
    private PageIOStatistics PageIOStatistics { get; init; } = new();

    
    public MemoryManagerService(AppSettings appSettings)
    {
        _appSettings = appSettings;
        _ram = new RAM(appSettings);
        PageSizeInBytes = appSettings.PageSizeInNumberOfRecords * appSettings.RecordSizeInBytes;
        PageSizeInNumberOfRecords = (uint)appSettings.PageSizeInNumberOfRecords;
    }
    
    public void SavePageToFile(byte[] pageAsByteStream, Guid pageID, PageType pageType)
    {
        var filePath = $"Disk/{pageID.ToString()}.bin";
        File.WriteAllBytes(filePath, pageAsByteStream);
        switch (pageType)
        {
            case PageType.Records:
                PageIOStatistics.IncrementRecordsWrite();
                break;
            case PageType.BTreeNode:
                PageIOStatistics.IncrementBTreeWrite();
                break;
        }
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
        var filePath = $"Disk/{pageID}.bin";

        if (_appSettings.EnableCaching && _ram.CheckCacheForSpecificPage(pageID))
        {
            return _ram.GetPageFromCache(pageID);
        }

        var data = File.ReadAllBytes(filePath);
        PageIOStatistics.IncrementBTreeRead();
        var node = BTreeNodePageSerializer.Deserialize(data);

        if (_appSettings.EnableCaching)
        {
            _ram.AddPageToCache(node);
        }

        return node;
    }
    
    public RecordsPage GetRecordsPageFromDisk(Guid pageID)
    {
        var filePath = $"Disk/{pageID}.bin";
        var data = File.ReadAllBytes(filePath);
        PageIOStatistics.IncrementRecordsRead();
        var node = RecordsPageSerializer.Deserialize(data);
        return node;
    }

    public void ClearIOStatistics()
    {
        PageIOStatistics.Clear();
    }
    
    public void PrintIOStatistics()
    {
        Console.WriteLine($"IO Statistics: " +
                          $"BTreePages R({PageIOStatistics.TotalRecordsPagesReads}) W({PageIOStatistics.TotalBTreePagesWrites}), " +
                          $"RecordsPages R({PageIOStatistics.TotalRecordsPagesReads}) W({PageIOStatistics.TotalRecordsPagesWrites})");
    }
}