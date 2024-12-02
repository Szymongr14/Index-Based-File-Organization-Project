using BTreeIndexedFileSimulator.Enums;

namespace BTreeIndexedFileSimulator.Models;

public class Command(CommandType type, List<double> parameters)
{
    public CommandType Type { get; set; } = type;
    public List<double> Parameters { get; set; } = parameters;
}