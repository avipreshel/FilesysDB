using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesysDB
{
    public interface IFileSysDB : IDisposable
    {
        void CreateOrOpen(string dbFile);
        void WriteAllText(string fileName, string content);
        void WriteAllBytes(string fileName, byte[] content);
        string ReadAllText(string fileName);
        byte[] ReadAllBytes(string fileName);
        string[] GetFiles(string dirName, string wildcard = "");
        void Move(string sourceFile, string destFile, bool overWrite = true);
        void Copy(string sourceFile, string destFile);
        void Delete(string fileName);
    }

    public class FileSysDB : IFileSysDB
    {
        private bool _disposed = false; // Flag to track disposal state
        private SqliteConnection? _connection;

        public void CreateOrOpen(string dbFile)
        {
            _connection = new SqliteConnection($"Data Source={dbFile}");
            _connection.Open();

            using (var command = new SqliteCommand("PRAGMA journal_mode = WAL;", _connection))
            {
                command.ExecuteNonQuery();
            }

            // Create a single table to store both metadata and content (no 'Directory' column)
            using (var command = new SqliteCommand(@"CREATE TABLE IF NOT EXISTS Files (
                                                     FilePath TEXT PRIMARY KEY, 
                                                     IsBinary INTEGER, 
                                                     Data BLOB);", _connection))
            {
                command.ExecuteNonQuery();
            }

            // Create an index on FilePath for faster queries
            using (var command = new SqliteCommand(@"CREATE INDEX IF NOT EXISTS idx_FilePath ON Files (FilePath);", _connection))
            {
                command.ExecuteNonQuery();
            }
        }

        public void WriteAllText(string fileName, string content)
        {
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);

            // Insert or update file metadata and content in the same table
            using (var command = new SqliteCommand("INSERT OR REPLACE INTO Files (FilePath, IsBinary, Data) VALUES (@FilePath, @IsBinary, @Data);", _connection))
            {
                command.Parameters.AddWithValue("@FilePath", fileName);
                command.Parameters.AddWithValue("@IsBinary", 0); // 0 means it's a text file
                command.Parameters.AddWithValue("@Data", contentBytes);
                command.ExecuteNonQuery();
            }
        }

        public void WriteAllBytes(string fileName, byte[] content)
        {
            // Insert or update file metadata and content in the same table
            using (var command = new SqliteCommand("INSERT OR REPLACE INTO Files (FilePath, IsBinary, Data) VALUES (@FilePath, @IsBinary, @Data);", _connection))
            {
                command.Parameters.AddWithValue("@FilePath", fileName);
                command.Parameters.AddWithValue("@IsBinary", 1); // 1 means it's a binary file
                command.Parameters.AddWithValue("@Data", content);
                command.ExecuteNonQuery();
            }
        }

        public string ReadAllText(string fileName)
        {
            byte[]? content = ReadAllBytes(fileName); // Use nullable byte array here
            if (content == null || content.Length == 0) return string.Empty;
            return Encoding.UTF8.GetString(content);
        }

        public byte[] ReadAllBytes(string fileName)
        {
            using (var command = new SqliteCommand("SELECT Data FROM Files WHERE FilePath = @FilePath;", _connection))
            {
                command.Parameters.AddWithValue("@FilePath", fileName);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return (byte[])reader["Data"]; // Using `as` to safely handle nullable value
                    }
                }
            }
            throw new Exception("oops");
        }

        // New Delete method to remove a file by its fileName
        public void Delete(string fileName)
        {
            using (var command = new SqliteCommand("DELETE FROM Files WHERE FilePath = @FilePath;", _connection))
            {
                command.Parameters.AddWithValue("@FilePath", fileName);
                int rowsAffected = command.ExecuteNonQuery();

                if (rowsAffected == 0)
                {
                    Console.WriteLine($"No file found with the path: {fileName}");
                }
                else
                {
                }
            }
        }

        public string[] GetFiles(string dirName, string wildcard = "*.*")
        {
            // Normalize the directory path by replacing any backslashes with slashes
            // and ensuring it is in the correct format
            dirName = dirName.Replace("/", "\\");  // Ensure the directory separator is correct

            // Prepare the search pattern
            string searchPattern;

            if (wildcard.Contains("*") || wildcard.Contains("?"))
            {
                // If wildcard contains "*" or "?", treat it as a pattern
                searchPattern = wildcard.Replace(".", "%").Replace("*", "%").Replace("?", "_");
            }
            else
            {
                // If no wildcard, treat it as an exact file name match
                searchPattern = wildcard;
            }

            // Prepare the query for files in the directory
            string query = "SELECT FilePath FROM Files WHERE FilePath LIKE @DirName AND FilePath LIKE @Wildcard";

            // Prepare the SQL command
            using (var cmd = _connection!.CreateCommand())
            {
                // Set parameters for the directory and wildcard
                cmd.CommandText = query;
                cmd.Parameters.AddWithValue("@DirName", $"{dirName}%");  // Match files under the directory (including subdirectories)
                cmd.Parameters.AddWithValue("@Wildcard", $"%{searchPattern}%");  // Apply wildcard matching for file names

                // Execute the query and read the results
                using (var reader = cmd.ExecuteReader())
                {
                    List<string> files = new List<string>();

                    while (reader.Read())
                    {
                        files.Add(reader.GetString(0)); // Read file path
                    }

                    return files.ToArray();
                }
            }
        }

        public void Copy(string sourceFile, string destFile)
        {
            // Ensure that both source and destination paths are valid
            if (string.IsNullOrWhiteSpace(sourceFile) || string.IsNullOrWhiteSpace(destFile))
            {
                throw new ArgumentException("Source or destination file path cannot be empty.");
            }

            // Open a connection to the SQLite database

            // Check if the source file exists
            var sourceFileExists = FileExists(_connection!, sourceFile);
            if (!sourceFileExists)
            {
                throw new FileNotFoundException("Source file does not exist.", sourceFile);
            }

            // Check if the destination file already exists
            var destFileExists = FileExists(_connection!, destFile);
            if (destFileExists)
            {
                throw new InvalidOperationException("Destination file already exists.");
            }

            // Copy the file: Insert a new row with the same content in the destination
            using (var cmd = _connection!.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO Files (FilePath, Data) SELECT @DestFile, Data FROM Files WHERE FilePath = @SourceFile";
                cmd.Parameters.AddWithValue("@SourceFile", sourceFile);
                cmd.Parameters.AddWithValue("@DestFile", destFile);

                int rowsAffected = cmd.ExecuteNonQuery();

                if (rowsAffected == 0)
                {
                    throw new InvalidOperationException("File copy operation failed.");
                }
            }
        }

        public void Move(string sourceFile, string destFile, bool overWrite = false)
        {
            if (_connection == null)
                return;

            if (string.IsNullOrWhiteSpace(sourceFile) || string.IsNullOrWhiteSpace(destFile))
            {
                throw new ArgumentException("Source or destination file path cannot be empty.");
            }

            // Check if the source file exists
            if (!FileExists(_connection, sourceFile))
            {
                throw new FileNotFoundException($"Source file '{sourceFile}' does not exist.");
            }

            // If overwrite is false, check if the destination file exists
            if (!overWrite && FileExists(_connection, destFile))
            {
                throw new InvalidOperationException($"Destination file '{destFile}' already exists.");
            }

            // Start a transaction to ensure atomicity
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    // If overwrite is true, delete the destination file
                    if (overWrite)
                    {
                        using (var deleteCmd = _connection.CreateCommand())
                        {
                            deleteCmd.CommandText = "DELETE FROM Files WHERE FilePath = @DestFile";
                            deleteCmd.Parameters.AddWithValue("@DestFile", destFile);
                            deleteCmd.ExecuteNonQuery();
                        }
                    }

                    // Update the file path of the source file to the destination file
                    using (var updateCmd = _connection.CreateCommand())
                    {
                        updateCmd.CommandText = "UPDATE Files SET FilePath = @DestFile WHERE FilePath = @SourceFile";
                        updateCmd.Parameters.AddWithValue("@SourceFile", sourceFile);
                        updateCmd.Parameters.AddWithValue("@DestFile", destFile);

                        int rowsAffected = updateCmd.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            throw new InvalidOperationException($"Failed to move file from '{sourceFile}' to '{destFile}'.");
                        }
                    }

                    // Delete the source file from the database
                    using (var deleteSourceCmd = _connection.CreateCommand())
                    {
                        deleteSourceCmd.CommandText = "DELETE FROM Files WHERE FilePath = @SourceFile";
                        deleteSourceCmd.Parameters.AddWithValue("@SourceFile", sourceFile);
                        deleteSourceCmd.ExecuteNonQuery();
                    }

                    // Commit the transaction to apply changes
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    // Rollback transaction in case of any errors
                    transaction.Rollback();
                    throw new InvalidOperationException("File move operation failed.", ex);
                }
            }
        }



        private bool FileExists(SqliteConnection conn, string filePath)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM Files WHERE FilePath = @FilePath";
                cmd.Parameters.AddWithValue("@FilePath", filePath);

                long count = (long)cmd.ExecuteScalar()!;
                return count > 0;
            }
        }






        // IDisposable implementation to release resources
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Internal dispose method for disposing resources
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _connection?.Dispose();
                }

                // Dispose unmanaged resources if any (not applicable here)
                _disposed = true;
            }
        }

        // Destructor/finalizer to handle resource cleanup if Dispose is not called explicitly
        ~FileSysDB()
        {
            Dispose(false);
        }
    }

}
