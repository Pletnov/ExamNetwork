using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Exam.Client.Models;
using Exam.Common.Models;
using HW_3.Common;

Console.WriteLine("Client");

var serverIp = "192.168.0.199";
var serverPort = 5000;
var client = new Client(IPAddress.Parse(serverIp), serverPort);
DirectoryInfo dirInfo = Directory.CreateDirectory(Directory.GetCurrentDirectory());

while (true)
{
    Console.WriteLine("=== Menu ===");
    Console.WriteLine("1. Add file to upload");
    Console.WriteLine("2. Files on server");
    Console.WriteLine("3. Download file from server");
    Console.WriteLine("0. Exit");
    Console.WriteLine("============");
    Console.Write(">>> ");
    string option = Console.ReadLine();
    switch(option)
    {
        case "1":
            Console.WriteLine("Enter the url of file");
            Console.Write(">>> ");
            string url = Console.ReadLine();
            await client.SendUrlAsync(url);
            break;
        case "2":
            var files = await client.GetFilesAsync();
            if (files == null)
            {
                Console.WriteLine("Failed to get files from server");
                break;
            }
            Console.WriteLine("Files on Server: ");
            foreach (var item in files)
            {
                Console.WriteLine($"{item.FileStatus} - {item.FileName}");
            }
            break;
        case "3":
            var filesToDownload = await client.GetFilesAsync();
            if(filesToDownload == null)
            {
                Console.WriteLine("Failed to get files from server");
                break;
            }
            List<string> filesToDownloadOptions = new List<string>();
            Console.WriteLine("Files on Server to download: ");
            for (int i = 0; i < filesToDownload.Count; i++)
            {
                if (filesToDownload[i].FileStatus == FileStatus.Downloaded)
                {
                    Console.WriteLine($"{i}. {filesToDownload[i].FileName}");
                    filesToDownloadOptions.Add(filesToDownload[i].FileName);
                }
            }
            Console.WriteLine("Enter name of downloading file");
            Console.Write(">>> ");
            string name = Console.ReadLine();
            foreach (var item in filesToDownloadOptions)
            {
                if(item == name)
                {
                    bool success = await client.DownloadFilesAsync(name, dirInfo.FullName);
                    if (success)
                    {
                        Console.WriteLine($"File {name} successfully received and saved!");
                    }
                }
            }
            break;
        case "0":
            return;
        default:
            Console.WriteLine("Wrong option");
            break;
    }
}