namespace MemoryPageAccessSimulator.Models;

public class BTreeNodePage
{
    public Guid PageID { get; set; }
    public List<uint> Keys { get; set; }
    public List<uint> ChildPointers { get; set; }
    public List<(uint pageID, uint offset)> Addresses { get; set; }
    public bool IsLeaf { get; set; }

    public BTreeNodePage(Guid pageID, bool isLeaf)
    {
        PageID = pageID;
        Keys = [];
        ChildPointers = [];
        Addresses = [];
        IsLeaf = isLeaf;
    }
}