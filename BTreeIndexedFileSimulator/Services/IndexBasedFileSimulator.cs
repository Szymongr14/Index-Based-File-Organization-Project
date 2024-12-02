using BTreeIndexedFileSimulator.Enums;
using BTreeIndexedFileSimulator.Interfaces;
using BTreeIndexedFileSimulator.Models;
using MemoryPageAccessSimulator.Interfaces;
using MemoryPageAccessSimulator.Models;
using MemoryPageAccessSimulator.Utilities;
using Microsoft.Extensions.Logging;

namespace BTreeIndexedFileSimulator.Services;

public class IndexBasedFileSimulator
{
    private readonly IDatasetInputStrategy _datasetInputStrategy;
    private readonly AppSettings _appSettings;
    private readonly IMemoryManagerService _memoryManagerService;
    private readonly ILogger<IndexBasedFileSimulator> _logger;
    private readonly IBTreeDiskService _bTreeDiskService;
    private readonly ICommandParser _commandParser;
    private const int RecordsPageHeaderSizeInBytes = 20;

    public IndexBasedFileSimulator(AppSettings appSettings,
        IDatasetInputStrategy datasetInputStrategy, IMemoryManagerService memoryManagerService,
        ILogger<IndexBasedFileSimulator> logger, IBTreeDiskService bTreeDiskService, ICommandParser commandParser)
    {
        _appSettings = appSettings;
        _datasetInputStrategy = datasetInputStrategy;
        _memoryManagerService = memoryManagerService;
        _logger = logger;
        _bTreeDiskService = bTreeDiskService;
        _commandParser = commandParser;
    }

    public void Start()
    {
        var records = _datasetInputStrategy.GetRecords();
        SaveRecordsOnDisk(records);
        _bTreeDiskService.PrintBTree();
        var commands = _commandParser.ParseCommandsFile(_appSettings.FilePathToInstructions);
        foreach (var command in commands)
        {
            ExecuteCommand(command);
        }
        var (pageID, offset) = _bTreeDiskService.FindAddressOfKey(1);
        Console.WriteLine($"PageID: {pageID} Offset: {offset}");
    }

    private void SaveRecordsOnDisk(IEnumerable<Record> records)
    {
        var recordIndex = 0;
        var page = new RecordsPage(Guid.NewGuid());
        var offsetInPage = RecordsPageHeaderSizeInBytes;

        foreach (var record in records)
        {
            page.Records.Add(record);
            _bTreeDiskService.InsertRecord(record, page.PageID, (uint)offsetInPage);
            offsetInPage += _appSettings.RecordSizeInBytes;
            recordIndex++;

            if (recordIndex % _appSettings.PageSizeInNumberOfRecords == 0)
            {
                var pageAsByteStream = RecordsPageSerializer.Serialize(page);
                _memoryManagerService.SavePageToFile(pageAsByteStream, page.PageID);
                offsetInPage = RecordsPageHeaderSizeInBytes;
                page = new RecordsPage(Guid.NewGuid());
            }
        }

        if (page.Records.Count > 0)
        {
            var pageAsByteStream = RecordsPageSerializer.Serialize(page);
            _memoryManagerService.SavePageToFile(pageAsByteStream, page.PageID);
            _memoryManagerService.AddFreeSpaceForRecord((page.PageID, (uint)offsetInPage));
        }
    }

    // private void InsertRecordAndUpdateBTree(Record record)
    // {
    //     var (pageID, offset) = _memoryManagerService.GetFreeSpaceForRecord();
    //     if (pageID == Guid.Empty)
    //     {
    //         var page = new RecordsPage(Guid.NewGuid());
    //         page.Records.Add(record);
    //         _bTreeDiskService.InsertRecord(record, page.PageID, RecordsPageHeaderSizeInBytes);
    //         _memoryManagerService.AddFreeSpaceForRecord((page.PageID, RecordsPageHeaderSizeInBytes + (uint)_appSettings.RecordSizeInBytes));
    //     }
    //     else
    //     {
    //     }
    //
    // }

    // private Record? FindRecord(uint key)
    // {
    //     var (pageID, offset) = _bTreeDiskService.FindAddressOfKey(key);
    //     if (pageID == Guid.Empty)
    //     {
    //         _logger.LogInformation($"Record with key {key} not found.");
    //         return null;
    //     }
    //     
    //     
    //
    // }
    
    private static void ExecuteCommand(Command command)
    {
        switch (command.Type)
        {
            case CommandType.Insert:
                var keyToInsert = command.Parameters[0];
                // Handle insert (e.g., _bTreeDiskService.InsertRecord(...))
                Console.WriteLine($"Insert key {keyToInsert}");
                break;

            case CommandType.Find:
                var keyToFind = command.Parameters[0];
                // Handle find (e.g., _bTreeDiskService.FindRecord(...))
                Console.WriteLine($"Find key {keyToFind}");
                break;

            case CommandType.Delete:
                var keyToDelete = command.Parameters[0];
                // Handle delete (e.g., _bTreeDiskService.DeleteRecord(...))
                Console.WriteLine($"Delete key {keyToDelete}");
                break;

            case CommandType.Update:
                var keyToUpdate = command.Parameters[0];
                var newValue = command.Parameters[1];
                // Handle update (e.g., _bTreeDiskService.UpdateRecord(...))
                Console.WriteLine($"Update key {keyToUpdate} with value {newValue}");
                break;

            default:
                throw new InvalidOperationException("Unsupported command");
        }
    }
}