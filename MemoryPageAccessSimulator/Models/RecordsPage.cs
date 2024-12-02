namespace MemoryPageAccessSimulator.Models;

public class RecordsPage : BasePage
{
    public List<Record> Records { get; set; }

    public RecordsPage(Guid pageID) : base(pageID)
    {
        Records = [];
    }
}