namespace MemoryPageAccessSimulator.Models;

public class BTreeNodePage
{
    public Guid PageID { get; set; }
    public List<uint> Keys { get; set; }
    public List<Guid> ChildPointers { get; set; }
    public List<(Guid pageID, uint offset)> Addresses { get; set; }
    public bool IsLeaf { get; set; }
    public bool IsRoot { get; set; }

    public BTreeNodePage(Guid pageID, bool isLeaf, bool isRoot, int treeDegree)
    {
        PageID = pageID;
        // Keys = [..new uint[treeDegree - 1]];
        // ChildPointers = [..new Guid[treeDegree + 1]];
        // Addresses = [..new (Guid, uint)[treeDegree]];
        Keys = [];
        ChildPointers = [];
        Addresses = [];
        IsLeaf = isLeaf;
        IsRoot = isRoot;
    }
}