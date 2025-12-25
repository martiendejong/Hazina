# Hazina.LLMs.HuggingFace

This project provides integration with HuggingFace's Inference API for use with Hazina. The project structure and API mirror the Hazina.LLMs.OpenAI project for consistency and ease of use.

## Key Files
- `Hazina.LLMs.HuggingFace.csproj`: Project file with dependencies and references.
- `HuggingFaceClientWrapper.cs`: Implements ILLMClient and interacts with HuggingFace endpoints.
- `HuggingFaceConfig.cs`: Configuration class for HuggingFace API setup.
- `HazinaHuggingFaceExtensions.cs`: Helper extensions to map Hazina types to HuggingFace formats.

## TODO
- Implement actual HuggingFace API integration in `HuggingFaceClientWrapper`.
