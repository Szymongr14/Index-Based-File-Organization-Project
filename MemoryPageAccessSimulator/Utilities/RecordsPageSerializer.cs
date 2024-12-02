using MemoryPageAccessSimulator.Models;

namespace MemoryPageAccessSimulator.Utilities;

public static class RecordsPageSerializer
{
    public static byte[] Serialize(RecordsPage page)
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
    
    public static RecordsPage Deserialize(byte[] data)
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
}
