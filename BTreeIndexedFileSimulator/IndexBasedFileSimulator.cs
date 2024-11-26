using BTreeIndexedFileSimulator.Interfaces;
using MemoryPageAccessSimulator.Interfaces;
using MemoryPageAccessSimulator.Models;
using Microsoft.Extensions.Logging;

namespace BTreeIndexedFileSimulator;

public class IndexBasedFileSimulator
{
    private readonly IDatasetInputStrategy _datasetInputStrategy;
    private readonly AppSettings _appSettings;
    private readonly IMemoryManagerService _memoryManagerService;
    private readonly ILogger<IndexBasedFileSimulator> _logger;
    private const string InitialRecordsFile = "initial_records.bin";
    private const string DiskDirPath = "Disk";

    public IndexBasedFileSimulator(AppSettings appSettings,
        IDatasetInputStrategy datasetInputStrategy, IMemoryManagerService memoryManagerService,
        ILogger<IndexBasedFileSimulator> logger)
    {
        _appSettings = appSettings;
        _datasetInputStrategy = datasetInputStrategy;
        _memoryManagerService = memoryManagerService;
        _logger = logger;
    }

    public void Start()
    {
        var records = _datasetInputStrategy.GetRecords();
        SaveRecordsOnDisk(records);
    }

    private void SaveRecordsOnDisk(IEnumerable<Record> records)
    {
        //TODO: implement creating B-Tree index table in this method
        var recordIndex = 0;
        var page = new RecordsPage(Guid.NewGuid());

        foreach (var record in records)
        {
            page.Records.Add(record);
            recordIndex++;

            if (recordIndex % _appSettings.PageSizeInNumberOfRecords == 0)
            {
                var pageAsByteStream = _memoryManagerService.SerializeRecordsPage(page);
                _memoryManagerService.SavePageToFile(pageAsByteStream, page.PageID);

                page = new RecordsPage(Guid.NewGuid());
            }
        }

        if (page.Records.Count > 0)
        {
            var pageAsByteStream = _memoryManagerService.SerializeRecordsPage(page);
            _memoryManagerService.SavePageToFile(pageAsByteStream, page.PageID);
        }
    }
}