using Hazina.Tools.Services.Images.Abstractions;
using Hazina.Tools.Services.Images.Operations;
using Hazina.Tools.Services.Images.Providers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Hazina.Tools.Services.Images.Tests;

public class LocalImageProviderTests
{
    private readonly LocalImageProvider _provider;
    private readonly ImageProviderOptions _defaultOptions;

    public LocalImageProviderTests()
    {
        _provider = new LocalImageProvider();
        _defaultOptions = new ImageProviderOptions();
    }

    [Fact]
    public void Name_ReturnsLocal()
    {
        _provider.Name.Should().Be("Local");
    }

    [Theory]
    [InlineData(typeof(AddTextOperation), true)]
    [InlineData(typeof(CropOperation), true)]
    [InlineData(typeof(ResizeOperation), true)]
    [InlineData(typeof(RotateOperation), true)]
    [InlineData(typeof(AdjustBrightnessOperation), true)]
    [InlineData(typeof(AdjustContrastOperation), true)]
    [InlineData(typeof(ApplyFilterOperation), true)]
    [InlineData(typeof(MaskReplaceOperation), false)]
    public void CanHandle_ReturnsExpectedResult(Type operationType, bool expected)
    {
        var operation = CreateOperation(operationType);
        _provider.CanHandle(operation).Should().Be(expected);
    }

    [Fact]
    public async Task ApplyAsync_CropOperation_ReducesDimensions()
    {
        // Arrange
        using var inputStream = CreateTestImage(100, 100);
        var operation = new CropOperation(X: 10, Y: 10, Width: 50, Height: 50);

        // Act
        using var resultStream = await _provider.ApplyAsync(
            inputStream, operation, _defaultOptions);

        // Assert
        resultStream.Should().NotBeNull();
        resultStream.Length.Should().BeGreaterThan(0);

        using var resultImage = await Image.LoadAsync<Rgba32>(resultStream);
        resultImage.Width.Should().Be(50);
        resultImage.Height.Should().Be(50);
    }

    [Fact]
    public async Task ApplyAsync_ResizeOperation_ChangesImageSize()
    {
        // Arrange
        using var inputStream = CreateTestImage(100, 100);
        var operation = new ResizeOperation(Width: 50, Height: 50, MaintainAspectRatio: false);

        // Act
        using var resultStream = await _provider.ApplyAsync(
            inputStream, operation, _defaultOptions);

        // Assert
        resultStream.Length.Should().BeGreaterThan(0);

        using var resultImage = await Image.LoadAsync<Rgba32>(resultStream);
        resultImage.Width.Should().Be(50);
        resultImage.Height.Should().Be(50);
    }

    [Fact]
    public async Task ApplyAsync_AddTextOperation_ProducesValidImage()
    {
        // Arrange
        using var inputStream = CreateTestImage(200, 200);
        var operation = new AddTextOperation(
            Text: "Test",
            X: 10,
            Y: 10,
            FontSize: 20,
            ColorHex: "#FF0000"
        );

        // Act
        using var resultStream = await _provider.ApplyAsync(
            inputStream, operation, _defaultOptions);

        // Assert
        resultStream.Length.Should().BeGreaterThan(0);

        // Verify it's a valid image
        using var resultImage = await Image.LoadAsync<Rgba32>(resultStream);
        resultImage.Width.Should().Be(200);
        resultImage.Height.Should().Be(200);
    }

    [Fact]
    public async Task ApplyAsync_RotateOperation_ProducesValidImage()
    {
        // Arrange
        using var inputStream = CreateTestImage(100, 100);
        var operation = new RotateOperation(Degrees: 90);

        // Act
        using var resultStream = await _provider.ApplyAsync(
            inputStream, operation, _defaultOptions);

        // Assert
        resultStream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ApplyAsync_GrayscaleFilter_ProducesValidImage()
    {
        // Arrange
        using var inputStream = CreateTestImage(100, 100);
        var operation = new ApplyFilterOperation(ImageFilter.Grayscale);

        // Act
        using var resultStream = await _provider.ApplyAsync(
            inputStream, operation, _defaultOptions);

        // Assert
        resultStream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ApplyAsync_BrightnessAdjustment_ProducesValidImage()
    {
        // Arrange
        using var inputStream = CreateTestImage(100, 100);
        var operation = new AdjustBrightnessOperation(Amount: 0.5f);

        // Act
        using var resultStream = await _provider.ApplyAsync(
            inputStream, operation, _defaultOptions);

        // Assert
        resultStream.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ApplyAsync_ResetsOutputStreamPosition()
    {
        // Arrange
        using var inputStream = CreateTestImage(100, 100);
        var operation = new CropOperation(X: 0, Y: 0, Width: 50, Height: 50);

        // Act
        using var resultStream = await _provider.ApplyAsync(
            inputStream, operation, _defaultOptions);

        // Assert
        resultStream.Position.Should().Be(0);
    }

    private static Stream CreateTestImage(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, Color.Blue);
        var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;
        return stream;
    }

    private static ImageEditOperation CreateOperation(Type operationType)
    {
        return operationType.Name switch
        {
            nameof(AddTextOperation) => new AddTextOperation("Test", 0, 0, 12, "#000000"),
            nameof(CropOperation) => new CropOperation(0, 0, 10, 10),
            nameof(ResizeOperation) => new ResizeOperation(100, 100),
            nameof(RotateOperation) => new RotateOperation(90),
            nameof(AdjustBrightnessOperation) => new AdjustBrightnessOperation(0.5f),
            nameof(AdjustContrastOperation) => new AdjustContrastOperation(0.5f),
            nameof(ApplyFilterOperation) => new ApplyFilterOperation(ImageFilter.Grayscale),
            nameof(MaskReplaceOperation) => new MaskReplaceOperation(Stream.Null, "test"),
            _ => throw new ArgumentException($"Unknown operation type: {operationType.Name}")
        };
    }
}
