using BTreeIndexedFileSimulator.Enums;

namespace BTreeIndexedFileSimulator.Models;

public class Command(CommandType type, List<string> parameters)
{
    public CommandType Type { get; set; } = type;
    public List<string> Parameters { get; set; } = parameters;
}