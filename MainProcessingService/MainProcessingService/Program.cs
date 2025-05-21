using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

class Program
{
    private static readonly Dictionary<string, FileBuffer> fileBuffers = new();

    static void Main(string[] args)
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "file_queue",
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (model, ea) =>
        {
            var headers = ea.BasicProperties.Headers;

            var fileName = Encoding.UTF8.GetString((byte[])headers["FileName"]);
            int chunkIndex = Convert.ToInt32(headers["ChunkIndex"]);
            int totalChunks = Convert.ToInt32(headers["TotalChunks"]);

            byte[] chunkData = ea.Body.ToArray();

            lock (fileBuffers)
            {
                if (!fileBuffers.ContainsKey(fileName))
                    fileBuffers[fileName] = new FileBuffer(totalChunks);

                fileBuffers[fileName].AddChunk(chunkIndex, chunkData);

                Console.WriteLine($"📥 Received chunk {chunkIndex + 1}/{totalChunks} of {fileName}");

                if (fileBuffers[fileName].IsComplete)
                {
                    // 1. Dosyayı birleştir
                    byte[] completeFile = fileBuffers[fileName].GetCombinedFile();
                    string outputPath = Path.Combine("ReceivedFiles", fileName);
                    Directory.CreateDirectory("ReceivedFiles");
                    File.WriteAllBytes(outputPath, completeFile);

                    Console.WriteLine($"✅ File assembled and saved: {outputPath}\n");

                    // 2. Dönüştürme servisine gönder
                    SendToTransformQueue(completeFile, fileName);

                    // 3. Buffers'tan kaldır
                    fileBuffers.Remove(fileName);
                }
            }
        };

        channel.BasicConsume(queue: "file_queue",
                             autoAck: true,
                             consumer: consumer);

        Console.WriteLine("📡 Waiting for file chunks. Press [Enter] to exit.");
        Console.ReadLine();
    }

    static void SendToTransformQueue(byte[] fileData, string fileName)
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "transform_queue",
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);

        var properties = channel.CreateBasicProperties();
        properties.Headers = new Dictionary<string, object>
        {
            { "FileName", Encoding.UTF8.GetBytes(fileName) }
        };

        channel.BasicPublish(exchange: "",
                             routingKey: "transform_queue",
                             basicProperties: properties,
                             body: fileData);

        Console.WriteLine($"📨 Sent file to transform_queue: {fileName}");
    }

    class FileBuffer
    {
        private readonly byte[][] chunks;
        private readonly bool[] received;
        private int totalReceived = 0;

        public FileBuffer(int totalChunks)
        {
            chunks = new byte[totalChunks][];
            received = new bool[totalChunks];
        }

        public void AddChunk(int index, byte[] data)
        {
            if (!received[index])
            {
                chunks[index] = data;
                received[index] = true;
                totalReceived++;
            }
        }

        public bool IsComplete => totalReceived == chunks.Length;

        public byte[] GetCombinedFile()
        {
            using MemoryStream ms = new();
            foreach (var chunk in chunks)
                ms.Write(chunk, 0, chunk.Length);
            return ms.ToArray();
        }
    }
}
