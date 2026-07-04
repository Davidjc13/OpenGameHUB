using System.Drawing;
using System.Drawing.Imaging;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: GenerateIcon <input.png> <output.ico>");
    return 1;
}

var inputPath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Input not found: {inputPath}");
    return 1;
}

using var source = new Bitmap(inputPath);
var sizes = new[] { 16, 32, 48, 64, 128, 256 };
using var stream = new MemoryStream();
using var writer = new BinaryWriter(stream);

writer.Write((short)0);
writer.Write((short)1);
writer.Write((short)sizes.Length);

var imageData = new List<byte[]>();
var offset = 6 + 16 * sizes.Length;

foreach (var size in sizes)
{
    using var resized = new Bitmap(source, new Size(size, size));
    using var pngStream = new MemoryStream();
    resized.Save(pngStream, ImageFormat.Png);
    var pngBytes = pngStream.ToArray();
    imageData.Add(pngBytes);

    writer.Write((byte)(size >= 256 ? 0 : size));
    writer.Write((byte)(size >= 256 ? 0 : size));
    writer.Write((byte)0);
    writer.Write((byte)0);
    writer.Write((short)1);
    writer.Write((short)32);
    writer.Write(pngBytes.Length);
    writer.Write(offset);
    offset += pngBytes.Length;
}

foreach (var data in imageData)
    writer.Write(data);

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllBytes(outputPath, stream.ToArray());
Console.WriteLine($"Wrote {outputPath}");
return 0;
