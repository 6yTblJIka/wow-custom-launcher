using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CleckList.Systems
{
    public static class ChecksumHelper
    {
        public static async Task<string> CalculateFileChecksumAsync(string filePath, int currentFileIndex,
            int totalFiles, Action<int> updateProgress = null)
        {
            return await Task.Run(() =>
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

                        // Update progress if the action is provided
                        updateProgress?.Invoke((int)((totalBytesRead * 100) / fileSize));
                    }

                    sha.TransformFinalBlock(buffer, 0, 0);

                    // Convert the byte array to a hexadecimal string
                    return BitConverter.ToString(sha.Hash).Replace("-", String.Empty);
                }
            });
        }
    }
}