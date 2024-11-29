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
        
        var currentPageID = new Guid(reader.ReadBytes(16));
        var isLeaf = reader.ReadBoolean();
        var isRoot = reader.ReadBoolean();
        
        var node = new BTreeNodePage(currentPageID, isLeaf, isRoot, _appSettings.TreeDegree);
        
        var keysCount = reader.ReadInt32();
        
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
    
    public void SavePageToFile(byte[] pageAsByteStream, Guid pageID)
    {
        var filePath = $"Disk/{pageID.ToString()}.bin";
        File.WriteAllBytes(filePath, pageAsByteStream);
    }

    public BTreeNodePage? GetRootPage()
    {
        return _ram.BTreeRootPage;
    }

    public void SetRootPage(BTreeNodePage? nodePage)
    {
        _ram.BTreeRootPage = nodePage;
    }

    public (Guid pageID, uint offset) GetFreeSpaceForRecord()
    {
        return _ram.FreeSlots.Count == 0 ? ((Guid pageID, uint offset))(Guid.Empty, 0) : _ram.FreeSlots.Pop();
    }

    public void InsertRecordToPage(RecordsPage page, Record record)
    {
        _ram.AddPageToCache(page.PageID, page);
    }

    public void AddFreeSpaceForRecord((Guid pageID, uint offset) freeSpace)
    {
        _ram.FreeSlots.Push(freeSpace);
    }
}