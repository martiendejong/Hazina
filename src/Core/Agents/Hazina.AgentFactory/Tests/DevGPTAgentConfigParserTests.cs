//using System;
//using System.Collections.Generic;
//using System.IO;
//using Xunit;

//public class DevGPTAgentConfigParserTests
//{
//    [Fact]
//    public void Serialize_And_Parse_RoundTrip_Works()
//    {
//        var agents = new List<AgentConfig>()
//        {
//            new AgentConfig {
//                Name = "TestAgent",
//                Description = "A test agent",
//                Prompt = "Hello!\nHow can I help?",
//                Stores = new List<StoreRef>{ new StoreRef { Name = "Codebase", Write = true }, new StoreRef { Name = "Docs", Write = false } },
//                Functions = new List<string> { "build", "git" },
//                CallsAgents = new List<string> { "CodeReviewer" },
//                CallsFlows = new List<string> { "MyFlow" },
//                ExplicitModify = true
//            }
//        };
//        string text = DevGPTAgentConfigParser.Serialize(agents);
//        var parsed = DevGPTAgentConfigParser.Parse(text);
//        Assert.Single(parsed);
//        Assert.Equal("TestAgent", parsed[0].Name);
//        Assert.Equal("A test agent", parsed[0].Description);
//        Assert.Equal("Hello!\nHow can I help?", parsed[0].Prompt.Replace("\r",""));
//        Assert.Equal(2, parsed[0].Stores.Count);
//        Assert.Equal("Codebase", parsed[0].Stores[0].Name);
//        Assert.True(parsed[0].Stores[0].Write);
//        Assert.Equal("Docs", parsed[0].Stores[1].Name);
//        Assert.False(parsed[0].Stores[1].Write);
//        Assert.Equal(new List<string>{"build","git"}, parsed[0].Functions);
//        Assert.Equal(new List<string>{"CodeReviewer"}, parsed[0].CallsAgents);
//        Assert.Equal(new List<string>{"MyFlow"}, parsed[0].CallsFlows);
//        Assert.True(parsed[0].ExplicitModify);
//    }

//    [Fact]
//    public void Parse_InvalidBool_ExplicitModify_DefaultsFalse()
//    {
//        string data = @"
//        Name: Agent1
//        Description: blah
//        Prompt: test
//        Stores: StoreA|true
//        Functions: run
//        CallsAgents: x
//        ExplicitModify: NotABool
//        ";
//        var agents = DevGPTAgentConfigParser.Parse(data);
//        Assert.Single(agents);
//        Assert.False(agents[0].ExplicitModify); // Should default to false
//    }

//    [Fact]
//    public void Parse_Handles_Empty_Fields()
//    {
//        string input = @"
//        Name: AGENT_X
//        Description:
//        Prompt:
//        Stores:
//        Functions:
//        CallsAgents:
//        ExplicitModify:
//        ";
//        var agents = DevGPTAgentConfigParser.Parse(input);
//        Assert.Single(agents);
//        Assert.Equal("AGENT_X", agents[0].Name);
//        Assert.Empty(agents[0].Functions);
//        Assert.Empty(agents[0].CallsAgents);
//        Assert.Empty(agents[0].CallsFlows);
//        Assert.Empty(agents[0].Stores);
//        Assert.False(agents[0].ExplicitModify);
//    }
//    [Fact]
//    public void SaveToFile_And_LoadFromFile_Works() {
//        var file = Path.GetTempFileName();
//        try
//        {
//            var agents = new List<AgentConfig>()
//            {
//                new AgentConfig {
//                    Name = "T",
//                    Description = "D",
//                    Prompt = "P\nL",
//                    Stores = new List<StoreRef>() { new StoreRef { Name = "Test", Write = false } },
//                    Functions = new List<string> { },
//                    CallsAgents = new List<string> { },
//                    CallsFlows = new List<string> { },
//                    ExplicitModify = false
//                }
//            };
//            DevGPTAgentConfigParser.SaveToFile(agents, file);
//            var loaded = DevGPTAgentConfigParser.LoadFromFile(file);
//            Assert.Single(loaded);
//            Assert.Equal("T", loaded[0].Name);
//            Assert.Equal("P\nL", loaded[0].Prompt);
//        }
//        finally
//        {
//            File.Delete(file);
//        }
//    }
//    [Fact]
//    public void ConvertJsonToDevGptFile_Works()
//    {
//        var agent = new AgentConfig
//        {
//            Name = "ConvertedAgent",
//            Description = "From JSON",
//            Prompt = "Go",
//            Stores = new List<StoreRef>{ new StoreRef { Name = "Code", Write = true } },
//            Functions = new List<string>{ "a" },
//            CallsAgents = new List<string>{ },
//            CallsFlows = new List<string> { },
//            ExplicitModify = true
//        };
//        var list = new List<AgentConfig> { agent };
//        string json = System.Text.Json.JsonSerializer.Serialize(list);
//        var file = Path.GetTempFileName();
//        try
//        {
//            DevGPTAgentConfigParser.ConvertJsonToDevGptFile(json, file);
//            var text = File.ReadAllText(file);
//            Assert.Contains("Name: ConvertedAgent", text);
//            var loaded = DevGPTAgentConfigParser.LoadFromFile(file);
//            Assert.Single(loaded);
//            Assert.Equal("ConvertedAgent", loaded[0].Name);
//        }
//        finally { File.Delete(file); }
//    }
//}
