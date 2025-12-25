using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public static class DevGPTStoreConfigParser
{
    // Serializes a list of StoreConfig objects to .devgpt format
    public static string Serialize(IEnumerable<StoreConfig> stores)
    {
        var sb = new StringBuilder();
        foreach (var store in stores)
        {
            sb.AppendLine($"Name: {store.Name}");
            sb.AppendLine($"Description: {store.Description}");
            sb.AppendLine($"Path: {store.Path}");
            sb.AppendLine($"FileFilters: {string.Join(",", store.FileFilters ?? Array.Empty<string>())}");
            sb.AppendLine($"SubDirectory: {store.SubDirectory}");
            sb.AppendLine($"ExcludePattern: {string.Join(",", store.ExcludePattern ?? Array.Empty<string>())}");
            sb.AppendLine(); // Empty line between objects
        }
        return sb.ToString().TrimEnd();
    }

    // Parses a .devgpt formatted string into a list of StoreConfig objects
    public static List<StoreConfig> Parse(string input)
    {
        var stores = new List<StoreConfig>();
        StoreConfig current = null;

        foreach (var line in input.Split(["\r\n", "\n"], StringSplitOptions.TrimEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                if (current != null)
                {
                    stores.Add(current);
                    current = null;
                }
                continue;
            }
            if (current == null) current = new StoreConfig();

            var sepIdx = trimmed.IndexOf(':');
            if (sepIdx < 0) continue; // skip malformed lines
            var key = trimmed.Substring(0, sepIdx).Trim();
            var value = trimmed.Substring(sepIdx + 1).Trim();

            switch (key)
            {
                case "Name": current.Name = value; break;
                case "Description": current.Description = value; break;
                case "Path": current.Path = value; break;
                case "FileFilters":
                    current.FileFilters = string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : value.Split(',').Select(item => item.Trim()).ToArray();
                    break;
                case "SubDirectory": current.SubDirectory = value; break;
                case "ExcludePattern":
                    current.ExcludePattern = string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : value.Split(',').Select(item => item.Trim()).ToArray();
                    break;
            }
        }
        if (current != null && !stores.Contains(current)) stores.Add(current); // add trailing
        return stores;
    }
}