using BTreeIndexedFileSimulator.Interfaces;
using MemoryPageAccessSimulator.Interfaces;
using MemoryPageAccessSimulator.Models;
using Microsoft.Extensions.Logging;

namespace BTreeIndexedFileSimulator.Services;

public class IndexBasedFileSimulator
{
    private readonly IDatasetInputStrategy _datasetInputStrategy;
    private readonly AppSettings _appSettings;
    private readonly IMemoryManagerService _memoryManagerService;
    private readonly ILogger<IndexBasedFileSimulator> _logger;
    private readonly IBTreeDiskService _bTreeDiskService;
    private const string DiskDirPath = "Disk";
    private const int RecordsPageHeaderSizeInBytes = 20;

    public IndexBasedFileSimulator(AppSettings appSettings,
        IDatasetInputStrategy datasetInputStrategy, IMemoryManagerService memoryManagerService,
        ILogger<IndexBasedFileSimulator> logger, IBTreeDiskService bTreeDiskService)
    {
        _appSettings = appSettings;
        _datasetInputStrategy = datasetInputStrategy;
        _memoryManagerService = memoryManagerService;
        _logger = logger;
        _bTreeDiskService = bTreeDiskService;
    }

    public void Start()
    {
        var records = _datasetInputStrategy.GetRecords();
        SaveRecordsOnDisk(records);
        _bTreeDiskService.PrintBTree();
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
                var pageAsByteStream = _memoryManagerService.SerializeRecordsPage(page);
                _memoryManagerService.SavePageToFile(pageAsByteStream, page.PageID);
                offsetInPage = RecordsPageHeaderSizeInBytes;
                page = new RecordsPage(Guid.NewGuid());
            }
        }

        if (page.Records.Count > 0)
        {
            var pageAsByteStream = _memoryManagerService.SerializeRecordsPage(page);
            _memoryManagerService.SavePageToFile(pageAsByteStream, page.PageID);
            _memoryManagerService.AddFreeSpaceForRecord((page.PageID, (uint)offsetInPage));
        }
    }
}