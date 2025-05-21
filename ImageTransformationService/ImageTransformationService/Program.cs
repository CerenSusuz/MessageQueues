using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using Tesseract;
using System.IO;

Directory.CreateDirectory("TransformedFiles");

var factory = new ConnectionFactory() { HostName = "localhost" };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.QueueDeclare(queue: "transform_queue",
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

var consumer = new EventingBasicConsumer(channel);
consumer.Received += (model, ea) =>
{
    var fileName = ea.BasicProperties.Headers["FileName"] != null
        ? Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["FileName"])
        : Guid.NewGuid().ToString();

    var body = ea.Body.ToArray();

    using var inputStream = new MemoryStream(body);
    using var image = Image.Load(inputStream);

    var outputImagePath = Path.Combine("TransformedFiles", Path.GetFileNameWithoutExtension(fileName) + ".jpg");
    using var outputStream = File.OpenWrite(outputImagePath);
    image.SaveAsJpeg(outputStream, new JpegEncoder { Quality = 90 });

    Console.WriteLine($"🖼️ Transformed and saved image: {outputImagePath}");

    try
    {
        using var ocrEngine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
        using var imgForOCR = Pix.LoadFromMemory(body);
        using var page = ocrEngine.Process(imgForOCR);
        string text = page.GetText();

        var textOutputPath = Path.Combine("TransformedFiles", Path.GetFileNameWithoutExtension(fileName) + ".txt");
        File.WriteAllText(textOutputPath, text);

        Console.WriteLine($"🧾 OCR completed and saved to: {textOutputPath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ OCR failed: {ex.Message}");
    }
};

channel.BasicConsume(queue: "transform_queue", autoAck: true, consumer: consumer);
Console.WriteLine(" [*] Waiting for images to transform and OCR. Press [Enter] to exit.");
Console.ReadLine();
