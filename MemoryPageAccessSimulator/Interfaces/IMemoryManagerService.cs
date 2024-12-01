using MemoryPageAccessSimulator.Models;

namespace MemoryPageAccessSimulator.Interfaces;

public interface IMemoryManagerService
{

    public byte[] SerializeBTreeNodePage(BTreeNodePage node);
    public byte[] SerializeRecordsPage(RecordsPage page);
    public RecordsPage DeserializeRecordsPage(byte[] data);
    public BTreeNodePage? DeserializeBTreeNodePage(byte[] data);
    public void SavePageToFile(byte[] pageAsByteStream, Guid pageID);
    public BTreeNodePage? GetRootPage();
    public void SetRootPage(BTreeNodePage? nodePage);
    public (Guid pageID, uint offset) GetFreeSpaceForRecord();
    public void AddFreeSpaceForRecord((Guid pageID, uint offset) freeSpace);
    public BTreeNodePage? GetBTreePageFromDisk(Guid pageID);
}