namespace MemoryPageAccessSimulator.Models;

public class AppSettings
{
    public int PageSizeInNumberOfRecords { get; init; }
    public int RAMSizeInNumberOfPages { get; init; }
    public int RecordSizeInBytes { get; init; }
    public string DataSource { get; init; } = null!;
    public int? NumberOfRecordsToGenerate { get; init; }
    public string LogLevel { get; init; } = null!;
   public string FilePathToRecords { get; init; } = null!;
   public string FilePathToInstructions { get; init; } = null!;
   public int TreeDegree { get; init; }
   public bool EnableCaching { get; init; }
   public bool EnableNodeCompensation { get; init; }
}