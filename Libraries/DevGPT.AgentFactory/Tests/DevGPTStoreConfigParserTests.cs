//using System;
//using System.Collections.Generic;
//using Xunit;

//public class DevGPTStoreConfigParserTests
//{
//    private const string DevGPTExample = @"Name: Foo
//Description: A test config
//Path: test/path
//FileFilters: *.cs,*.txt
//SubDirectory: subfolder
//ExcludePattern: bin,obj

//Name: Bar
//Description: Another one
//Path: bar
//FileFilters: *.json
//SubDirectory: 
//ExcludePattern: 
//";

//    [Fact]
//    public void Test_Parse_Parses_Multiple_Objects()
//    {
//        var result = DevGPTStoreConfigParser.Parse(DevGPTExample);
//        Assert.Equal(2, result.Count);
//        Assert.Equal("Foo", result[0].Name);
//        Assert.Equal("Bar", result[1].Name);
//    }

//    [Fact]
//    public void Test_Serialize_RoundTrip_Preserves_Data()
//    {
//        var configs = new List<StoreConfig> {
//            new StoreConfig {
//                Name = "X", Description = "D", Path = "p/x", FileFilters = new[] {"*.cs","*.txt"}, SubDirectory = "s", ExcludePattern = new []{"bin", "obj"}
//            },
//            new StoreConfig {
//                Name = "Y", Description = "E", Path = "p/y", FileFilters = new[] {"*.*"}, SubDirectory = "", ExcludePattern = Array.Empty<string>()
//            }
//        };
//        var serialized = DevGPTStoreConfigParser.Serialize(configs);
//        var reparsed = DevGPTStoreConfigParser.Parse(serialized);
//        Assert.Equal(2, reparsed.Count);
//        Assert.Equal("X", reparsed[0].Name);
//        Assert.Equal("Y", reparsed[1].Name);
//        Assert.Equal("p/y", reparsed[1].Path);
//        Assert.Equal("E", reparsed[1].Description);
//        Assert.Equal(new[] {"*.*"}, reparsed[1].FileFilters);
//    }

//    [Fact]
//    public void Test_Parse_EmptyOrNull_Returns_Empty()
//    {
//        var r = DevGPTStoreConfigParser.Parse("");
//        Assert.Empty(r);
//        var r2 = DevGPTStoreConfigParser.Parse("\n\n");
//        Assert.Empty(r2);
//    }
//}
