using BTreeIndexedFileSimulator.Interfaces;
using MemoryPageAccessSimulator.Models;

namespace BTreeIndexedFileSimulator.DataInputStrategies;

public class FileDataInput : IDatasetInputStrategy
{
    private readonly AppSettings _appSettings;

    public FileDataInput(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public IEnumerable<Record> GetRecords()
    {
        var records = new List<Record>();
        var lines = File.ReadAllLines(_appSettings.FilePathToRecords);
        foreach (var line in lines)
        {
            var parts = line.Split(',');
            var x = double.Parse(parts[0]);
            var y = double.Parse(parts[1]);
            var key = uint.Parse(parts[2]);
            
            records.Add(new Record(x, y, key));
        }

        return records;
    }
}