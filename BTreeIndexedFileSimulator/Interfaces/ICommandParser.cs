using BTreeIndexedFileSimulator.Models;

namespace BTreeIndexedFileSimulator.Interfaces;

public interface ICommandParser
{
    public List<Command> ParseCommandsFile(string filePath);
}