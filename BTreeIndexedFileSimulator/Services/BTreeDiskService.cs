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
            InitializeNewRoot(record, pageID, offset);
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

    private void InitializeNewRoot(Record record, Guid pageID, uint offset)
    {
        var rootNode = new BTreeNodePage(Guid.NewGuid(), isLeaf: true, isRoot: true);
        rootNode.Keys.Add(record.Key);
        rootNode.Addresses.Add((pageID, offset));

        _memoryManagerService.SavePageToFile(
            BTreeNodePageSerializer.Serialize(rootNode),
            rootNode.PageID,
            PageType.BTreeNode
        );

        _memoryManagerService.SetRootPage(rootNode);
    }

    private BTreeNodePage HandleRootSplit(BTreeNodePage rootNode, SplitResult splitResult)
    {
        // Create a new root
        var newRoot = new BTreeNodePage(Guid.NewGuid(), isLeaf: false, isRoot: true);
        newRoot.Keys.Add(splitResult.PromotedKey); // The promoted key becomes the root's key
        newRoot.Addresses.Add(splitResult.PromotedKeyAddress); // Add promoted key address
        newRoot.ChildPointers.Add(rootNode.PageID); // Old root becomes the left child
        newRoot.ChildPointers.Add(splitResult.NewNode.PageID); // New sibling becomes the right child

        _memoryManagerService.SavePageToFile(
            BTreeNodePageSerializer.Serialize(newRoot),
            newRoot.PageID,
            PageType.BTreeNode
        );
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
            if (TryCompensateNodeForInsertion(currentNode, parentsStack) && _appSettings.EnableNodeCompensation)
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
    
    private bool TryCompensateNodeForInsertion(BTreeNodePage node, Stack<(BTreeNodePage Node, int ChildIndex)> parentsStack)
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
                PerformLeftCompensationForInsertion(node, leftSibling, parentNode, childIndexInParent);
                return true;
            }
        }
        
        if (childIndexInParent < parentNode.ChildPointers.Count - 1)
        {
            var rightSiblingPageID = parentNode.ChildPointers[childIndexInParent + 1];
            var rightSibling = _memoryManagerService.GetBTreePageFromDisk(rightSiblingPageID);

            if (rightSibling.Keys.Count < 2 * Degree)
            {
                PerformRightCompensationForInsertion(node, rightSibling, parentNode, childIndexInParent);
                return true;
            }
        }

        return false;
        
    }
    
    private void PerformLeftCompensationForInsertion(
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

        SaveNodesToDiskAfterCompensation(currentNode, leftSibling, parentNode);
    }
    
    private void PerformRightCompensationForInsertion(
        BTreeNodePage currentNode,
        BTreeNodePage rightSibling,
        BTreeNodePage parentNode,
        int childIndexInParent)
    {
        uint parentKey;
        (Guid pageID, uint offset) parentAddress;

        // Take the key from the parent and move it to the right sibling (as the first element)
        parentKey = parentNode.Keys[childIndexInParent];
        parentAddress = parentNode.Addresses[childIndexInParent];
        rightSibling.Keys.Insert(0, parentKey); // Insert at the beginning
        rightSibling.Addresses.Insert(0, parentAddress); // Insert at the beginning

        // Move the largest key from the current node to the parent
        var movedKey = currentNode.Keys.Last();
        parentNode.Keys[childIndexInParent] = movedKey;
        parentNode.Addresses[childIndexInParent] = currentNode.Addresses.Last();

        // Remove the moved key and address from the current node
        currentNode.Keys.RemoveAt(currentNode.Keys.Count - 1);
        currentNode.Addresses.RemoveAt(currentNode.Addresses.Count - 1);

        // If it's an internal node, move the appropriate child pointer
        if (!currentNode.IsLeaf)
        {
            // Move the last child pointer from the current node to the beginning of the right sibling
            var movedChildPointer = currentNode.ChildPointers.Last();
            rightSibling.ChildPointers.Insert(0, movedChildPointer); // Insert at the beginning
            currentNode.ChildPointers.RemoveAt(currentNode.ChildPointers.Count - 1);
        }

        SaveNodesToDiskAfterCompensation(currentNode, rightSibling, parentNode);
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

        var stack = new Stack<(BTreeNodePage Node, int Level)>();
        stack.Push((rootNode, 0));

        while (stack.Count > 0)
        {
            var (node, level) = stack.Pop();

            Console.WriteLine($"{new string(' ', level * 2)}Level {level}, Page {node.PageID}: Keys = [{string.Join(", ", node.Keys)}]");

            if (!node.IsLeaf)
            {
                for (var i = node.ChildPointers.Count - 1; i >= 0; i--)
                {
                    var childNode = _memoryManagerService.GetBTreePageFromDisk(node.ChildPointers[i]); // Load child nodes from disk
                    stack.Push((childNode, level + 1));
                }
            }
        }
    }

    public bool DeleteRecord(uint key)
    {
        var parentsStack = new Stack<(BTreeNodePage Node, int ChildIndex)>();

        var currentNode = _memoryManagerService.GetRootPage();
        if (currentNode == null) return false;

        while (!currentNode.IsLeaf)
        {
            var childIndex = currentNode.Keys.BinarySearch(key);
            if (childIndex >= 0) // Key found in internal node
            {
                var nodeWithSuccessorKey = IterateToNodeWithSuccessorKey(currentNode, parentsStack, childIndex);
            
                var successorKey = nodeWithSuccessorKey.Keys.First();
                var successorKeyAddress = nodeWithSuccessorKey.Addresses.First();
                nodeWithSuccessorKey.Keys.RemoveAt(0);
                nodeWithSuccessorKey.Addresses.RemoveAt(0);
                currentNode.Keys[childIndex] = successorKey;
                currentNode.Addresses[childIndex] = successorKeyAddress;
                
                _memoryManagerService.SavePageToFile(
                    BTreeNodePageSerializer.Serialize(currentNode),
                    currentNode.PageID,
                    PageType.BTreeNode
                );
                
                _memoryManagerService.SavePageToFile(
                    BTreeNodePageSerializer.Serialize(nodeWithSuccessorKey),
                    nodeWithSuccessorKey.PageID,
                    PageType.BTreeNode
                );
                _memoryManagerService.AddFreeSpaceForRecord(successorKeyAddress);

                if (nodeWithSuccessorKey.Keys.Count >= Degree) return true;

                if (TryCompensateNodeForDeletion(nodeWithSuccessorKey, parentsStack)) return true;

                MergeNodes(nodeWithSuccessorKey, parentsStack);
                return true;
            }
            childIndex = ~childIndex;
            var childPageID = currentNode.ChildPointers[childIndex];
            parentsStack.Push((currentNode, childIndex));
            currentNode = _memoryManagerService.GetBTreePageFromDisk(childPageID);
        }

        var leafIndex = currentNode.Keys.BinarySearch(key);
        if (leafIndex >= 0) // Key found in leaf node
        {
            _memoryManagerService.AddFreeSpaceForRecord(currentNode.Addresses[leafIndex]);
            currentNode.Keys.RemoveAt(leafIndex);
            currentNode.Addresses.RemoveAt(leafIndex);

            if (currentNode.Keys.Count < Degree)
            {
                if (TryCompensateNodeForDeletion(currentNode, parentsStack))
                {
                    return true;
                }

                MergeNodes(currentNode, parentsStack);
            }

            return true;
        }

        return false; // Key not found
    }

    private void MergeNodes(BTreeNodePage node, Stack<(BTreeNodePage Node, int ChildIndex)> parentsStack)
    {
        while (true)
        {
            if (!parentsStack.TryPop(out var parentAndIndex))
            {
                if (node.Keys.Count != 0 || node.IsLeaf) return;

                // Update root if the current root becomes empty
                var newRootPageID = node.ChildPointers.First();
                var newRoot = _memoryManagerService.GetBTreePageFromDisk(newRootPageID);
                newRoot.IsRoot = true;

                _memoryManagerService.SavePageToFile(BTreeNodePageSerializer.Serialize(newRoot), newRoot.PageID, PageType.BTreeNode);
                _memoryManagerService.SetRootPage(newRoot);

                // Delete the old root node
                _memoryManagerService.DeletePageFromDisk(node.PageID);
                return;
            }

            var (parentNode, childIndexInParent) = parentAndIndex;

            if (childIndexInParent > 0)
            {
                // Merge with left sibling
                var leftSiblingPageID = parentNode.ChildPointers[childIndexInParent - 1];
                var leftSibling = _memoryManagerService.GetBTreePageFromDisk(leftSiblingPageID);

                // Move a parent key into left sibling
                leftSibling.Keys.Add(parentNode.Keys[childIndexInParent - 1]);
                leftSibling.Addresses.Add(parentNode.Addresses[childIndexInParent - 1]);

                // Move all keys and children from the current node to left sibling
                leftSibling.Keys.AddRange(node.Keys);
                leftSibling.Addresses.AddRange(node.Addresses);
                if (!node.IsLeaf)
                {
                    leftSibling.ChildPointers.AddRange(node.ChildPointers);
                }

                // Remove the merged key from the parent
                parentNode.Keys.RemoveAt(childIndexInParent - 1);
                parentNode.Addresses.RemoveAt(childIndexInParent - 1);
                parentNode.ChildPointers.RemoveAt(childIndexInParent);

                // Save updated nodes to disk
                _memoryManagerService.SavePageToFile(BTreeNodePageSerializer.Serialize(leftSibling), leftSibling.PageID, PageType.BTreeNode);
                _memoryManagerService.SavePageToFile(BTreeNodePageSerializer.Serialize(parentNode), parentNode.PageID, PageType.BTreeNode);

                // Delete the current (merged) node from the disk
                _memoryManagerService.DeletePageFromDisk(node.PageID);

                if (parentNode.Keys.Count < Degree)
                {
                    node = parentNode;
                    continue;
                }
            }
            else if (childIndexInParent < parentNode.ChildPointers.Count - 1)
            {
                // Merge with right sibling
                var rightSiblingPageID = parentNode.ChildPointers[childIndexInParent + 1];
                var rightSibling = _memoryManagerService.GetBTreePageFromDisk(rightSiblingPageID);

                // Move a parent key into the current node
                node.Keys.Add(parentNode.Keys[childIndexInParent]);
                node.Addresses.Add(parentNode.Addresses[childIndexInParent]);

                // Move all keys and children from right sibling to the current node
                node.Keys.AddRange(rightSibling.Keys);
                node.Addresses.AddRange(rightSibling.Addresses);
                if (!rightSibling.IsLeaf)
                {
                    node.ChildPointers.AddRange(rightSibling.ChildPointers);
                }

                // Remove the merged key from the parent
                parentNode.Keys.RemoveAt(childIndexInParent);
                parentNode.Addresses.RemoveAt(childIndexInParent);
                parentNode.ChildPointers.RemoveAt(childIndexInParent + 1);

                // Save updated nodes to disk
                _memoryManagerService.SavePageToFile(BTreeNodePageSerializer.Serialize(node), node.PageID, PageType.BTreeNode);
                _memoryManagerService.SavePageToFile(BTreeNodePageSerializer.Serialize(parentNode), parentNode.PageID, PageType.BTreeNode);

                // Delete the right sibling (merged) node from the disk
                _memoryManagerService.DeletePageFromDisk(rightSibling.PageID);

                if (parentNode.Keys.Count < Degree)
                {
                    node = parentNode;
                    continue;
                }
            }

            break;
        }
    }



    private BTreeNodePage IterateToNodeWithSuccessorKey(BTreeNodePage node, Stack<(BTreeNodePage Node, int ChildIndex)> parentsStack, int childIndex)
    {
        // Push the initial node and child index to the stack
        parentsStack.Push((node, childIndex + 1)); // Increment to indicate we are moving to the right subtree

        // Move to the right subtree
        var childPageID = node.ChildPointers[childIndex + 1];
        node = _memoryManagerService.GetBTreePageFromDisk(childPageID);

        // Traverse to the leftmost child in the subtree
        while (!node.IsLeaf)
        {
            parentsStack.Push((node, 0));
            childPageID = node.ChildPointers[0];
            node = _memoryManagerService.GetBTreePageFromDisk(childPageID);
        }

        return node;
    }
    
    private bool TryCompensateNodeForDeletion(BTreeNodePage node, Stack<(BTreeNodePage Node, int ChildIndex)> parentsStack)
    {
        if (!parentsStack.TryPeek(out var parentAndIndex))
        {
            return false; // No parent means any compensation possible
        }

        var (parentNode, childIndexInParent) = parentAndIndex;

        // Try borrowing from the left sibling
        if (childIndexInParent > 0)
        {
            var leftSiblingPageID = parentNode.ChildPointers[childIndexInParent - 1];
            var leftSibling = _memoryManagerService.GetBTreePageFromDisk(leftSiblingPageID);

            if (leftSibling.Keys.Count > Degree) // Can borrow keys from left sibling
            {
                BorrowFromLeftSibling(node, leftSibling, parentNode, childIndexInParent);
                return true;
            }
        }

        // Try borrowing from the right sibling
        if (childIndexInParent < parentNode.ChildPointers.Count - 1)
        {
            var rightSiblingPageID = parentNode.ChildPointers[childIndexInParent + 1];
            var rightSibling = _memoryManagerService.GetBTreePageFromDisk(rightSiblingPageID);

            if (rightSibling.Keys.Count > Degree) // Can borrow keys from the right sibling
            {
                BorrowFromRightSibling(node, rightSibling, parentNode, childIndexInParent);
                return true;
            }
        }

        return false; // No compensation possible
    }
    
    private void BorrowFromRightSibling(
        BTreeNodePage currentNode, 
        BTreeNodePage rightSibling, 
        BTreeNodePage parentNode, 
        int childIndexInParent)
    {
        MoveParentKeyToNode(currentNode, parentNode, childIndexInParent, fromRight: true);

        MoveSiblingKeyToParent(rightSibling, parentNode, childIndexInParent, fromRight: true);

        if (!rightSibling.IsLeaf)
        {
            MoveChildPointer(currentNode, rightSibling, fromRight: true);
        }

        SaveNodesToDiskAfterCompensation(currentNode, rightSibling, parentNode);
    }

    private void BorrowFromLeftSibling(
        BTreeNodePage currentNode, 
        BTreeNodePage leftSibling, 
        BTreeNodePage parentNode, 
        int childIndexInParent)
    {
        MoveParentKeyToNode(currentNode, parentNode, childIndexInParent - 1, fromRight: false);

        MoveSiblingKeyToParent(leftSibling, parentNode, childIndexInParent - 1, fromRight: false);

        if (!leftSibling.IsLeaf)
        {
            MoveChildPointer(currentNode, leftSibling, fromRight: false);
        }

        SaveNodesToDiskAfterCompensation(currentNode, leftSibling, parentNode);
    }

    private static void MoveParentKeyToNode(BTreeNodePage currentNode, BTreeNodePage parentNode, int parentIndex, bool fromRight)
    {
        var parentKey = parentNode.Keys[parentIndex];
        var parentAddress = parentNode.Addresses[parentIndex];

        if (fromRight)
        {
            currentNode.Keys.Add(parentKey);
            currentNode.Addresses.Add(parentAddress);
        }
        else
        {
            currentNode.Keys.Insert(0, parentKey);
            currentNode.Addresses.Insert(0, parentAddress);
        }
    }
    
    private static void MoveSiblingKeyToParent(BTreeNodePage siblingNode, BTreeNodePage parentNode, int parentIndex, bool fromRight)
    {
        if (fromRight)
        {
            var borrowedKey = siblingNode.Keys.First();
            var borrowedAddress = siblingNode.Addresses.First();
            parentNode.Keys[parentIndex] = borrowedKey;
            parentNode.Addresses[parentIndex] = borrowedAddress;

            siblingNode.Keys.RemoveAt(0);
            siblingNode.Addresses.RemoveAt(0);
        }
        else
        {
            var borrowedKey = siblingNode.Keys.Last();
            var borrowedAddress = siblingNode.Addresses.Last();
            parentNode.Keys[parentIndex] = borrowedKey;
            parentNode.Addresses[parentIndex] = borrowedAddress;

            siblingNode.Keys.RemoveAt(siblingNode.Keys.Count - 1);
            siblingNode.Addresses.RemoveAt(siblingNode.Addresses.Count - 1);
        }
    }
    
    private static void MoveChildPointer(BTreeNodePage currentNode, BTreeNodePage siblingNode, bool fromRight)
    {
        if (fromRight)
        {
            var borrowedChildPointer = siblingNode.ChildPointers.First();
            currentNode.ChildPointers.Add(borrowedChildPointer);
            siblingNode.ChildPointers.RemoveAt(0);
        }
        else
        {
            var borrowedChildPointer = siblingNode.ChildPointers.Last();
            currentNode.ChildPointers.Insert(0, borrowedChildPointer);
            siblingNode.ChildPointers.RemoveAt(siblingNode.ChildPointers.Count - 1);
        }
    }
    
    private void SaveNodesToDiskAfterCompensation(BTreeNodePage currentNode, BTreeNodePage siblingNode, BTreeNodePage parentNode)
    {
        _memoryManagerService.SavePageToFile(BTreeNodePageSerializer.Serialize(currentNode), currentNode.PageID, PageType.BTreeNode);
        _memoryManagerService.SavePageToFile(BTreeNodePageSerializer.Serialize(siblingNode), siblingNode.PageID, PageType.BTreeNode);
        _memoryManagerService.SavePageToFile(BTreeNodePageSerializer.Serialize(parentNode), parentNode.PageID, PageType.BTreeNode);

        if (parentNode.IsRoot)
        {
            _memoryManagerService.SetRootPage(parentNode);
        }
    }

}
