using System;
using System.IO;
using System.Text.Json;
using System.Windows;
namespace DevGPT.App.Windows;
public partial class SettingsWindow : Window
    {
        private readonly string appSettingsPath = "appsettings.json";
        private dynamic originalConfig; // For cancel functionality
        public bool RestartRequired { get; private set; } = false;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(appSettingsPath))
                {
                    ErrorText.Text = $"Kan {appSettingsPath} niet vinden.";
                    return;
                }
                string json = File.ReadAllText(appSettingsPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("OpenAI", out var openAi))
                {
                    ApiKeyBox.Text = openAi.TryGetProperty("ApiKey", out var v1) ? v1.GetString() ?? string.Empty : string.Empty;
                    ModelBox.Text = openAi.TryGetProperty("Model", out var v2) ? v2.GetString() ?? string.Empty : string.Empty;
                    ImageModelBox.Text = openAi.TryGetProperty("ImageModel", out var v3) ? v3.GetString() ?? string.Empty : string.Empty;
                    EmbeddingModelBox.Text = openAi.TryGetProperty("EmbeddingModel", out var v4) ? v4.GetString() ?? string.Empty : string.Empty;
                }
                else
                {
                    ApiKeyBox.Text = string.Empty;
                    ModelBox.Text = string.Empty;
                    ImageModelBox.Text = string.Empty;
                    EmbeddingModelBox.Text = string.Empty;
                }
                originalConfig = JsonSerializer.Deserialize<object>(json);
                ErrorText.Text = "";
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"Fout bij laden: {ex.Message}";
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;
            // Validatie
            if (string.IsNullOrWhiteSpace(ApiKeyBox.Text))
            {
                ErrorText.Text = "Api Key mag niet leeg zijn.";
                return;
            }
            if (string.IsNullOrWhiteSpace(ModelBox.Text))
            {
                ErrorText.Text = "Model mag niet leeg zijn.";
                return;
            }
            if (string.IsNullOrWhiteSpace(ImageModelBox.Text))
            {
                ErrorText.Text = "ImageModel mag niet leeg zijn.";
                return;
            }
            if (string.IsNullOrWhiteSpace(EmbeddingModelBox.Text))
            {
                ErrorText.Text = "EmbeddingModel mag niet leeg zijn.";
                return;
            }
            try
            {
                // Load, wijzig, save met pretty indent
                string json = File.ReadAllText(appSettingsPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement.Clone();
                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    bool openAiFound = false;
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Name == "OpenAI")
                        {
                            openAiFound = true;
                            writer.WritePropertyName("OpenAI");
                            writer.WriteStartObject();
                            writer.WriteString("ApiKey", ApiKeyBox.Text.Trim());
                            writer.WriteString("Model", ModelBox.Text.Trim());
                            writer.WriteString("ImageModel", ImageModelBox.Text.Trim());
                            writer.WriteString("EmbeddingModel", EmbeddingModelBox.Text.Trim());
                            writer.WriteEndObject();
                        }
                        else
                        {
                            prop.WriteTo(writer);
                        }
                    }
                    if (!openAiFound)
                    {
                        // Voeg OpenAI toe als hij niet bestond
                        writer.WritePropertyName("OpenAI");
                        writer.WriteStartObject();
                        writer.WriteString("ApiKey", ApiKeyBox.Text.Trim());
                        writer.WriteString("Model", ModelBox.Text.Trim());
                        writer.WriteString("ImageModel", ImageModelBox.Text.Trim());
                        writer.WriteString("EmbeddingModel", EmbeddingModelBox.Text.Trim());
                        writer.WriteEndObject();
                    }
                    writer.WriteEndObject();
                }
                File.WriteAllText(appSettingsPath, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ErrorText.Text = "Fout bij opslaan: " + ex.Message;
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            // Herlaad naar originele situatie
            LoadSettings();
            DialogResult = false;
            Close();
        }
    }

