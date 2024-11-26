using MemoryPageAccessSimulator.Interfaces;
using MemoryPageAccessSimulator.Models;

namespace MemoryPageAccessSimulator.Services;

public class MemoryManagerService : IMemoryManagerService
{
    private readonly AppSettings _appSettings;
    private RAM _ram;
    private int PageSizeInBytes { get; init; }
    private uint PageSizeInNumberOfRecords { get; init; }
    public PageIOStatistics PageIOStatistics { get; init; } = new();

    
    public MemoryManagerService(AppSettings appSettings)
    {
        _appSettings = appSettings;
        _ram = new RAM(appSettings);
        PageSizeInBytes = appSettings.PageSizeInNumberOfRecords * appSettings.RecordSizeInBytes;
        PageSizeInNumberOfRecords = (uint)appSettings.PageSizeInNumberOfRecords;
    }
    
    public byte[] SerializeRecordsPage(RecordsPage page)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(page.PageID.ToByteArray());
        writer.Write(page.Records.Count);

        foreach (var record in page.Records)
        {
            writer.Write(record.Key);
            writer.Write(record.X);
            writer.Write(record.Y);
        }

        return ms.ToArray();
    }
    
    public byte[] SerializeBTreeNodePage(BTreeNodePage node)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(node.PageID.ToByteArray());
        writer.Write(node.IsLeaf);
        writer.Write(node.Keys.Count);

        foreach (var key in node.Keys)
        {
            writer.Write(key);
        }

        foreach (var address in node.Addresses)
        {
            writer.Write(address.pageID);
            writer.Write(address.offset);
        }

        if (node.IsLeaf) return ms.ToArray();
        foreach (var childPointer in node.ChildPointers)
        {
            writer.Write(childPointer);
        }

        return ms.ToArray();
    }
    
    public RecordsPage DeserializeRecordsPage(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        var pageID = new Guid(reader.ReadBytes(16));
        var page = new RecordsPage(pageID);

        var recordCount = reader.ReadInt32();
        for (var i = 0; i < recordCount; i++)
        {
            var key = reader.ReadUInt32();
            var x = reader.ReadDouble();
            var y = reader.ReadDouble();
                
            page.Records.Add(new Record(x, y, key));
        }

        return page;
    }
    
    public BTreeNodePage DeserializeBTreeNodePage(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        
        var pageID = new Guid(reader.ReadBytes(16));
        var isLeaf = reader.ReadBoolean();
        var node = new BTreeNodePage(pageID, isLeaf);

        var keyCount = reader.ReadInt32();
        for (var i = 0; i < keyCount; i++)
        {
            node.Keys.Add(reader.ReadUInt32());
        }

        var addressCount = reader.ReadInt32();
        for (var i = 0; i < addressCount; i++)
        {
            var page = reader.ReadUInt32();
            var offset = reader.ReadUInt32();
            node.Addresses.Add((page, offset));
        }

        if (isLeaf) return node;
        
        var childPointerCount = keyCount + 1;
        for (var i = 0; i < childPointerCount; i++)
        {
            node.ChildPointers.Add(reader.ReadUInt32());
        }

        return node;
    }
    
    public void SavePageToFile(byte[] pageAsByteStream, Guid pageID)
    {
        var filePath = $"Disk/{pageID.ToString()}.bin";
        File.WriteAllBytes(filePath, pageAsByteStream);
    }
    
}