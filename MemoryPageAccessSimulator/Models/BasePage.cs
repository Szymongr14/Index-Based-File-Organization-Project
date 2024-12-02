using MemoryPageAccessSimulator.Interfaces;

namespace MemoryPageAccessSimulator.Models;

public abstract class BasePage : IPage
{
    public Guid PageID { get; set; }

    protected BasePage(Guid pageID)
    {
        PageID = pageID;
    }

}