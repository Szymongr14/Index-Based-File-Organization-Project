using BTreeIndexedFileSimulator.Enums;

namespace BTreeIndexedFileSimulator.Models;

public class Command(CommandType type, List<int> parameters)
{
    public CommandType Type { get; set; } = type;
    public List<int> Parameters { get; set; } = parameters;
}