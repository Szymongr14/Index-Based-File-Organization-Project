using BTreeIndexedFileSimulator.Models;
using MemoryPageAccessSimulator.Models;

namespace BTreeIndexedFileSimulator.Interfaces;

public interface IBTreeDiskService
{
    public void InsertRecord(Record record, Guid pageID, uint offset);
    public (Guid pageId, uint offset) FindAddressOfKey(uint key);
    // public void UpdateRecord(Record record);
    public void PrintBTree();
    public bool DeleteRecord(uint key);
}