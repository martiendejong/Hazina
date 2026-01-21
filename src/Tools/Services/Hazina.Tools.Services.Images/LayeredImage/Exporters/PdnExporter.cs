using System.IO.Compression;
using System.Text;
using Hazina.Tools.Services.Images.LayeredImage.Abstractions;
using Hazina.Tools.Services.Images.LayeredImage.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Hazina.Tools.Services.Images.LayeredImage.Exporters;

/// <summary>
/// Exports layered images to Paint.NET PDN format (PDN3).
/// Uses manual NRBF binary construction to avoid BinaryFormatter restrictions in .NET 8+.
/// </summary>
public class PdnExporter : ILayeredImageExporter
{
    public LayeredImageFormat Format => LayeredImageFormat.Pdn;
    public string FileExtension => ".pdn";

    // NRBF record types
    private const byte SerializedStreamHeader = 0;
    private const byte ClassWithMembersAndTypes = 5;
    private const byte MessageEnd = 11;
    private const byte BinaryLibrary = 12;
    private const byte ArraySinglePrimitive = 15;
    private const byte BinaryObjectString = 6;

    // Primitive type codes
    private const byte PrimitiveTypeBoolean = 1;
    private const byte PrimitiveTypeByte = 2;
    private const byte PrimitiveTypeInt32 = 8;

    // Binary type codes
    private const byte BinaryTypePrimitive = 0;
    private const byte BinaryTypeString = 1;
    private const byte BinaryTypeClass = 4;
    private const byte BinaryTypePrimitiveArray = 7;

    /// <summary>
    /// Wrapper class to hold object ID counter for use in async methods.
    /// </summary>
    private class ObjectIdCounter
    {
        public int Value { get; set; } = 1;
        public int Next() => Value++;
    }

    public async Task<byte[]> ExportAsync(
        int canvasWidth,
        int canvasHeight,
        IReadOnlyList<LayerGenerationResult> layers,
        CancellationToken cancellationToken = default)
    {
        using var outputStream = new MemoryStream();

        // Write PDN3 magic header (uncompressed)
        var magic = Encoding.ASCII.GetBytes("PDN3");
        await outputStream.WriteAsync(magic, cancellationToken);

        // Write compressed NRBF data
        using var compressedStream = new MemoryStream();
        await using (var gzip = new GZipStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            var nrbfData = BuildNrbfData(canvasWidth, canvasHeight, layers, cancellationToken);
            await gzip.WriteAsync(nrbfData, cancellationToken);
        }

        compressedStream.Position = 0;
        await compressedStream.CopyToAsync(outputStream, cancellationToken);

        return outputStream.ToArray();
    }

    private byte[] BuildNrbfData(
        int canvasWidth,
        int canvasHeight,
        IReadOnlyList<LayerGenerationResult> layers,
        CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        var idCounter = new ObjectIdCounter();

        // Write SerializationHeader
        writer.Write(SerializedStreamHeader);
        writer.Write(1); // Root ID
        writer.Write(-1); // Header ID
        writer.Write(1); // Major version
        writer.Write(0); // Minor version

        // Write Paint.NET library reference
        var pdnLibraryId = idCounter.Next();
        WriteBinaryLibrary(writer, pdnLibraryId, "PaintDotNet.Data, Version=5.0.0.0, Culture=neutral, PublicKeyToken=null");

        // Write Document class
        var documentId = idCounter.Next();
        WriteDocumentClass(writer, documentId, pdnLibraryId, canvasWidth, canvasHeight, layers.Count, idCounter);

        // Write layers
        foreach (var layer in layers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var layerId = idCounter.Next();
            WriteLayerClass(writer, layerId, pdnLibraryId, layer, canvasWidth, canvasHeight, idCounter);
        }

        // Write MessageEnd
        writer.Write(MessageEnd);

        return ms.ToArray();
    }

    private static void WriteBinaryLibrary(BinaryWriter writer, int libraryId, string libraryName)
    {
        writer.Write(BinaryLibrary);
        writer.Write(libraryId);
        WriteLengthPrefixedString(writer, libraryName);
    }

    private static void WriteDocumentClass(BinaryWriter writer, int objectId, int libraryId, int width, int height, int layerCount, ObjectIdCounter idCounter)
    {
        // ClassWithMembersAndTypes for Document
        writer.Write(ClassWithMembersAndTypes);
        writer.Write(objectId);

        // Class info
        WriteLengthPrefixedString(writer, "PaintDotNet.Document");
        writer.Write(3); // Member count: width, height, layers

        // Member names
        WriteLengthPrefixedString(writer, "width");
        WriteLengthPrefixedString(writer, "height");
        WriteLengthPrefixedString(writer, "layers");

        // Member types
        writer.Write(BinaryTypePrimitive);
        writer.Write(BinaryTypePrimitive);
        writer.Write(BinaryTypeClass);

        // Additional info for primitives
        writer.Write(PrimitiveTypeInt32); // width type
        writer.Write(PrimitiveTypeInt32); // height type

        // Additional info for layers (class reference)
        WriteLengthPrefixedString(writer, "PaintDotNet.LayerList");
        writer.Write(libraryId);

        // Library ID
        writer.Write(libraryId);

        // Write member values
        writer.Write(width);
        writer.Write(height);

        // Write LayerList reference
        var layerListId = idCounter.Next();
        WriteLayerList(writer, layerListId, libraryId, layerCount, idCounter);
    }

    private static void WriteLayerList(BinaryWriter writer, int objectId, int libraryId, int layerCount, ObjectIdCounter idCounter)
    {
        writer.Write(ClassWithMembersAndTypes);
        writer.Write(objectId);

        WriteLengthPrefixedString(writer, "PaintDotNet.LayerList");
        writer.Write(2); // Member count: count, items

        WriteLengthPrefixedString(writer, "count");
        WriteLengthPrefixedString(writer, "items");

        writer.Write(BinaryTypePrimitive);
        writer.Write(BinaryTypePrimitiveArray);

        writer.Write(PrimitiveTypeInt32); // count type
        writer.Write(PrimitiveTypeInt32); // array element type

        writer.Write(libraryId);

        // Write values
        writer.Write(layerCount);
    }

    private static void WriteLayerClass(BinaryWriter writer, int objectId, int libraryId, LayerGenerationResult layer, int canvasWidth, int canvasHeight, ObjectIdCounter idCounter)
    {
        // Load the layer image and convert to BGRA
        byte[] bgraData;

        using (var image = Image.Load<Bgra32>(layer.ImageData))
        {
            // Create canvas-sized BGRA buffer
            bgraData = new byte[canvasWidth * canvasHeight * 4];

            // Copy layer data to the correct position on canvas
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    var canvasY = layer.PositionY + y;
                    if (canvasY < 0 || canvasY >= canvasHeight) continue;

                    for (var x = 0; x < row.Length; x++)
                    {
                        var canvasX = layer.PositionX + x;
                        if (canvasX < 0 || canvasX >= canvasWidth) continue;

                        var pixel = row[x];
                        var offset = ((canvasY * canvasWidth) + canvasX) * 4;

                        // Apply layer opacity
                        var alpha = (byte)((pixel.A * layer.Opacity) / 255);

                        bgraData[offset] = pixel.B;
                        bgraData[offset + 1] = pixel.G;
                        bgraData[offset + 2] = pixel.R;
                        bgraData[offset + 3] = alpha;
                    }
                }
            });
        }

        // Write BitmapLayer class
        writer.Write(ClassWithMembersAndTypes);
        writer.Write(objectId);

        WriteLengthPrefixedString(writer, "PaintDotNet.BitmapLayer");
        writer.Write(5); // Member count

        // Member names
        WriteLengthPrefixedString(writer, "name");
        WriteLengthPrefixedString(writer, "visible");
        WriteLengthPrefixedString(writer, "opacity");
        WriteLengthPrefixedString(writer, "blendMode");
        WriteLengthPrefixedString(writer, "surface");

        // Member types
        writer.Write(BinaryTypeString);
        writer.Write(BinaryTypePrimitive);
        writer.Write(BinaryTypePrimitive);
        writer.Write(BinaryTypePrimitive);
        writer.Write(BinaryTypeClass);

        // Additional type info
        writer.Write(PrimitiveTypeBoolean); // visible
        writer.Write(PrimitiveTypeByte);    // opacity
        writer.Write(PrimitiveTypeInt32);   // blendMode

        // Surface class info
        WriteLengthPrefixedString(writer, "PaintDotNet.Surface");
        writer.Write(libraryId);

        writer.Write(libraryId);

        // Write member values
        var stringId = idCounter.Next();
        writer.Write(BinaryObjectString);
        writer.Write(stringId);
        WriteLengthPrefixedString(writer, layer.Name);

        writer.Write(layer.Visible);
        writer.Write(layer.Opacity);
        writer.Write(BlendModeToInt(layer.BlendMode));

        // Write Surface (contains the bitmap data)
        var surfaceId = idCounter.Next();
        WriteSurface(writer, surfaceId, libraryId, canvasWidth, canvasHeight, bgraData, idCounter);
    }

    private static void WriteSurface(BinaryWriter writer, int objectId, int libraryId, int width, int height, byte[] bgraData, ObjectIdCounter idCounter)
    {
        writer.Write(ClassWithMembersAndTypes);
        writer.Write(objectId);

        WriteLengthPrefixedString(writer, "PaintDotNet.Surface");
        writer.Write(3); // width, height, scan0

        WriteLengthPrefixedString(writer, "width");
        WriteLengthPrefixedString(writer, "height");
        WriteLengthPrefixedString(writer, "scan0");

        writer.Write(BinaryTypePrimitive);
        writer.Write(BinaryTypePrimitive);
        writer.Write(BinaryTypePrimitiveArray);

        writer.Write(PrimitiveTypeInt32);
        writer.Write(PrimitiveTypeInt32);
        writer.Write(PrimitiveTypeByte);

        writer.Write(libraryId);

        // Write values
        writer.Write(width);
        writer.Write(height);

        // Write byte array
        writer.Write(ArraySinglePrimitive);
        var arrayId = idCounter.Next();
        writer.Write(arrayId);
        writer.Write(bgraData.Length);
        writer.Write(PrimitiveTypeByte);
        writer.Write(bgraData);
    }

    private static int BlendModeToInt(LayerBlendMode blendMode)
    {
        return blendMode switch
        {
            LayerBlendMode.Normal => 0,
            LayerBlendMode.Multiply => 1,
            LayerBlendMode.Screen => 3,
            LayerBlendMode.Overlay => 4,
            LayerBlendMode.Darken => 7,
            LayerBlendMode.Lighten => 8,
            _ => 0
        };
    }

    private static void WriteLengthPrefixedString(BinaryWriter writer, string value)
    {
        // NRBF uses 7-bit encoded length prefix
        var bytes = Encoding.UTF8.GetBytes(value);
        Write7BitEncodedInt(writer, bytes.Length);
        writer.Write(bytes);
    }

    private static void Write7BitEncodedInt(BinaryWriter writer, int value)
    {
        var v = (uint)value;
        while (v >= 0x80)
        {
            writer.Write((byte)(v | 0x80));
            v >>= 7;
        }
        writer.Write((byte)v);
    }
}
