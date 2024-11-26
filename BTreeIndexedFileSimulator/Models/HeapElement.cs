using MemoryPageAccessSimulator.Models;

namespace BTreeIndexedFileSimulator.Models;

public record HeapElement(Record Record, int PageNumber);