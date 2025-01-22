# FilesysDB

A simple class that provides an API similar to System.IO.File class, but all the data is stored in a single SQLite database file. 
Enjoy the simplicity of files with the ACID robustness of SQLite.
Also, file searches are performed orders of magnitude faster than a typical OS file system (At least on Windows).

```
static void Main()
{
    using IFileSysDB fileSys = new FileSysDB();
    fileSys.CreateOrOpen(@"c:\temp\filesysdb.db");
    foreach (var file in fileSys.GetFiles(@"", wildcard: "*.*"))
    {
        fileSys.Delete(file);
    }
    // Write a text file
    fileSys.WriteAllText(@"root\folder1\file.txt", @"root\folder1\file.txt");
    fileSys.WriteAllText(@"root\folder1\file.txt", @"root\folder1\file.txt again");
    fileSys.WriteAllText(@"root\folder1\file1.txt", @"root\folder1\file1.txt");
    fileSys.WriteAllText(@"root\folder1\file2.txt", @"root\folder1\file2.txt");
    fileSys.WriteAllText(@"root\folder2\file.txt", @"root\folder2\file.txt");
    fileSys.WriteAllText(@"root\folder3\file.txt", @"root\folder3\file.txt");
    fileSys.WriteAllText(@"root\folder4\file.txt", @"root\folder4\file.txt");
    fileSys.WriteAllBytes(@"root\folder1\file2.bin", Encoding.UTF8.GetBytes("hello DB"));
    fileSys.Copy(@"root\folder1\file2.txt", @"root\folder1\file2_copy.txt");
    fileSys.Delete(@"root\folder1\file2.txt");
    fileSys.Move(@"root\folder1\file.txt", @"root\folderX\file_moved.txt", overWrite : true);
    fileSys.Move(@"root\folder1\file2.bin", @"root\folderX\file_moved.txt", overWrite: true);
    // Get files in folder1
    string[] files = fileSys.GetFiles(@"root", wildcard: "*.*");
    foreach (var file in files)
    {
        Console.WriteLine($"{file} : {fileSys.ReadAllText(file)}");
    }
    Console.WriteLine("Done");
    Console.ReadLine();
}
```cs
