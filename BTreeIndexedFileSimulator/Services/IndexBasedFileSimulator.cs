using BTreeIndexedFileSimulator.Enums;
using BTreeIndexedFileSimulator.Interfaces;
using BTreeIndexedFileSimulator.Models;
using MemoryPageAccessSimulator.Enums;
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
        ProcessInitialRecords();
        ProcessInstructions();
    }
    
    private void ProcessInitialRecords()
    {
        var records = _datasetInputStrategy.GetRecords();
        SaveRecordsOnDisk(records);
    }
    
    private void ProcessInstructions()
    {
        var commands = _commandParser.ParseCommandsFile(_appSettings.FilePathToInstructions);
        foreach (var command in commands)
        {
            ExecuteCommand(command);
        }
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
                _memoryManagerService.SavePageToFile(pageAsByteStream, page.PageID, PageType.Records);
                offsetInPage = RecordsPageHeaderSizeInBytes;
                page = new RecordsPage(Guid.NewGuid());
            }
        }

        if (page.Records.Count > 0)
        {
            var pageAsByteStream = RecordsPageSerializer.Serialize(page);
            _memoryManagerService.SavePageToFile(pageAsByteStream, page.PageID, PageType.Records);
            _memoryManagerService.AddFreeSpaceForRecord((page.PageID, (uint)offsetInPage));
        }
    }

    private void InsertRecordAndUpdateBTree(Record record)
    {
        if(FindRecord(record.Key) != null)
        {
            _logger.LogError($"Record with key {record.Key} already exists.");
            return;
        }
        
        var (pageID, offset) = _memoryManagerService.GetFreeSpaceForRecord();
        if (pageID == Guid.Empty)
        {
            var page = new RecordsPage(Guid.NewGuid());
            page.Records.Add(record);
            _memoryManagerService.SavePageToFile(RecordsPageSerializer.Serialize(page), page.PageID, PageType.Records);
            _bTreeDiskService.InsertRecord(record, page.PageID, RecordsPageHeaderSizeInBytes);
            _memoryManagerService.AddFreeSpaceForRecord((page.PageID, RecordsPageHeaderSizeInBytes + (uint)_appSettings.RecordSizeInBytes));
        }
        else
        {
            var recordsPage = _memoryManagerService.GetRecordsPageFromDisk(pageID);
            recordsPage.Records.Insert(((int)offset - RecordsPageHeaderSizeInBytes) / _appSettings.RecordSizeInBytes, record);
            _memoryManagerService.SavePageToFile(RecordsPageSerializer.Serialize(recordsPage), recordsPage.PageID, PageType.Records);
            _bTreeDiskService.InsertRecord(record, recordsPage.PageID, offset);
            if (!IsPageFull(recordsPage))
            {
                _memoryManagerService.AddFreeSpaceForRecord((recordsPage.PageID, offset + (uint)_appSettings.RecordSizeInBytes));
            }
            
        }
    }
    
    private bool IsPageFull(RecordsPage page)
    {
        return page.Records.Count == _appSettings.PageSizeInNumberOfRecords;
    }

    private Record? FindRecord(uint key)
    {
        var (pageID, offset) = _bTreeDiskService.FindAddressOfKey(key);
        if (pageID == Guid.Empty)
        {
            _logger.LogError($"Record with key {key} not found.");
            return null;
        }

        var pageWithRecord = _memoryManagerService.GetRecordsPageFromDisk(pageID);
        var recordIndex = (int)(offset - RecordsPageHeaderSizeInBytes) / _appSettings.RecordSizeInBytes;
        var record = pageWithRecord.Records[recordIndex];
        return record;
    }
    
    private void ExecuteCommand(Command command)
    {
        switch (command.Type)
        {
            case CommandType.Insert:
                var keyToInsert = command.Parameters[2];
                _memoryManagerService.ClearIOStatistics();
                InsertRecordAndUpdateBTree(new Record(Convert.ToDouble(command.Parameters[0]), Convert.ToDouble(command.Parameters[1]), Convert.ToUInt32(keyToInsert)));
                Console.WriteLine($"Inserted record with key {keyToInsert}");
                break;

            case CommandType.Find:
                var keyToFind = command.Parameters[0];
                _memoryManagerService.ClearIOStatistics();
                var record = FindRecord(Convert.ToUInt32(keyToFind));
                if (record == null) break;
                Console.WriteLine($"Found record with key {keyToFind}: X:{record.X}, Y:{record.Y}");
                break;

            case CommandType.Delete:
                var keyToDelete = command.Parameters[0];
                _memoryManagerService.ClearIOStatistics();
                if (_bTreeDiskService.DeleteRecord(Convert.ToUInt32(keyToDelete)))
                {
                    Console.WriteLine($"Deleted key {keyToDelete}");
                    
                }
                else
                {
                    _logger.LogError($"Key {keyToDelete} not found");
                }
                break;

            case CommandType.Update:
                var keyToUpdate = command.Parameters[0];
                var x = Convert.ToDouble(command.Parameters[1]);
                var y = Convert.ToDouble(command.Parameters[2]);
                
                if(_bTreeDiskService.FindAddressOfKey(Convert.ToUInt32(keyToUpdate)).pageId == Guid.Empty)
                {
                    _logger.LogError($"Key {keyToUpdate} not found");
                    break;
                }
                
                if (command.Parameters.Count == 4) // new key value is provided
                {
                    var newKey = command.Parameters[3];
                    UpdateRecord(Convert.ToUInt32(keyToUpdate), x, y, Convert.ToUInt32(newKey));
                    Console.WriteLine($"Updated key {keyToUpdate} with values X:{x}, Y:{y} and new Key:{newKey}");
                    break;
                }

                UpdateRecord(Convert.ToUInt32(keyToUpdate), x, y);
                Console.WriteLine($"Updated key {keyToUpdate} with values X:{x}, Y:{y}");

                break;
            
            case CommandType.Print:
                var printOption = command.Parameters[0];
                switch (printOption)
                {
                    case "records":
                        _memoryManagerService.ClearIOStatistics();
                        PrintRecordsInOrder();
                        break;
                    case "btree":
                        _memoryManagerService.ClearIOStatistics();
                        _bTreeDiskService.PrintBTree();
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported print option");
                }

                break;

            default:
                throw new InvalidOperationException("Unsupported command");
        }
        _memoryManagerService.PrintIOStatistics();
    }
    
    private void PrintRecordsInOrder()
    {
        var stack = new Stack<(BTreeNodePage, int)>();
        var rootNode = _memoryManagerService.GetRootPage();
        stack.Push((rootNode, 0)!);

        while (stack.Count > 0)
        {
            var (node, i) = stack.Pop();
            if (node.IsLeaf)
            {
                foreach (var address in node.Addresses)
                {
                    var record = GetRecordFromAddress(address);
                    Console.WriteLine($"Key: {record.Key}, X: {record.X}, Y: {record.Y}");
                }
            }
            else if (i < node.ChildPointers.Count)
            {
                if (i > 0)
                {
                    var record = GetRecordFromAddress(node.Addresses[i - 1]);
                    Console.WriteLine($"Key: {record.Key}, X: {record.X}, Y: {record.Y}");
                }
                stack.Push((node, i + 1));
                stack.Push((_memoryManagerService.GetBTreePageFromDisk(node.ChildPointers[i]), 0));
            }
        }
    }
    
    private Record GetRecordFromAddress((Guid pageID, uint offset) address)
    {
        var page = _memoryManagerService.GetRecordsPageFromDisk(address.pageID);
        var recordIndex = ((int)address.offset - RecordsPageHeaderSizeInBytes) / _appSettings.RecordSizeInBytes;
        return page.Records[recordIndex];
    }
    
    private bool UpdateRecord(uint keyToUpdate,  double x, double y, uint? newKey = null)
    {
        if (newKey.HasValue)
        {
            if (!_bTreeDiskService.DeleteRecord(keyToUpdate)) return false;
            InsertRecordAndUpdateBTree(new Record(x, y, newKey.Value));
            return true;
        }
        
        var addressOfKeyToUpdate = _bTreeDiskService.FindAddressOfKey(keyToUpdate);
        if (addressOfKeyToUpdate.pageId == Guid.Empty) return false;

        var recordsPage = _memoryManagerService.GetRecordsPageFromDisk(addressOfKeyToUpdate.pageId); 
        var recordIndex = ((int)addressOfKeyToUpdate.offset - RecordsPageHeaderSizeInBytes) / _appSettings.RecordSizeInBytes;
        recordsPage.Records[recordIndex] = new Record(x, y, keyToUpdate);
        
        _memoryManagerService.SavePageToFile(RecordsPageSerializer.Serialize(recordsPage), recordsPage.PageID, PageType.Records);
        return true;
    }
}