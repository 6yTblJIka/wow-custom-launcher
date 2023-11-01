using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

internal class Program
{
    private static void Main(string[] args)
    {
        string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
        string exeDirectory = Path.GetDirectoryName(exePath);
        string checkFolder = "Files"; // Replace with the directory path you want to analyze
        string outputFileName = "CheckList.txt"; // Name of the output text file
        string linksFileName = "FileLinks.txt"; // Name of the file containing links

        string directoryPath;
        string outputFile;
        string linksFile;
        try
        {

            directoryPath = Path.Combine(exeDirectory, checkFolder);
            outputFile = Path.Combine(exeDirectory, outputFileName);
            linksFile = Path.Combine(exeDirectory, linksFileName);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Thread.Sleep(1000);
            directoryPath = checkFolder;
            outputFile = outputFileName;
            linksFile = linksFileName;
        }

        try
        {
            if (Directory.Exists(directoryPath))
            {
                string[] lines = File.Exists(linksFile) ? File.ReadAllLines(linksFile) : new string[0]; // Read all lines from FileLinks.txt or create an empty array

                string[] links = new string[lines.Length]; // Array to store links
                string[] names = new string[lines.Length]; // Array to store names

                for (int i = 0; i < lines.Length; i++)
                {
                    string[] parts = lines[i].Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length >= 2)
                    {
                        names[i] = parts[0]; // Store the name
                        links[i] = parts[1]; // Store the link
                    }
                    else if (parts.Length == 1)
                    {
                        names[i] = ""; // No name found
                        links[i] = parts[0]; // Store the link
                    }
                }

                if (!File.Exists(linksFile))
                {
                    links = new string[] { "null" };
                }

                using (StreamWriter writer = new StreamWriter(outputFile))
                {
                    // Recursively get all file paths in the directory and its subdirectories
                    string[] filePaths = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                    int totalFiles = filePaths.Length;

                    for (int i = 0; i < totalFiles; i++)
                    {
                        string filePath = filePaths[i];
                        string relativePath = filePath.Substring(directoryPath.Length + 1);
                        string fileChecksum = CalculateFileChecksum(filePath, i + 1, totalFiles);

                        // Check if the file name is in the links array
                        int linkIndex = Array.IndexOf(names, relativePath);
                        string link = linkIndex != -1 ? links[linkIndex] : "null";

                        // Append the link to the line in CheckList.txt
                        string line = $"{relativePath} {fileChecksum} {link}";

                        writer.WriteLine(line);
                        Console.Clear();
                        Console.WriteLine($"Processed {i + 1} of {totalFiles} files: {Path.GetFileName(filePath)}");
                        Console.WriteLine($"Output: {line}");
                    }

                    Console.WriteLine($"Check List has been made: {outputFile}");
                    Thread.Sleep(1000);
                }
            }
            else
            {
                Console.WriteLine($"Can't find {directoryPath}");
                Thread.Sleep(5000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            Thread.Sleep(5000);
            // Add additional error handling or logging as needed.
        }
    }

    private static string CalculateFileChecksum(string filePath, int currentFileIndex, int totalFiles)
    {
        using (var stream = File.OpenRead(filePath))
        {
            var sha = SHA256.Create();
            byte[] buffer = new byte[4096];
            long totalBytesRead = 0;
            long fileSize = new FileInfo(filePath).Length;

            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                sha.TransformBlock(buffer, 0, bytesRead, null, 0);
                totalBytesRead += bytesRead;

                int percentage = (int)((totalBytesRead * 100) / fileSize);
                Console.Write($"\rCalculating for file {currentFileIndex} of {totalFiles} ({percentage}%): {Path.GetFileName(filePath)}     ");
            }

            sha.TransformFinalBlock(buffer, 0, 0);

            // Convert the byte array to a hexadecimal string
            return BitConverter.ToString(sha.Hash).Replace("-", String.Empty);
        }
    }
}
