
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Exam.Common.Models;
using HW_3.Common;

namespace Exam.Client.Models;

public class Client
{
    private readonly IPAddress _serverIp;
    private readonly int _serverPort;

    private TcpClient? _client;
    private NetworkStream? _stream;

    public Client(IPAddress serverIp, int serverPort)
    {
        _serverIp = serverIp;
        _serverPort = serverPort;
    }

    private async Task<bool> ConnectAsync()
    {
        try
        {
            _client = new TcpClient();

            await _client.ConnectAsync(_serverIp, _serverPort);

            _stream = _client.GetStream();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection error: {ex.Message}");

            _client?.Dispose();

            _client = null;
            _stream = null;

            return false;
        }
    }

    public NetworkStream? GetStream()
    {
        return _stream;
    }
    public async Task SendUrlAsync(string url)
    {
        if (_stream == null)
        {
            bool connected = await ConnectAsync();

            if (!connected)
            {
                Console.WriteLine("Server unavailable");
                return;
            }
        }

        try
        {
            await NetworkHelper.SendStringAsync(_stream, CommandType.AddFile);
            await NetworkHelper.SendStringAsync(_stream, url);
            string response = await NetworkHelper.ReceiveStringAsync(_stream!);
            Console.WriteLine(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sending error: {ex.Message}");
        }
        finally
        {
            Disconnect();
        }
    }
    public async Task<List<MessageFiles>?> GetFilesAsync()
    {
        if (_stream == null)
        {
            bool connected = await ConnectAsync();

            if (!connected)
            {
                Console.WriteLine("Server unavailable");
                return null;
            }
        }
        try
        {
            await NetworkHelper.SendStringAsync(_stream!, CommandType.GetFiles);
            List<MessageFiles>? files = await NetworkHelper.ReceiveObjectAsync<List<MessageFiles>>(_stream);
            return files;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Receiving error: {ex.Message}");
            return null;
        }
        finally
        {
            Disconnect();
        }
    }
    public async Task<bool> DownloadFilesAsync(string name, string fullpath)
    {
        if (_stream == null)
        {
            bool connected = await ConnectAsync();
            if (!connected)
            {
                Console.WriteLine("Server unavailable");
                return false;
            }
        }
        try
        {
            await NetworkHelper.SendStringAsync(_stream, CommandType.DownloadFile);
            await NetworkHelper.SendStringAsync(_stream, name);
            string status = await NetworkHelper.ReceiveStringAsync(_stream);
            if (status != "OK")
            {
                Console.WriteLine(status);
                return false;
            }
            Directory.CreateDirectory(fullpath);
            await NetworkHelper.ReceiveFileAsync(_stream!, fullpath);
            Console.WriteLine("File successfully downloaded");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Receiving error: {ex.Message}");
            return false;
        }
        finally
        {
            Disconnect();
        }
    }

    public void Disconnect()
    {
        _stream?.Dispose();
        _client?.Dispose();

        _stream = null;
        _client = null;
    }
}
