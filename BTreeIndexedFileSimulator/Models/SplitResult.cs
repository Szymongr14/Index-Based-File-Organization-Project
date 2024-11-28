using MemoryPageAccessSimulator.Models;

namespace BTreeIndexedFileSimulator.Models;

public class SplitResult
{
    public uint PromotedKey { get; set; }
    public BTreeNodePage? NewNode { get; set; }
    public (Guid pageID, uint offset) PromotedKeyAddress { get; set; }

    public SplitResult(uint promotedKey, (Guid, uint) promotedKeyAddress,BTreeNodePage? newNode)
    {
        PromotedKey = promotedKey;
        NewNode = newNode;
        PromotedKeyAddress = promotedKeyAddress;
    }
}