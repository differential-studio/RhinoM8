# RhinoM8 🦏

RhinoM8 is an AI-powered plugin for Rhino 8 that helps you create 3D models through natural language prompts and image uploads. It converts your text descriptions into Python scripts and 3D models.

👉 [Download RhinoM8](https://differential.fillout.com/t/rRV6AbGw1hus)

## Software Requirements

### Required Software
- Rhino 8 for Windows (Mac version not supported)
- Windows 10 or later
- .NET Framework 4.8 or later
- Python for Rhino (included with Rhino 8)

### System Requirements
- Operating System: Windows 10/11 (64-bit)
- RAM: 8GB minimum (16GB recommended)
- Internet connection required for AI services

### API Keys Required
- OpenAI API key (for GPT-4)
- Anthropic API key (for Claude)
- X.AI API key (for Grok)
- Meshy API key (for 3D generation)

## Features 🚀

- **Text-to-3D**: Generate 3D models from text descriptions using Meshy.ai
- **Image-to-3D**: Convert images into detailed 3D models using Meshy.ai
- **Text-to-Code** (Experimental): Convert text descriptions into executable RhinoScript Python code
- **Multi-Model Support** for code generation:
  - OpenAI
  - Anthropic Claude
  - Grok xAI

## How It Works 🔧

1. Choose your generation type (Text-to-3D, Image-to-3D, or Text-to-Code)
2. For Text-to-3D: Describe what you want to create in plain English
3. For Image-to-3D: Upload an image of an object you want to convert to 3D
4. For Text-to-Code (Experimental): Describe the geometry you want to create
5. The AI generates your content:
   - 3D models are imported automatically
   - Code is executed directly in Rhino

## Installation 🔌

1. Download the latest release
2. Install the .rhp file in Rhino 8
3. Type `RhinoM8` in Rhino to start the plugin
4. Configure your API keys in the settings

## API Keys 🔑

You'll need API keys for the services you want to use:
- [Meshy.ai API key](www.meshy.ai?via=differential) (for Text-to-3D and Image-to-3D models)
- [OpenAI API key](https://platform.openai.com/api-keys) (for code generation)
- [Anthropic Claude API key](https://console.anthropic.com/account/keys) (for code generation)
- [Grok API key](https://x.ai/api) (for code generation)

## Usage Examples 💡

Text-to-3D Generation:
```
"A detailed architectural column with Corinthian capital"
```

Image-to-3D Generation:
```
Upload any image of a 3D object to convert it into a detailed 3D model
(Works best with simple objects, but can handle complex scenes. Works best with consistent lighting and a plain background.)
```

Text-to-Code Generation (Experimental, works only for simple geometries):
```
"Create a fractal tree" or "Populate a surface with random spheres"
```

## File Locations 📁

RhinoM8 stores its files in the following locations:

### Main Directory
`%AppData%\RhinoM8\`

### Components
- **3D Models**: `%AppData%\RhinoM8\Models\`
  - Generated .glb files
  - Thumbnails: `thumbnails` subfolder
  - Textures: `[modelname]_textures` subfolders

- **Settings & History**: Stored in Rhino plugin settings

## Development 🛠️

Built with:
- .NET 7.0/4.8
- RhinoCommon SDK
- Windows Forms
- Newtonsoft.Json

## Contributing 🤝

Contributions are welcome! Feel free to:
- Submit bug reports
- Propose new features
- Create pull requests
- Collaborate on AI/Architecture projects

Want to collaborate on innovative AI-powered architectural tools? 
We're always excited to work with like-minded developers and architects!

## Disclaimer ⚠️

### Terms of Use and Liability
By using RhinoM8, you acknowledge and agree to the following:

1. **Experimental Status**
   - RhinoM8 is an experimental tool in active development
   - The software is provided "AS IS", without warranty of any kind
   - Features may be unstable, change, or stop working without notice

2. **API Usage & Costs**
   - Users are solely responsible for managing their API keys and associated costs
   - We are not responsible for any API charges, credit consumption, or related expenses
   - We do not guarantee the efficiency of API usage or credit consumption
   - Users should monitor their own API usage and set appropriate limits

3. **No Warranty**
   - We make no warranties about the completeness, reliability, or accuracy of the tool
   - We are not responsible for any damage to your computer, software, or business
   - We do not guarantee compatibility with any specific Rhino version or Windows build

4. **Limitation of Liability**
   - We shall not be liable for any direct, indirect, incidental, or consequential damages
   - This includes but is not limited to:
     - Loss of data or profits
     - Business interruption
     - API credits or costs
     - Failed or incorrect generations
     - System crashes or file corruption

5. **User Responsibility**
   - Users are responsible for backing up their data
   - Users should test the tool in a non-critical environment first
   - Users acknowledge they use this tool at their own risk

By using RhinoM8, you explicitly agree to these terms and acknowledge these limitations.

For best results, we recommend using Meshy.ai for 3D model generation, as it has proven to be the most reliable feature.

## License 📄

This project is open source and available under the MIT License.

## Contact & Support 📧

Having issues or questions? Want to share your experience or collaborate? 
Reach out to us at hello@differential.works

---

Made for Rhino community, with ❤️ by DIFFERENTIAL: https://www.differential.studio/