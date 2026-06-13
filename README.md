# ShipExtract

**Convert shipping PDFs to structured Excel data — instantly.**

ShipExtract uses AI to extract tracking numbers, addresses, weights, and customs
data from any shipping document. Works offline with Ollama or with Anthropic
Claude for maximum accuracy.

## Features

- Drag-and-drop batch processing
- Supports DHL, FedEx, UPS, TNT, DPD, GLS and more
- Export to Excel and CSV with confidence scoring
- Free with Ollama (local AI) — no API key needed
- Optional: Anthropic Claude for higher accuracy (~$0.002/doc)
- OCR support for scanned PDFs (English, German, French)
- Batch history — reload and re-export previous sessions
- Custom extraction fields for your internal ERP format

## System Requirements

- Windows 10/11 (x64)
- 8 GB RAM recommended (16 GB for Ollama)
- Internet connection (optional — only needed for Claude AI)

## Quick Start

1. Download ShipExtract from [Gumroad link]
2. Run `ShipExtract.UI.exe`
3. On first launch: choose Ollama (free) or Anthropic
4. Drop your PDFs onto the queue
5. Click **Process** → **Export Excel**

## Getting Started with Ollama (Free)

1. Download Ollama from https://ollama.com
2. Run: `ollama pull mistral`
3. ShipExtract will detect it automatically

## Getting Started with Anthropic Claude

1. Get an API key from https://console.anthropic.com
   (minimum $5 credit, ~$0.002 per document)
2. Enter your key in ShipExtract Settings

## Building from Source

Prerequisites: .NET 8 SDK, Git

```
git clone https://github.com/davidkuts/ship-extract.git
cd ship-extract
dotnet build ShipExtract.sln
dotnet test
dotnet run --project src/ShipExtract.UI/ShipExtract.UI.csproj
```

## Release Build

```
.\publish.ps1 -Target Gumroad -Version 1.0.0
```

## Architecture

Four-layer clean architecture:
- **Domain**: models, interfaces, business rules
- **Application**: orchestration, batch processing pipeline
- **Infrastructure**: PDF parsing, OCR, AI providers, exports
- **UI**: WPF MVVM with CommunityToolkit.Mvvm

## License

[Your chosen license]

## Acknowledgements

Built with: PdfPig, Tesseract, ClosedXML, CsvHelper,
Serilog, CommunityToolkit.Mvvm, Anthropic.SDK
