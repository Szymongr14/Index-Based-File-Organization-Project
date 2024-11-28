using MemoryPageAccessSimulator.Models;

namespace BTreeIndexedFileSimulator.Models;

public class SplitResult
{
    public uint PromotedKey { get; set; }
    public BTreeNodePage? NewNode { get; set; }

    public SplitResult(uint promotedKey, BTreeNodePage? newNode)
    {
        PromotedKey = promotedKey;
        NewNode = newNode;
    }
}