using System;
using System.IO;
using System.Text;
using System.Threading;
using RabbitMQ.Client;

class Program
{
    const string folderPath = "InputFiles";
    const int chunkSize = 512 * 1024; // 512 KB

    static void Main()
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "file_queue",
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);

        Console.WriteLine("📂 Watching folder: " + Path.GetFullPath(folderPath));
        Directory.CreateDirectory(folderPath);

        FileSystemWatcher watcher = new FileSystemWatcher(folderPath);
        watcher.Created += (sender, e) => SendFileInChunks(e.FullPath, channel);
        watcher.EnableRaisingEvents = true;

        Console.WriteLine("📡 Waiting for files... Press Enter to exit.");
        Console.ReadLine();
    }

    static void SendFileInChunks(string filePath, IModel channel)
    {
        var fileName = Path.GetFileName(filePath);
        var fileBytes = File.ReadAllBytes(filePath);
        int totalChunks = (int)Math.Ceiling((double)fileBytes.Length / chunkSize);

        Console.WriteLine($"📤 Sending file: {fileName} ({fileBytes.Length / 1024} KB, {totalChunks} chunks)");

        for (int i = 0; i < totalChunks; i++)
        {
            int currentChunkSize = Math.Min(chunkSize, fileBytes.Length - (i * chunkSize));
            byte[] chunkData = new byte[currentChunkSize];
            Array.Copy(fileBytes, i * chunkSize, chunkData, 0, currentChunkSize);

            var props = channel.CreateBasicProperties();
            props.Headers = new System.Collections.Generic.Dictionary<string, object>
            {
                { "FileName", fileName },
                { "ChunkIndex", i },
                { "TotalChunks", totalChunks }
            };

            channel.BasicPublish(exchange: "",
                                 routingKey: "file_queue",
                                 basicProperties: props,
                                 body: chunkData);

            Console.WriteLine($"  -> Chunk {i + 1}/{totalChunks} sent.");
        }

        Console.WriteLine("✅ File sent.\n");
    }
}
