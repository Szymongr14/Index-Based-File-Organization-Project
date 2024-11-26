using BTreeIndexedFileSimulator.Interfaces;
using MemoryPageAccessSimulator.Models;

namespace BTreeIndexedFileSimulator.DataInputStrategies;

public class RandomDataInput : IDatasetInputStrategy
{
    private readonly AppSettings _appSettings;
    
    public RandomDataInput(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public IEnumerable<Record> GetRecords()
    {
        var records = new List<Record>();
        var random = new Random();
        for (uint i = 0; i < _appSettings.NumberOfRecordsToGenerate; i++)
        {
            var x = random.NextDouble() * 100;
            var y = random.NextDouble() * 100;
            records.Add(new Record(x, y, i+1));
        }

        return records;
    }
}