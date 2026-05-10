using Hazina.Generator;

namespace Hazina.Generator.Tests;

public class StoreSafetyPolicyTests
{
    [Fact]
    public void Validate_AllowedExtension_DoesNotThrow()
    {
        var policy = new StoreSafetyPolicy();

        // Should not throw for any default-allowed extension
        policy.Validate("docs/readme.md", "Hello world");
        policy.Validate("data/config.json", "{}");
        policy.Validate("notes.txt", "Some notes");
        policy.Validate("src/Program.cs", "using System;");
        policy.Validate("app/index.ts", "export default {};");
        policy.Validate("styles/main.css", "body {}");
        policy.Validate("template.html", "<html></html>");
        policy.Validate("config.xml", "<root/>");
        policy.Validate("config.yaml", "key: value");
        policy.Validate("config.yml", "key: value");
    }

    [Fact]
    public void Validate_DisallowedExtension_Throws()
    {
        var policy = new StoreSafetyPolicy();

        var ex = Assert.Throws<StoreSafetyPolicyViolationException>(
            () => policy.Validate("malware.exe", "binary content"));

        Assert.Contains(".exe", ex.Message);
        Assert.Contains("not in the allowed extensions", ex.Message);
    }

    [Theory]
    [InlineData("script.bat")]
    [InlineData("library.dll")]
    [InlineData("archive.zip")]
    [InlineData("image.png")]
    [InlineData("binary.bin")]
    [InlineData("shell.sh")]
    [InlineData("script.ps1")]
    public void Validate_VariousDisallowedExtensions_Throws(string filename)
    {
        var policy = new StoreSafetyPolicy();

        Assert.Throws<StoreSafetyPolicyViolationException>(
            () => policy.Validate(filename, "content"));
    }

    [Fact]
    public void Validate_ExtensionCheckIsCaseInsensitive()
    {
        var policy = new StoreSafetyPolicy();

        // Should not throw — .JSON should match .json
        policy.Validate("data.JSON", "{}");
        policy.Validate("readme.MD", "# Title");
        policy.Validate("file.Txt", "hello");
    }

    [Fact]
    public void Validate_FileWithoutExtension_IsAllowed()
    {
        var policy = new StoreSafetyPolicy();

        // Files without extensions like Dockerfile, Makefile, .gitignore
        policy.Validate("Dockerfile", "FROM node:18");
        policy.Validate("Makefile", "all:");
    }

    [Fact]
    public void Validate_EmptyAllowedExtensions_AllowsEverything()
    {
        var policy = new StoreSafetyPolicy
        {
            AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };

        // Should not throw for any extension when allowlist is empty
        policy.Validate("script.exe", "binary");
        policy.Validate("archive.zip", "data");
    }

    [Fact]
    public void Validate_CustomAllowedExtensions()
    {
        var policy = new StoreSafetyPolicy
        {
            AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".py", ".rs" }
        };

        policy.Validate("main.py", "print('hello')");
        policy.Validate("lib.rs", "fn main() {}");

        Assert.Throws<StoreSafetyPolicyViolationException>(
            () => policy.Validate("app.cs", "using System;"));
    }

    [Fact]
    public void Validate_ContentWithinSizeLimit_DoesNotThrow()
    {
        var policy = new StoreSafetyPolicy
        {
            MaxFileSizeBytes = 1024 // 1 KB
        };

        policy.Validate("small.txt", "Hello world");
    }

    [Fact]
    public void Validate_ContentExceedsSizeLimit_Throws()
    {
        var policy = new StoreSafetyPolicy
        {
            MaxFileSizeBytes = 100 // 100 bytes
        };

        var largeContent = new string('x', 200);

        var ex = Assert.Throws<StoreSafetyPolicyViolationException>(
            () => policy.Validate("large.txt", largeContent));

        Assert.Contains("exceeds the maximum allowed size", ex.Message);
        Assert.Contains("100", ex.Message);
    }

    [Fact]
    public void Validate_ContentExactlyAtLimit_DoesNotThrow()
    {
        // 10 ASCII chars = 10 bytes in UTF-8
        var policy = new StoreSafetyPolicy
        {
            MaxFileSizeBytes = 10
        };

        policy.Validate("exact.txt", "0123456789");
    }

    [Fact]
    public void Validate_ContentOneByteOverLimit_Throws()
    {
        var policy = new StoreSafetyPolicy
        {
            MaxFileSizeBytes = 10
        };

        Assert.Throws<StoreSafetyPolicyViolationException>(
            () => policy.Validate("over.txt", "01234567890")); // 11 bytes
    }

    [Fact]
    public void Validate_DefaultMaxFileSize_Is10MB()
    {
        var policy = new StoreSafetyPolicy();

        Assert.Equal(10 * 1024 * 1024, policy.MaxFileSizeBytes);
    }

    [Fact]
    public void Validate_DisabledMaxFileSize_AllowsAnySize()
    {
        var policy = new StoreSafetyPolicy
        {
            MaxFileSizeBytes = 0 // disabled
        };

        var hugeContent = new string('x', 20_000_000); // 20 MB
        policy.Validate("huge.txt", hugeContent);
    }

    [Fact]
    public void Validate_NegativeMaxFileSize_DisablesCheck()
    {
        var policy = new StoreSafetyPolicy
        {
            MaxFileSizeBytes = -1
        };

        var content = new string('x', 1000);
        policy.Validate("file.txt", content);
    }

    [Fact]
    public void Validate_MultiBytUtf8Characters_CountsBytesCorrectly()
    {
        var policy = new StoreSafetyPolicy
        {
            MaxFileSizeBytes = 10
        };

        // Each emoji is 4 bytes in UTF-8. 3 emojis = 12 bytes > 10 limit
        var content = "\U0001F600\U0001F600\U0001F600"; // 3 emoji faces

        Assert.Throws<StoreSafetyPolicyViolationException>(
            () => policy.Validate("emoji.txt", content));
    }

    [Fact]
    public void Validate_PolicyDisabled_SkipsAllChecks()
    {
        var policy = new StoreSafetyPolicy
        {
            Enabled = false,
            MaxFileSizeBytes = 1, // would normally fail
            AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".md" }
        };

        // Would violate both extension and size — but policy is disabled
        policy.Validate("script.exe", new string('x', 1000));
    }

    [Fact]
    public void ValidateMove_AllowedExtension_DoesNotThrow()
    {
        var policy = new StoreSafetyPolicy();

        policy.ValidateMove("docs/new-location.md");
        policy.ValidateMove("data/config.json");
    }

    [Fact]
    public void ValidateMove_DisallowedExtension_Throws()
    {
        var policy = new StoreSafetyPolicy();

        Assert.Throws<StoreSafetyPolicyViolationException>(
            () => policy.ValidateMove("scripts/malware.exe"));
    }

    [Fact]
    public void ValidateMove_PolicyDisabled_SkipsChecks()
    {
        var policy = new StoreSafetyPolicy { Enabled = false };

        policy.ValidateMove("script.exe");
    }

    [Fact]
    public void DefaultPolicy_HasExpectedExtensions()
    {
        var policy = new StoreSafetyPolicy();

        // Verify all documented default extensions are present
        var expected = new[] { ".json", ".md", ".txt", ".cs", ".ts", ".js",
            ".html", ".css", ".xml", ".yaml", ".yml",
            ".csv", ".svg", ".razor", ".csproj", ".sln",
            ".props", ".targets", ".config", ".gitignore" };

        foreach (var ext in expected)
        {
            Assert.Contains(ext, policy.AllowedExtensions);
        }
    }

    [Fact]
    public void Validate_PathWithSubdirectories_ExtractsExtensionCorrectly()
    {
        var policy = new StoreSafetyPolicy();

        policy.Validate("deep/nested/path/file.json", "{}");

        Assert.Throws<StoreSafetyPolicyViolationException>(
            () => policy.Validate("deep/nested/path/file.exe", "binary"));
    }
}
