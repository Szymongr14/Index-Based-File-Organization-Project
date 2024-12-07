using BTreeIndexedFileSimulator.Interfaces;
using BTreeIndexedFileSimulator.Models;
using MemoryPageAccessSimulator.Enums;
using MemoryPageAccessSimulator.Interfaces;
using MemoryPageAccessSimulator.Models;
using MemoryPageAccessSimulator.Utilities;

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

    public (Guid pageId, uint offset) FindAddressOfKey(uint key)
    {
        var currentNode = _memoryManagerService.GetRootPage();
        if (currentNode == null) return (Guid.Empty, 0);
    
        while (!currentNode!.IsLeaf)
        {
            var childIndex = currentNode.Keys.BinarySearch(key);
            if (childIndex >= 0)
            {
                return currentNode.Addresses[childIndex];
            }
            childIndex = ~childIndex;
            var childPageID = currentNode.ChildPointers[childIndex];
            currentNode = _memoryManagerService.GetBTreePageFromDisk(childPageID);
        }

        var leafIndex = currentNode.Keys.BinarySearch(key);
        if (leafIndex >= 0)
        {
            return currentNode.Addresses[leafIndex];
        }

        return (Guid.Empty, 0);
    }

    private BTreeNodePage InitializeNewRoot(Record record, Guid pageID, uint offset)
    {
        var rootNode = new BTreeNodePage(Guid.NewGuid(), isLeaf: true, isRoot: true);
        rootNode.Keys.Add(record.Key);
        rootNode.Addresses.Add((pageID, offset));

        // Save the root node to disk
        _memoryManagerService.SavePageToFile(
            BTreeNodePageSerializer.Serialize(rootNode),
            rootNode.PageID,
            PageType.BTreeNode
        );

        // Update the root in memory
        _memoryManagerService.SetRootPage(rootNode);

        return rootNode;
    }

    private BTreeNodePage HandleRootSplit(BTreeNodePage rootNode, SplitResult splitResult)
    {
        // Create a new root
        var newRoot = new BTreeNodePage(Guid.NewGuid(), isLeaf: false, isRoot: true);
        newRoot.Keys.Add(splitResult.PromotedKey); // The promoted key becomes the root's key
        newRoot.Addresses.Add(splitResult.PromotedKeyAddress); // Add promoted key address
        newRoot.ChildPointers.Add(rootNode.PageID); // Old root becomes the left child
        newRoot.ChildPointers.Add(splitResult.NewNode.PageID); // New sibling becomes the right child

        // Save the new root to disk
        _memoryManagerService.SavePageToFile(
            BTreeNodePageSerializer.Serialize(newRoot),
            newRoot.PageID,
            PageType.BTreeNode
        );

        // Update the root in memory
        _memoryManagerService.SetRootPage(newRoot);

        return newRoot;
    }

    private SplitResult? InsertIntoNode(BTreeNodePage rootNode, Record record, Guid pageID, uint offset)
    {
        // Stack to track the path to the leaf
        var parentsStack = new Stack<(BTreeNodePage Node, int ChildIndex)>();

        var currentNode = rootNode;

        while (!currentNode.IsLeaf)
        {
            // Find the child index
            var childIndex = currentNode.Keys.BinarySearch(record.Key);
            if (childIndex < 0) childIndex = ~childIndex;

            // Push the current node and index to the stack
            parentsStack.Push((currentNode, childIndex));

            // Load the child node
            var childPageID = currentNode.ChildPointers[childIndex];
            currentNode = _memoryManagerService.GetBTreePageFromDisk(childPageID);
        }

        // Insert into the leaf node
        var insertIndex = currentNode.Keys.BinarySearch(record.Key);
        if (insertIndex < 0) insertIndex = ~insertIndex;

        currentNode.Keys.Insert(insertIndex, record.Key);
        currentNode.Addresses.Insert(insertIndex, (pageID, offset));

        // Save the updated leaf node to disk
        _memoryManagerService.SavePageToFile(
            BTreeNodePageSerializer.Serialize(currentNode),
            currentNode.PageID,
            PageType.BTreeNode
        );

        // Handle splits iteratively
        while (currentNode.Keys.Count > 2 * Degree)
        {
            if (TryCompensateNode(currentNode, parentsStack))
            {
                break; // Stop processing further if compensation succeeded
            }
            
            var splitResult = SplitNode(currentNode);

            // If the stack is empty, a root split will occur
            if (parentsStack.Count == 0)
            {
                return splitResult;
            }

            // Update the parent node
            var (parentNode, childIndexInParent) = parentsStack.Pop();
            parentNode.Keys.Insert(childIndexInParent, splitResult.PromotedKey);
            parentNode.Addresses.Insert(childIndexInParent, splitResult.PromotedKeyAddress);
            parentNode.ChildPointers.Insert(childIndexInParent + 1, splitResult.NewNode.PageID);

            // Save the updated parent node to disk
            _memoryManagerService.SavePageToFile(
                BTreeNodePageSerializer.Serialize(parentNode),
                parentNode.PageID,
                PageType.BTreeNode
            );

            currentNode = parentNode; // Continue checking upwards
        }

        return null; // No split propagated to the root
    }
    
    private bool TryCompensateNode(BTreeNodePage node, Stack<(BTreeNodePage Node, int ChildIndex)> parentsStack)
    {
        if (!parentsStack.TryPeek(out var parentAndIndex))
        {
            return false;
        }
        
        var (parentNode, childIndexInParent) = parentAndIndex;
        
        if (childIndexInParent > 0)
        {
            var leftSiblingPageID = parentNode.ChildPointers[childIndexInParent - 1];
            var leftSibling = _memoryManagerService.GetBTreePageFromDisk(leftSiblingPageID);

            if (leftSibling.Keys.Count < 2 * Degree)
            {
                // Perform compensation with the left sibling
                PerformLeftCompensation(node, leftSibling, parentNode, childIndexInParent);
                return true;
            }
        }
        
        if (childIndexInParent < parentNode.ChildPointers.Count - 1)
        {
            var rightSiblingPageID = parentNode.ChildPointers[childIndexInParent + 1];
            var rightSibling = _memoryManagerService.GetBTreePageFromDisk(rightSiblingPageID);

            if (rightSibling.Keys.Count < 2 * Degree)
            {
                // Perform compensation with the right sibling
                PerformRightCompensation(node, rightSibling, parentNode, childIndexInParent);
                return true;
            }
        }

        return false;
        
    }
    
    private void PerformLeftCompensation(
        BTreeNodePage currentNode, 
        BTreeNodePage leftSibling, 
        BTreeNodePage parentNode, 
        int childIndexInParent)
    {
        // Take the key from the parent and move it to the left sibling
        var parentKey = parentNode.Keys[childIndexInParent - 1];
        leftSibling.Keys.Add(parentKey);
        leftSibling.Addresses.Add(parentNode.Addresses[childIndexInParent - 1]);

        // Move the smallest key from the current node to the parent
        var movedKey = currentNode.Keys.First();
        parentNode.Keys[childIndexInParent - 1] = movedKey;
        parentNode.Addresses[childIndexInParent - 1] = currentNode.Addresses.First();

        // Remove the moved key and address from the current node
        currentNode.Keys.RemoveAt(0);
        currentNode.Addresses.RemoveAt(0);

        // If it's an internal node, move the appropriate child pointer
        if (!currentNode.IsLeaf)
        {
            // Move the first child pointer from the current node to the left sibling
            var movedChildPointer = currentNode.ChildPointers.First();
            leftSibling.ChildPointers.Add(movedChildPointer);
            currentNode.ChildPointers.RemoveAt(0);
        }

        // Save changes to disk
        _memoryManagerService.SavePageToFile(BTreeNodePageSerializer.Serialize(leftSibling), leftSibling.PageID, PageType.BTreeNode);
        _memoryManagerService.SavePageToFile(BTreeNodePageSerializer.Serialize(currentNode), currentNode.PageID, PageType.BTreeNode);
        _memoryManagerService.SavePageToFile(BTreeNodePageSerializer.Serialize(parentNode), parentNode.PageID, PageType.BTreeNode);
    }
    
    private void PerformRightCompensation(
        BTreeNodePage currentNode, 
        BTreeNodePage rightSibling, 
        BTreeNodePage parentNode, 
        int childIndexInParent)
    {
        // Take the key from the parent and move it to the right sibling
        var parentKey = parentNode.Keys[childIndexInParent + 1];
        rightSibling.Keys.Add(parentKey);
        rightSibling.Addresses.Add(parentNode.Addresses[childIndexInParent + 1]);

        // Move the smallest key from the current node to the parent
        var movedKey = currentNode.Keys.Last();
        parentNode.Keys[childIndexInParent + 1] = movedKey;
        parentNode.Addresses[childIndexInParent + 1] = currentNode.Addresses.Last();

        // Remove the moved key and address from the current node
        currentNode.Keys.RemoveAt(currentNode.Keys.Count-1);
        currentNode.Addresses.RemoveAt(currentNode.Addresses.Count-1);

        // If it's an internal node, move the appropriate child pointer
        if (!currentNode.IsLeaf)
        {
            // Move the first child pointer from the current node to the left sibling
            var movedChildPointer = currentNode.ChildPointers.Last();
            rightSibling.ChildPointers.Add(movedChildPointer);
            currentNode.ChildPointers.RemoveAt(currentNode.ChildPointers.Count-1);
        }

        // Save changes to disk
        _memoryManagerService.SavePageToFile(BTreeNodePageSerializer.Serialize(rightSibling), rightSibling.PageID, PageType.BTreeNode);
        _memoryManagerService.SavePageToFile(BTreeNodePageSerializer.Serialize(currentNode), currentNode.PageID, PageType.BTreeNode);
        _memoryManagerService.SavePageToFile(BTreeNodePageSerializer.Serialize(parentNode), parentNode.PageID, PageType.BTreeNode);
    }
    
    private SplitResult SplitNode(BTreeNodePage node)
    {
        var midIndex = node.Keys.Count / 2;
        var promotedKey = node.Keys[midIndex];
        var promotedKeyAddress = node.Addresses[midIndex];
        node.IsRoot = false;

        // Create a new node for the right half
        var newNode = new BTreeNodePage(Guid.NewGuid(), node.IsLeaf, isRoot: false);
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
            BTreeNodePageSerializer.Serialize(node),
            node.PageID,
            PageType.BTreeNode
        );

        _memoryManagerService.SavePageToFile(
            BTreeNodePageSerializer.Serialize(newNode),
            newNode.PageID,
            PageType.BTreeNode
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
            var node = _memoryManagerService.GetBTreePageFromDisk(pageID);

            // Print the current node
            Console.WriteLine($"{new string(' ', level * 2)}Level {level}, Page {pageID}: Keys = [{string.Join(", ", node.Keys)}]");

            // Add child nodes to the stack if not a leaf
            if (!node.IsLeaf)
            {
                for (var i = node.ChildPointers.Count - 1; i >= 0; i--)
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
