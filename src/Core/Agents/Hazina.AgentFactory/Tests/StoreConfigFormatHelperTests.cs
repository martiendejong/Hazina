//using System;
//using System.Collections.Generic;
//using Xunit;

//public class StoreConfigFormatHelperTests
//{
//    // Correct multiline JSON string as verbatim string with proper double-quote escaping
//    private static string Normalize(string s)
//        => (s ?? "").Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd();

//    private const string ValidJson = "[\n  {\n    \"Name\": \"Codebase\",\n    \"Description\": \"Main codebase store\",\n    \"Path\": \"C:/Projects/Hazina\",\n    \"FileFilters\": [\"*.cs\", \"*.js\"],\n    \"SubDirectory\": \"src\",\n    \"ExcludePattern\": [\"bin\", \"obj\"]\n  }\n]";

//    private const string ValidDevGPT = "Name: Codebase\nDescription: Main codebase store\nPath: C:/Projects/Hazina\nFileFilters: *.cs,*.js\nSubDirectory: src\nExcludePattern: bin,obj";

//    private const string InvalidJson = "{";
//    private const string InvalidDevGPT = "Name Codebase\nDescription Main codebase store";

//    [Fact]
//    public void Test_IsLikelyJson_CorrectlyIdentifiesJson()
//    {
//        Assert.True(StoreConfigFormatHelper.IsLikelyJson(ValidJson));
//        Assert.True(StoreConfigFormatHelper.IsLikelyJson(@"  [\n{}\n]   "));
//        Assert.True(StoreConfigFormatHelper.IsLikelyJson("{\"Name\":\"x\"}"));

//        Assert.False(StoreConfigFormatHelper.IsLikelyJson(ValidDevGPT));
//        Assert.False(StoreConfigFormatHelper.IsLikelyJson("Name: test\nDescription: test"));
//        Assert.False(StoreConfigFormatHelper.IsLikelyJson("")); // blank
//        Assert.False(StoreConfigFormatHelper.IsLikelyJson(null));
//    }

//    [Fact]
//    public void Test_IsLikelyJson_Heuristics_Ambiguous()
//    {
//        // This should return false, not json
//        Assert.False(StoreConfigFormatHelper.IsLikelyJson("Name: Something\nPath: Value"));
//        // Edge: JSON string with a colon but not starting with { or [
//        Assert.False(StoreConfigFormatHelper.IsLikelyJson("\"Name\": Something"));
//    }

//    [Fact]
//    public void Test_AutoDetectAndParse_ParsesJsonSuccessfully()
//    {
//        var configs = StoreConfigFormatHelper.AutoDetectAndParse(ValidJson);
//        Assert.Single(configs);
//        var cfg = configs[0];
//        Assert.Equal("Codebase", cfg.Name);
//        Assert.Equal("Main codebase store", cfg.Description);
//        Assert.Equal("C:/Projects/Hazina", cfg.Path);
//        Assert.Equal(new[] { "*.cs", "*.js" }, cfg.FileFilters);
//        Assert.Equal("src", cfg.SubDirectory);
//        Assert.Equal(new[] { "bin", "obj" }, cfg.ExcludePattern);
//    }

//    [Fact]
//    public void Test_AutoDetectAndParse_ParsesDevGPTSuccessfully()
//    {
//        var configs = StoreConfigFormatHelper.AutoDetectAndParse(ValidDevGPT);
//        Assert.Single(configs);
//        var cfg = configs[0];
//        Assert.Equal("Codebase", cfg.Name);
//        Assert.Equal("Main codebase store", cfg.Description);
//        Assert.Equal("C:/Projects/Hazina", cfg.Path);
//        Assert.Equal(new[] { "*.cs", "*.js" }, cfg.FileFilters);
//        Assert.Equal("src", cfg.SubDirectory);
//        Assert.Equal(new[] { "bin", "obj" }, cfg.ExcludePattern);
//    }

//    [Fact]
//    public void Test_AutoDetectAndParse_InvalidInputs_ThrowsOrReturnsEmpty()
//    {
//        Assert.ThrowsAny<Exception>(() => StoreConfigFormatHelper.AutoDetectAndParse(InvalidJson));
//        Assert.ThrowsAny<Exception>(() => StoreConfigFormatHelper.AutoDetectAndParse(InvalidDevGPT));
//    }

//    [Fact]
//    public void Test_DevGPT_And_Json_Parity_RoundTrip()
//    {
//        var fromDevgpt = StoreConfigFormatHelper.AutoDetectAndParse(ValidDevGPT);
//        var toJson = System.Text.Json.JsonSerializer.Serialize(fromDevgpt);
//        var fromJson = StoreConfigFormatHelper.AutoDetectAndParse(toJson);
//        Assert.Single(fromJson);
//        Assert.Equal("Codebase", fromJson[0].Name);

//        // serialize to Hazina and back
//        var toDevgpt = DevGPTStoreConfigParser.Serialize(fromJson);
//        // Compare with normalized baseline (ignore trailing newlines and newline types)
//        Assert.Equal(Normalize(ValidDevGPT), Normalize(toDevgpt));
//        var backToObjs = StoreConfigFormatHelper.AutoDetectAndParse(toDevgpt);
//        Assert.Single(backToObjs);
//        Assert.Equal("Codebase", backToObjs[0].Name);
//    }
//}
