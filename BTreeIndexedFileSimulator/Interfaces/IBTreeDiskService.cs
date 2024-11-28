using BTreeIndexedFileSimulator.Models;
using MemoryPageAccessSimulator.Models;

namespace BTreeIndexedFileSimulator.Interfaces;

public interface IBTreeDiskService
{
    public void InsertRecord(Record record, Guid pageID, uint offset);
    // public void DeleteRecord(Record record);
    public Record FindRecord(Record record);
    // public void UpdateRecord(Record record);
    public void PrintAllRecords();
    public void PrintRecordsInOrder();
    public void PrintRecordsByPage();

}