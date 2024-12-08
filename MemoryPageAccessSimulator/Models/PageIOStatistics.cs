namespace MemoryPageAccessSimulator.Models;

public class PageIOStatistics
{
    public int TotalBtreePagesReads { get; private set; }
    public int TotalBTreePagesWrites { get; private set; }
    public int TotalRecordsPagesReads { get; private set; }
    public int TotalRecordsPagesWrites { get; private set; }
    public int TotalPagesReads { get; private set; }
    public int TotalPagesWrites { get; private set; }

    public void IncrementBTreeRead()
    {
        TotalBtreePagesReads++;
        TotalPagesReads++;
    }

    public void IncrementBTreeWrite()
    {
        TotalBTreePagesWrites++;
        TotalPagesWrites++;
    }
    
    public void IncrementRecordsRead()
    {
        TotalRecordsPagesReads++;
        TotalPagesReads++;
    }
    
    public void IncrementRecordsWrite()
    {
        TotalRecordsPagesWrites++;
        TotalPagesWrites++;
    }
    
    public void Clear()
    {
        TotalBtreePagesReads = 0;
        TotalBTreePagesWrites = 0;
        TotalRecordsPagesReads = 0;
        TotalRecordsPagesWrites = 0;
    }
    
}