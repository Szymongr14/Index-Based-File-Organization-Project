# B-Tree Indexed File Simulator

## Overview
This repository implements a B-Tree-based indexed file system simulation. It supports operations such as insertion, deletion, and search for records while optimizing disk space usage and I/O operations. The project includes mechanisms like page caching, space optimization, and automatic reorganization to improve performance.

## Report
The detailed report for this project can be found [here](Indexed_File_Organization_Simulator_Report.pdf).

## Features
- **B-Tree Implementation**: Supports dynamic reorganization through node splitting, merging, and compensation.
- **Disk Space Optimization**: Reuses freed space during record deletion.
- **Page Caching**: Implements an LRU-based caching mechanism for frequently accessed pages.
- **Configurable Parameters**: Allows tuning of tree degree, page size, and RAM size.

## Usage
### Running the Simulator
1. Configure `AppSettings.json` for parameters like tree degree, page size, and RAM size.
2. Move to BTreeIndexedFileSimulator:
   ```bash
   cd BTreeIndexedFileSimulator
   ```
3. Build and run the project:
   ```bash
   dotnet run
   ```
4. Provide instruction files to test operations in `Data/` directory (e.g., `Data/instructions.txt`).

### Instruction File Format
- **Insert**: `insert x y key` (where `x`, `y` are record values, and `key` is the unique identifier).
- **Delete**: `delete key` (where `key` is the unique identifier).
- **Search**: `find key` (where `key` is the unique identifier).

### Example Instruction File
```
insert 10 20 1
insert 15 25 2
delete 1
find 2
```