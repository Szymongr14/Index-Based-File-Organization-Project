namespace MemoryPageAccessSimulator.Models;

public class BTreeNodePage : BasePage
{
    public List<uint> Keys { get; set; }
    public List<Guid> ChildPointers { get; set; }
    public List<(Guid pageID, uint offset)> Addresses { get; set; }
    public bool IsLeaf { get; set; }
    public bool IsRoot { get; set; }

    public BTreeNodePage(Guid pageID, bool isLeaf, bool isRoot)
        : base(pageID)
    {
        Keys = [];
        ChildPointers = [];
        Addresses = [];
        IsLeaf = isLeaf;
        IsRoot = isRoot;
    }
}