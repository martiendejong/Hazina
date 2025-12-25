# DevGPT.HuggingFace

This project provides integration with HuggingFace's Inference API for use with DevGPT. The project structure and API mirror the DevGPT.OpenAI project for consistency and ease of use.

## Key Files
- `DevGPT.HuggingFace.csproj`: Project file with dependencies and references.
- `HuggingFaceClientWrapper.cs`: Implements ILLMClient and interacts with HuggingFace endpoints.
- `HuggingFaceConfig.cs`: Configuration class for HuggingFace API setup.
- `DevGPTHuggingFaceExtensions.cs`: Helper extensions to map DevGPT types to HuggingFace formats.

## TODO
- Implement actual HuggingFace API integration in `HuggingFaceClientWrapper`.
