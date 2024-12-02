using MemoryPageAccessSimulator.Models;

namespace MemoryPageAccessSimulator.Utilities;

public static class BTreeNodePageSerializer
{
    public static byte[] Serialize(BTreeNodePage node)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(node.PageID.ToByteArray());
        writer.Write(node.IsLeaf);
        writer.Write(node.IsRoot);
        writer.Write(node.Keys.Count);

        foreach (var key in node.Keys)
        {
            writer.Write(key);
        }

        foreach (var address in node.Addresses)
        {
            writer.Write(address.pageID.ToByteArray());
            writer.Write(address.offset);
        }

        if (node.IsLeaf) return ms.ToArray();
        foreach (var childPointer in node.ChildPointers)
        {
            writer.Write(childPointer.ToByteArray());
        }

        return ms.ToArray();
    }
    
    public static BTreeNodePage Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        
        // Read header
        var currentPageID = new Guid(reader.ReadBytes(16));
        var isLeaf = reader.ReadBoolean();
        var isRoot = reader.ReadBoolean();
        var keysCount = reader.ReadInt32();
        
        var node = new BTreeNodePage(currentPageID, isLeaf, isRoot);
        
        for (var i = 0; i < keysCount; i++)
        {
            node.Keys.Add(reader.ReadUInt32());
        }

        for (var i = 0; i < keysCount; i++)
        {
            var pageID = reader.ReadBytes(16);
            var offset = reader.ReadUInt32();
            node.Addresses.Add((new Guid(pageID), offset));
        }

        if (isLeaf) return node;
        
        for (var i = 0; i < keysCount + 1; i++)
        {
            node.ChildPointers.Add(new Guid(reader.ReadBytes(16)));
        }

        return node;
    }
}
