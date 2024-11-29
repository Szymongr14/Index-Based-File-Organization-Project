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
            rootNode = InitializeNewRoot(record, pageID, offset);
            return;
        }

        // Proceed with iterative insertion logic
        var splitResult = InsertIntoNode(rootNode, record, pageID, offset);

        // Handle root splitting iteratively
        while (splitResult != null)
        {
            rootNode = HandleRootSplit(rootNode, splitResult);
            splitResult = rootNode.Keys.Count > 2 * Degree ? SplitNode(rootNode) : null;
        }
    }

    public Record FindRecord(Record record)
    {
        throw new NotImplementedException();
    }

    private BTreeNodePage InitializeNewRoot(Record record, Guid pageID, uint offset)
    {
        var rootNode = new BTreeNodePage(Guid.NewGuid(), isLeaf: true, isRoot: true, Degree);
        rootNode.Keys.Add(record.Key);
        rootNode.Addresses.Add((pageID, offset));

        // Save the root node to disk
        _memoryManagerService.SavePageToFile(
            _memoryManagerService.SerializeBTreeNodePage(rootNode),
            rootNode.PageID
        );

        // Update the root in memory
        _memoryManagerService.SetRootPage(rootNode);

        return rootNode;
    }

    private BTreeNodePage HandleRootSplit(BTreeNodePage rootNode, SplitResult splitResult)
    {
        // Create a new root
        var newRoot = new BTreeNodePage(Guid.NewGuid(), isLeaf: false, isRoot: true, Degree);
        newRoot.Keys.Add(splitResult.PromotedKey); // The promoted key becomes the root's key
        newRoot.Addresses.Add(splitResult.PromotedKeyAddress); // Add promoted key address
        newRoot.ChildPointers.Add(rootNode.PageID); // Old root becomes the left child
        newRoot.ChildPointers.Add(splitResult.NewNode.PageID); // New sibling becomes the right child

        // Save the new root to disk
        _memoryManagerService.SavePageToFile(
            _memoryManagerService.SerializeBTreeNodePage(newRoot),
            newRoot.PageID
        );

        // Update the root in memory
        _memoryManagerService.SetRootPage(newRoot);

        return newRoot;
    }

    private SplitResult? InsertIntoNode(BTreeNodePage rootNode, Record record, Guid pageID, uint offset)
    {
        // Stack to track the path to the leaf
        var stack = new Stack<(BTreeNodePage Node, int ChildIndex)>();

        var currentNode = rootNode;

        while (!currentNode.IsLeaf)
        {
            // Find the child index
            int childIndex = currentNode.Keys.BinarySearch(record.Key);
            if (childIndex < 0) childIndex = ~childIndex;

            // Push the current node and index to the stack
            stack.Push((currentNode, childIndex));

            // Load the child node
            var childPageID = currentNode.ChildPointers[childIndex];
            currentNode = _memoryManagerService.DeserializeBTreeNodePage(
                File.ReadAllBytes($"Disk/{childPageID}.bin")
            );
        }

        // Insert into the leaf node
        int insertIndex = currentNode.Keys.BinarySearch(record.Key);
        if (insertIndex < 0) insertIndex = ~insertIndex;

        currentNode.Keys.Insert(insertIndex, record.Key);
        currentNode.Addresses.Insert(insertIndex, (pageID, offset));

        // Save the updated leaf node to disk
        _memoryManagerService.SavePageToFile(
            _memoryManagerService.SerializeBTreeNodePage(currentNode),
            currentNode.PageID
        );

        // Handle splits iteratively
        while (currentNode.Keys.Count > 2 * Degree)
        {
            var splitResult = SplitNode(currentNode);

            // If the stack is empty, a root split will occur
            if (stack.Count == 0)
            {
                return splitResult;
            }

            // Update the parent node
            var (parentNode, childIndex) = stack.Pop();
            parentNode.Keys.Insert(childIndex, splitResult.PromotedKey);
            parentNode.Addresses.Insert(childIndex, splitResult.PromotedKeyAddress);
            parentNode.ChildPointers.Insert(childIndex + 1, splitResult.NewNode.PageID);

            // Save the updated parent node to disk
            _memoryManagerService.SavePageToFile(
                _memoryManagerService.SerializeBTreeNodePage(parentNode),
                parentNode.PageID
            );

            currentNode = parentNode; // Continue checking upwards
        }

        return null; // No split propagated to the root
    }

    private SplitResult SplitNode(BTreeNodePage node)
    {
        var midIndex = node.Keys.Count / 2;
        var promotedKey = node.Keys[midIndex];
        var promotedKeyAddress = node.Addresses[midIndex];
        node.IsRoot = false;

        // Create a new node for the right half
        var newNode = new BTreeNodePage(Guid.NewGuid(), node.IsLeaf, isRoot: false, Degree);
        newNode.Keys.AddRange(node.Keys.GetRange(midIndex + 1, node.Keys.Count - midIndex - 1));
        node.Keys.RemoveRange(midIndex, node.Keys.Count - midIndex);
        newNode.Addresses.AddRange(node.Addresses.GetRange(midIndex + 1, node.Addresses.Count - midIndex - 1));
        node.Addresses.RemoveRange(midIndex, node.Addresses.Count - midIndex);

        if (!node.IsLeaf){
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

    public void PrintBTree()
    {
        var rootNode = _memoryManagerService.GetRootPage();

        if (rootNode == null)
        {
            Console.WriteLine("The B-Tree is empty.");
            return;
        }

        // Stack for iterative traversal
        var stack = new Stack<(Guid PageID, int Level)>();
        stack.Push((rootNode.PageID, 0));

        while (stack.Count > 0)
        {
            var (pageID, level) = stack.Pop();

            // Load the node from disk
            var node = _memoryManagerService.DeserializeBTreeNodePage(File.ReadAllBytes($"Disk/{pageID}.bin"));

            // Print the current node
            Console.WriteLine($"{new string(' ', level * 2)}Level {level}, Page {pageID}: Keys = [{string.Join(", ", node.Keys)}]");

            // Add child nodes to the stack if not a leaf
            if (!node.IsLeaf)
            {
                for (int i = node.ChildPointers.Count - 1; i >= 0; i--)
                {
                    stack.Push((node.ChildPointers[i], level + 1));
                }
            }
        }
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
