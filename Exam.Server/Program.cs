using System.Net;
using System.Net.Sockets;
using Exam.Server.Models;

Console.WriteLine("Server");

int port = 5000;
IPAddress ipAddress = IPAddress.Parse("192.168.0.199");

TcpListener listener = new TcpListener(ipAddress, port);
listener.Start();

Console.WriteLine($"Server started: {ipAddress}:{port}");

var serverCore = new ServerCore();

while (true)
{
    try
    {
        TcpClient client = await listener.AcceptTcpClientAsync();

        Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
        _ = Task.Run(async () =>
        {
            using (client)
            {
                try
                {
                    await serverCore.HandleClientAsync(client);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Client error: {ex.Message}");
                }
                finally
                {
                    Console.WriteLine("Client disconnected");
                }
            }
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Server error: {ex.Message}");
    }
}