using BTreeIndexedFileSimulator.Interfaces;
using BTreeIndexedFileSimulator.Models;
using MemoryPageAccessSimulator.Interfaces;
using MemoryPageAccessSimulator.Models;

namespace BTreeIndexedFileSimulator.Services;

public class BTreeDiskService : IBTreeDiskService
{
    private readonly IMemoryManagerService _memoryManagerService;
    private readonly AppSettings _appSettings;
    private int Degree { get; init; }

    public BTreeDiskService(IMemoryManagerService memoryManagerService, AppSettings appSettings)
    {
        _memoryManagerService = memoryManagerService;
        _appSettings = appSettings;
        Degree = appSettings.TreeDegree;
    }

    public void InsertRecord(Record record, Guid pageID, uint offset)
    {
        var rootNode = _memoryManagerService.GetRootPage();

        // Check if the root is empty
        if (rootNode == null || rootNode.Keys.Count == 0)
        {
            // Initialize the root as a leaf node
            rootNode = new BTreeNodePage(Guid.NewGuid(), isLeaf: true, isRoot: true, Degree);
            rootNode.Keys.Add(record.Key);
            rootNode.Addresses.Add((pageID, offset));

            // Save the root node to disk
            _memoryManagerService.SavePageToFile(
                _memoryManagerService.SerializeBTreeNodePage(rootNode),
                rootNode.PageID
            );

            // Update the root in memory
            _memoryManagerService.SetRootPage(rootNode);
            return;
        }

        // Proceed with normal insertion logic
        var splitResult = InsertIntoNode(rootNode, record, pageID, offset);

        // If the root splits, create a new root
        if (splitResult != null)
        {
            var newRoot = new BTreeNodePage(Guid.NewGuid(), isLeaf: false, isRoot: true, Degree);
            newRoot.Keys.Add(splitResult.PromotedKey);
            newRoot.Addresses.Add(splitResult.PromotedKeyAddress);
            newRoot.ChildPointers.Add(rootNode.PageID);
            newRoot.ChildPointers.Add(splitResult.NewNode!.PageID);

            // Save the new root to disk
            _memoryManagerService.SavePageToFile(
                _memoryManagerService.SerializeBTreeNodePage(newRoot),
                newRoot.PageID
            );

            // Update the root in memory
            _memoryManagerService.SetRootPage(newRoot);
        }
    }


    private SplitResult? InsertIntoNode(BTreeNodePage? node, Record record, Guid pageID, uint offset)
    {
        // Check if the node is a leaf
        if (node.IsLeaf)
        {
            // Insert into the leaf node
            int insertIndex = node.Keys.BinarySearch(record.Key);
            if (insertIndex < 0) insertIndex = ~insertIndex;

            node.Keys.Insert(insertIndex, record.Key);
            node.Addresses.Insert(insertIndex, (pageID, offset));

            // Save the updated node to disk
            _memoryManagerService.SavePageToFile(
                _memoryManagerService.SerializeBTreeNodePage(node),
                node.PageID
            );

            // Check if the node is full and needs splitting
            if (node.Keys.Count > 2 * Degree)
            {
                return SplitNode(node);
            }
            // No split required
        }
        else
        {
            // Internal node: Find the child to traverse
            var childIndex = node.Keys.BinarySearch(record.Key);
            if (childIndex < 0) childIndex = ~childIndex;

            var childPageID = node.ChildPointers[childIndex];
            var childNode = _memoryManagerService.DeserializeBTreeNodePage(
                File.ReadAllBytes($"Disk/{childPageID}.bin")
            );

            // Recursively insert into the child
            var splitResult = InsertIntoNode(childNode, record, pageID, offset);

            if (splitResult != null)
            {
                // Insert the promoted key and pointer into the current node
                node.Keys.Insert(childIndex, splitResult.PromotedKey);
                node.ChildPointers.Insert(childIndex + 1, splitResult.NewNode.PageID);

                // Save the updated node to disk
                _memoryManagerService.SavePageToFile(
                    _memoryManagerService.SerializeBTreeNodePage(node),
                    node.PageID
                );

                // Check if the current node is full and needs splitting
                if (node.Keys.Count > 2 * Degree)
                {
                    return SplitNode(node);
                }
            }
            // No split required
        }

        return null; // No split required
    }

    private SplitResult SplitNode(BTreeNodePage? node)
    {
        var midIndex = node!.Keys.Count / 2;
        var promotedKey = node.Keys[midIndex];
        var promotedKeyAddress = node.Addresses[midIndex];
        node.IsRoot = false;

        // Create a new node for the right half
        var newNode = new BTreeNodePage(Guid.NewGuid(), node.IsLeaf, isRoot: false, Degree);
        newNode.Keys.AddRange(node.Keys.GetRange(midIndex + 1, node.Keys.Count - midIndex - 1));
        node.Keys.RemoveRange(midIndex, node.Keys.Count - midIndex);

        if (node.IsLeaf)
        {
            newNode.Addresses.AddRange(node.Addresses.GetRange(midIndex + 1, node.Addresses.Count - midIndex - 1));
            node.Addresses.RemoveRange(midIndex + 1, node.Addresses.Count - midIndex - 1);
        }
        else
        {
            newNode.ChildPointers.AddRange(node.ChildPointers.GetRange(midIndex + 1, node.ChildPointers.Count - midIndex - 1));
            node.ChildPointers.RemoveRange(midIndex + 1, node.ChildPointers.Count - midIndex - 1);
        }



        // Save the updated nodes to disk
        _memoryManagerService.SavePageToFile(
            _memoryManagerService.SerializeBTreeNodePage(node),
            node.PageID
        );

        _memoryManagerService.SavePageToFile(
            _memoryManagerService.SerializeBTreeNodePage(newNode),
            newNode.PageID
        );

        return new SplitResult(promotedKey, promotedKeyAddress, newNode);
    }

    public Record FindRecord(Record record)
    {
        throw new NotImplementedException();
    }

    public void PrintAllRecords()
    {
        throw new NotImplementedException();
    }

    public void PrintRecordsInOrder()
    {
        throw new NotImplementedException();
    }

    public void PrintRecordsByPage()
    {
        throw new NotImplementedException();
    }
}