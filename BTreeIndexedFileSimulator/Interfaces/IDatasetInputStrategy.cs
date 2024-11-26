using MemoryPageAccessSimulator.Models;

namespace BTreeIndexedFileSimulator.Interfaces;

public interface IDatasetInputStrategy
{
    IEnumerable<Record> GetRecords();
}