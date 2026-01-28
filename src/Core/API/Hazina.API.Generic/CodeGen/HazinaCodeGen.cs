using Hazina.API.Generic.Configuration;

namespace Hazina.API.Generic.CodeGen;

/// <summary>
/// CLI tool for generating C# code from YAML entity definitions.
///
/// Usage:
///   hazina-codegen generate-project --yaml entities.yaml --output ./MyApi --name MyApi
///   hazina-codegen generate-entity --yaml entities.yaml --output ./Entities
///   hazina-codegen generate-controller --yaml entities.yaml --output ./Controllers
/// </summary>
public static class HazinaCodeGen
{
    /// <summary>
    /// Generate a complete project from YAML definitions.
    /// </summary>
    public static async Task<int> GenerateProjectAsync(
        string yamlPath,
        string outputPath,
        string projectName,
        bool extended = false)
    {
        try
        {
            Console.WriteLine($"Loading entities from: {yamlPath}");

            var loader = new EntityConfigurationLoader();
            var definitions = await loader.LoadFromFileAsync(yamlPath);

            var errors = loader.ValidateDefinitions(definitions);
            if (errors.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Validation errors:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  - {error}");
                }
                Console.ResetColor();
                return 1;
            }

            Console.WriteLine($"Found {definitions.Count()} entity definitions");

            var generator = new ProjectGenerator(projectName);
            var files = generator.GenerateProject(definitions, extended);

            // Create output directory
            Directory.CreateDirectory(outputPath);

            // Write all files
            foreach (var (relativePath, content) in files)
            {
                var fullPath = Path.Combine(outputPath, relativePath);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(fullPath, content);
                Console.WriteLine($"  Created: {relativePath}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nProject generated successfully at: {outputPath}");
            Console.WriteLine($"\nTo run:");
            Console.WriteLine($"  cd {outputPath}");
            Console.WriteLine($"  dotnet run");
            Console.ResetColor();

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    /// <summary>
    /// Generate only entity classes from YAML definitions.
    /// </summary>
    public static async Task<int> GenerateEntitiesAsync(
        string yamlPath,
        string outputPath,
        string @namespace = "Generated.Entities")
    {
        try
        {
            var loader = new EntityConfigurationLoader();
            var definitions = await loader.LoadFromFileAsync(yamlPath);

            var generator = new EntityCodeGenerator(@namespace);
            var files = generator.GenerateAllEntityClasses(definitions);

            Directory.CreateDirectory(outputPath);

            foreach (var (filename, content) in files)
            {
                var fullPath = Path.Combine(outputPath, filename);
                await File.WriteAllTextAsync(fullPath, content);
                Console.WriteLine($"  Created: {filename}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nGenerated {files.Count} entity classes");
            Console.ResetColor();

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    /// <summary>
    /// Generate only controller classes from YAML definitions.
    /// </summary>
    public static async Task<int> GenerateControllersAsync(
        string yamlPath,
        string outputPath,
        string controllersNamespace = "Generated.Controllers",
        string entitiesNamespace = "Generated.Entities",
        bool extended = false)
    {
        try
        {
            var loader = new EntityConfigurationLoader();
            var definitions = await loader.LoadFromFileAsync(yamlPath);

            var generator = new ControllerCodeGenerator(controllersNamespace, entitiesNamespace);
            var files = generator.GenerateAllControllers(definitions, extended);

            Directory.CreateDirectory(outputPath);

            foreach (var (filename, content) in files)
            {
                var fullPath = Path.Combine(outputPath, filename);
                await File.WriteAllTextAsync(fullPath, content);
                Console.WriteLine($"  Created: {filename}");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nGenerated {files.Count} controller classes");
            Console.ResetColor();

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    /// <summary>
    /// Print the generated code to console (for preview).
    /// </summary>
    public static async Task<int> PreviewAsync(string yamlPath)
    {
        try
        {
            var loader = new EntityConfigurationLoader();
            var definitions = await loader.LoadFromFileAsync(yamlPath);

            Console.WriteLine("=== Entity Definitions ===\n");

            foreach (var def in definitions)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Entity: {def.Name}");
                Console.ResetColor();
                Console.WriteLine($"  Plural: {def.GetPluralName()}");
                Console.WriteLine($"  Description: {def.Description}");
                Console.WriteLine($"  Fields:");
                foreach (var field in def.Fields)
                {
                    var req = field.Required ? " [REQUIRED]" : "";
                    Console.WriteLine($"    - {field.Name}: {field.Type}{req}");
                }
                Console.WriteLine($"  Features: CRUD={def.Features.Crud}, Search={def.Features.Search}, Embedding={def.Features.Embedding}");
                Console.WriteLine();
            }

            Console.WriteLine("=== Generated Entity Class (first) ===\n");
            var firstDef = definitions.First();
            var entityGen = new EntityCodeGenerator("MyApp.Entities");
            Console.WriteLine(entityGen.GenerateEntityClass(firstDef));

            Console.WriteLine("\n=== Generated Controller (first) ===\n");
            var ctrlGen = new ControllerCodeGenerator("MyApp.Controllers", "MyApp.Entities");
            Console.WriteLine(ctrlGen.GenerateMinimalController(firstDef));

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }
}
