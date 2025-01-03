using MemoryPageAccessSimulator.Enums;
using MemoryPageAccessSimulator.Models;

namespace MemoryPageAccessSimulator.Interfaces;

public interface IMemoryManagerService
{

    public void SavePageToFile(byte[] pageAsByteStream, Guid pageID, PageType pageType);
    public BTreeNodePage? GetRootPage();
    public void SetRootPage(BTreeNodePage? nodePage);
    public (Guid pageID, uint offset) GetFreeSpaceForRecord();
    public void AddFreeSpaceForRecord((Guid pageID, uint offset) freeSpace);
    public BTreeNodePage GetBTreePageFromDisk(Guid pageID);
    public RecordsPage GetRecordsPageFromDisk(Guid pageID);
    public void ClearIOStatistics();
    public void PrintIOStatistics();
    public void DeletePageFromDisk(Guid pageID);
    public void PrintIOSummary();
    public (Guid pageID, uint offset) GetNextFreeSlot();
    public void SetNextFreeSlot((Guid pageID, uint offset) nextFreeSlot);
}