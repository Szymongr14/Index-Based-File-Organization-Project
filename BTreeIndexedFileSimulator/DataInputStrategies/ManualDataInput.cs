using BTreeIndexedFileSimulator.Interfaces;
using MemoryPageAccessSimulator.Models;

namespace BTreeIndexedFileSimulator.DataInputStrategies;

public class ManualDataInput : IDatasetInputStrategy
{
    private readonly AppSettings _appSettings;

    public ManualDataInput(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public IEnumerable<Record> GetRecords()
    {
        var records = new List<Record>();
        for (var i = 0; i < _appSettings.NumberOfRecordsToGenerate; i++)
        {
            //TODO: handle case when key already exist in dataset
            Console.WriteLine("Enter Key:");
            var key = uint.Parse(Console.ReadLine() ?? string.Empty);
            Console.WriteLine("Enter X:");
            var x = double.Parse(Console.ReadLine() ?? string.Empty);
            Console.WriteLine("Enter Y:");
            var y = double.Parse(Console.ReadLine() ?? string.Empty);
            records.Add(new Record(x, y, key));
        }

        return records;
    }
}