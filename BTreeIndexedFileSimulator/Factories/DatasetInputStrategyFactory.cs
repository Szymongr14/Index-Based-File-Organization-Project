using BTreeIndexedFileSimulator.DataInputStrategies;
using BTreeIndexedFileSimulator.Interfaces;
using MemoryPageAccessSimulator.Models;

namespace BTreeIndexedFileSimulator.Factories;

public class DatasetInputStrategyFactory : IDatasetInputStrategyFactory
{
    private readonly AppSettings _appSettings;

    public DatasetInputStrategyFactory(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public IDatasetInputStrategy Create()
    {
        return _appSettings.DataSource switch
        {
            "GenerateRandomly" => new RandomDataInput(_appSettings),
            "ProvideManually" => new ManualDataInput(_appSettings),
            "LoadFromFile" => new FileDataInput(_appSettings),
            _ => throw new NotImplementedException("Unsupported data source.")
        };
    }
}