using Hazina.Tools.Services.Images.Abstractions;
using Hazina.Tools.Services.Images.Operations;
using Hazina.Tools.Services.Images.Providers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Hazina.Tools.Services.Images.Tests;

public class ImageToolTests
{
    private readonly ImageTool _imageTool;

    public ImageToolTests()
    {
        var providers = new IImageProvider[] { new LocalImageProvider() };
        var resolver = new ImageProviderResolver(providers);
        _imageTool = new ImageTool(resolver);
    }

    [Fact]
    public async Task EditAsync_WithNoOperations_ReturnsOriginalImage()
    {
        // Arrange
        using var inputStream = CreateTestImage(100, 100);
        var request = new ImageEditRequest
        {
            InputImage = inputStream,
            Operations = Array.Empty<ImageEditOperation>()
        };

        // Act
        var result = await _imageTool.EditAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Image.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EditAsync_WithSingleOperation_AppliesOperation()
    {
        // Arrange
        using var inputStream = CreateTestImage(100, 100);
        var request = new ImageEditRequest
        {
            InputImage = inputStream,
            Operations = new[] { new CropOperation(0, 0, 50, 50) }
        };

        // Act
        var result = await _imageTool.EditAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        using var resultImage = await Image.LoadAsync<Rgba32>(result.Image);
        resultImage.Width.Should().Be(50);
        resultImage.Height.Should().Be(50);
    }

    [Fact]
    public async Task EditAsync_WithMultipleOperations_AppliesAllInSequence()
    {
        // Arrange
        using var inputStream = CreateTestImage(100, 100);
        var request = new ImageEditRequest
        {
            InputImage = inputStream,
            Operations = new ImageEditOperation[]
            {
                new CropOperation(0, 0, 80, 80),
                new ResizeOperation(40, 40, MaintainAspectRatio: false)
            }
        };

        // Act
        var result = await _imageTool.EditAsync(request);

        // Assert
        result.Success.Should().BeTrue();

        using var resultImage = await Image.LoadAsync<Rgba32>(result.Image);
        resultImage.Width.Should().Be(40);
        resultImage.Height.Should().Be(40);
    }

    [Fact]
    public async Task EditAsync_ResetsOutputStreamPosition()
    {
        // Arrange
        using var inputStream = CreateTestImage(100, 100);
        var request = new ImageEditRequest
        {
            InputImage = inputStream,
            Operations = new[] { new CropOperation(0, 0, 50, 50) }
        };

        // Act
        var result = await _imageTool.EditAsync(request);

        // Assert
        result.Image.Position.Should().Be(0);
    }

    [Fact]
    public async Task EditAsync_DoesNotMutateOriginalStream()
    {
        // Arrange
        using var inputStream = CreateTestImage(100, 100);
        var originalLength = inputStream.Length;
        inputStream.Position = 0;

        var request = new ImageEditRequest
        {
            InputImage = inputStream,
            Operations = new[] { new CropOperation(0, 0, 50, 50) }
        };

        // Act
        var result = await _imageTool.EditAsync(request);

        // Assert
        inputStream.Length.Should().Be(originalLength);
    }

    [Fact]
    public async Task EditAsync_WithOutputFormatJpeg_ProducesJpegImage()
    {
        // Arrange
        using var inputStream = CreateTestImage(100, 100);
        var request = new ImageEditRequest
        {
            InputImage = inputStream,
            Operations = Array.Empty<ImageEditOperation>(),
            OutputFormat = ImageOutputFormat.Jpeg
        };

        // Act
        var result = await _imageTool.EditAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Image.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EditAsync_WithCancellation_ThrowsOperationCanceled()
    {
        // Arrange
        using var inputStream = CreateTestImage(100, 100);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new ImageEditRequest
        {
            InputImage = inputStream,
            Operations = new[] { new CropOperation(0, 0, 50, 50) }
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _imageTool.EditAsync(request, cts.Token));
    }

    [Fact]
    public void Constructor_WithNullResolver_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ImageTool(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private static MemoryStream CreateTestImage(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, Color.Blue);
        var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;
        return stream;
    }
}
