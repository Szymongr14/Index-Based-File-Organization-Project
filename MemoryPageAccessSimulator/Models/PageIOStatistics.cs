namespace MemoryPageAccessSimulator.Models;

public class PageIOStatistics
{
    public int TotalBtreePagesReads { get; private set; }
    public int TotalBTreePagesWrites { get; private set; }
    public int TotalRecordsPagesReads { get; private set; }
    public int TotalRecordsPagesWrites { get; private set; }

    public void IncrementBTreeRead()
    {
        TotalBtreePagesReads++;
    }

    public void IncrementBTreeWrite()
    {
        TotalBTreePagesWrites++;
    }
    
    public void IncrementRecordsRead()
    {
        TotalRecordsPagesReads++;
    }
    
    public void IncrementRecordsWrite()
    {
        TotalRecordsPagesWrites++;
    }
    
    public void Clear()
    {
        TotalBtreePagesReads = 0;
        TotalBTreePagesWrites = 0;
        TotalRecordsPagesReads = 0;
        TotalRecordsPagesWrites = 0;
    }
    
}