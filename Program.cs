using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite; // Corrected namespace for Microsoft.Data.Sqlite

namespace FilesysDB
{
    class Program
    {
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

        static List<string> GenerateFolderPaths(int numberOfFolders)
        {
            Random random = new Random();
            List<string> folderPaths = new List<string>();
            int maxDepth = 3;

            for (int i = 0; i < numberOfFolders; i++)
            {
                List<string> pathParts = new List<string> { "root" };

                // Randomly decide the folder depth (1, 2, or 3)
                int depth = random.Next(1, maxDepth + 1);

                // Create folder path based on the chosen depth
                for (int j = 1; j <= depth; j++)
                {
                    pathParts.Add($"folder{random.Next(1, 500)}");
                }

                folderPaths.Add(string.Join("\\", pathParts));
            }

            return folderPaths;
        }

        // Method to generate random text of a given size
        static string GenerateRandomText(int size)
        {
            StringBuilder builder = new StringBuilder(size);
            Random random = new Random();

            for (int i = 0; i < size; i++)
            {
                builder.Append((char)random.Next(32, 127)); // Printable ASCII characters
            }

            return builder.ToString();
        }
        
    }
}
