using MemoryPageAccessSimulator.Models;

namespace MemoryPageAccessSimulator.Interfaces;

public interface IMemoryManagerService
{
    public void WriteInitialRecordsToBinaryFile(IEnumerable<Record> records, string filePath);
    public void WritePageToTape(Page page, string filePath);
    public void InsertPageIntoRAM(Page page);
    public Page GetPageFromRAM(int pageNumber);
    public bool RAMIsFull();
    public List<Record> GetRecordsFromRAM();
    public void WriteRecordsToRAM(List<Record> records);
    public bool RAMIsEmpty();
    public void RemovePageFromRAM(int pageNumber);
    public Page GetLastPageFromRAM();
    public void RemoveLastPageFromRAM();
    public int GetMaxPageOffsetForFile(string filePath);
    public (int totalReads, int totalWrites) GetTotalReadsAndWrites();
    public void RemoveFirstRecordFromGivenPage(int pageNumber);
    public Record GetFirstRecordFromGivenPage(int pageNumber);
    public void InsertPageIntoRAMAtGivenIndex(Page page, int index);
    public void ClearRAMPages();
    public void MoveRecordToPage(int pageNumber, Record record);
    public bool PageIsEmpty(int pageNumber);
    public void ClearPage(int pageNumber);
    public void InitializeEmptyPagesInRAM();
}