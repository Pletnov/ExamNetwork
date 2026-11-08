
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Exam.Common.Models;
using HW_3.Common;

namespace Exam.Server.Models;

public class ServerCore
{
    private readonly ConcurrentDictionary<string, byte> _downloaded = new();
    private readonly SemaphoreSlim _downloadSemaphore = new(1, 1);

    private readonly string _downloadsPath = Directory.CreateDirectory("downloads").FullName;

    public async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using NetworkStream stream = client.GetStream();
            string commandType = await NetworkHelper.ReceiveStringAsync(stream);
            Console.WriteLine($"Received command: {commandType}");
            switch (commandType)
            {
                case CommandType.AddFile:
                    await HandleAddFileCommandAsync(stream);
                    break;

                case CommandType.GetFiles:
                    await HandleGetFilesCommandAsync(stream);
                    break;

                case CommandType.DownloadFile:
                    await HandleDownloadFileCommandAsync(stream);
                    break;

                default:
                    await NetworkHelper.SendStringAsync(
                        stream, "Unknown command");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client error: {ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine("Client disconnected");
        }
    }
    private async Task HandleAddFileCommandAsync(NetworkStream stream)
    {
        string url = await NetworkHelper.ReceiveStringAsync(stream);
        string fileName = string.Empty;
        bool semaphoreAcquired = false;

        try
        {
            if (url.Contains("google.com/imgres"))
            {
                string[] parameters = url.Split('?').Last().Split('&');
                foreach (string parameter in parameters)
                {
                    if (parameter.StartsWith("imgurl="))
                    {
                        url = parameter.Substring(7);
                        break;
                    }
                }
            }

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                await NetworkHelper.SendStringAsync(stream, "Invalid URL");
                return;
            }
            if (!_downloaded.TryAdd(url, 0))
            {
                await NetworkHelper.SendStringAsync(stream, "Already downloaded");
                return;
            }

            fileName = url.Split('?')[0].Split('/').Last();

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "file_" + DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            }
            await _downloadSemaphore.WaitAsync();
            semaphoreAcquired = true;

            Console.WriteLine($"Downloading: {fileName}");

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };

            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                _downloaded.TryRemove(url, out _);
                await NetworkHelper.SendStringAsync(stream, "Download failed");
                return;
            }
            string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            string extension = contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                "application/pdf" => ".pdf",
                "application/zip" => ".zip",
                _ => ""
            };
            if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
            {
                fileName += extension;
            }
            fileName = Path.GetFileName(fileName);

            string fullPath = Path.Combine(_downloadsPath, fileName);

            if (File.Exists(fullPath))
            {
                _downloaded.TryRemove(url, out _);
                await NetworkHelper.SendStringAsync(stream, "File already exists");
                return;
            }

            using var input = await response.Content.ReadAsStreamAsync();
            using var output = File.Create(fullPath);
            byte[] buffer = new byte[8192];
            long downloadedBytes = 0;
            int bytesRead;
            long? totalBytes = response.Content.Headers.ContentLength;

            while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await output.WriteAsync(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;
                if (totalBytes.HasValue && totalBytes.Value > 0)
                {
                    double percent =
                        (double)downloadedBytes / totalBytes.Value * 100;
                    Console.Write($"\rProgress: {percent:F1}%");
                }
                else
                {
                    Console.Write($"\rDownloaded: {downloadedBytes} bytes");
                }
            }

            Console.WriteLine();

            await NetworkHelper.SendStringAsync(stream, "File successfully downloaded");
            Console.WriteLine($"Saved: {fullPath}");
        }
        catch (Exception ex)
        {
            _downloaded.TryRemove(url, out _);

            Console.WriteLine($"Error on downloading: {ex.Message}");

            try
            {
                await NetworkHelper.SendStringAsync(stream, "Error on downloading");
            }
            catch
            {
            }
        }
        finally
        {
            if (semaphoreAcquired)
            {
                _downloadSemaphore.Release();
            }
        }
    }
    private async Task HandleGetFilesCommandAsync(NetworkStream stream)
    {
        List<MessageFiles> files = Directory
            .GetFiles(_downloadsPath)
            .Select(path => new MessageFiles
            {
                FileName = Path.GetFileName(path),
                FileStatus = FileStatus.Downloaded
            })
            .ToList();

        await NetworkHelper.SendObjectAsync(stream, files);

        Console.WriteLine($"Files in server folder: {files.Count}");
    }
    private async Task HandleDownloadFileCommandAsync(NetworkStream stream)
    {
        string fileName = await NetworkHelper.ReceiveStringAsync(stream);

        fileName = Path.GetFileName(fileName);

        string fullPath = Path.Combine(_downloadsPath, fileName);

        if (!File.Exists(fullPath))
        {
            await NetworkHelper.SendStringAsync(stream, "File not found");

            return;
        }

        await NetworkHelper.SendStringAsync(stream, "OK");

        await NetworkHelper.SendFileAsync(stream, fullPath);

        Console.WriteLine($"File sent to client: {fileName}");
    }
}
