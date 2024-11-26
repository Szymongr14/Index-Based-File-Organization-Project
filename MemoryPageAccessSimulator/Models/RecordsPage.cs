namespace MemoryPageAccessSimulator.Models;

public class RecordsPage
{
    public List<Record> Records { get; set; }
    public Guid PageID { get; set; }

    public RecordsPage(Guid pageID)
    {
        Records = [];
        PageID = pageID;
    }
    
    
    
}