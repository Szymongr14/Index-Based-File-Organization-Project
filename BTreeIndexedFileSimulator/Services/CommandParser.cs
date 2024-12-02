using BTreeIndexedFileSimulator.Enums;
using BTreeIndexedFileSimulator.Interfaces;
using BTreeIndexedFileSimulator.Models;
using Microsoft.Extensions.Logging;

namespace BTreeIndexedFileSimulator.Services;

public class CommandParser : ICommandParser{
    
    private readonly ILogger<CommandParser> _logger;
    
    public CommandParser(IBTreeDiskService bTreeDiskService, ILogger<CommandParser> logger)
    {
        _logger = logger;
    }

    public List<Command> ParseCommandsFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath);

        return lines.Select(ParseCommand).OfType<Command>().ToList();
    }

    private static Command? ParseCommand(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        var commandType = parts[0] switch
        {
            "i" => CommandType.Insert,
            "f" => CommandType.Find,
            "d" => CommandType.Delete,
            "u" => CommandType.Update,
            _ => throw new InvalidOperationException($"Unknown command: {parts[0]}")
        };

        var parameters = parts.Skip(1).Select(int.Parse).ToList();

        return new Command(commandType, parameters); 
    }
}