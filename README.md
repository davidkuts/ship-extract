# ShipExtract

ShipExtract is a Windows desktop application that uses Claude AI to automatically extract structured shipment data from PDF documents — air waybills, bills of lading, commercial invoices, packing lists, and courier labels — and export the results to CSV or Excel.

## Requirements

- Windows 10/11 x64
- [Anthropic API key](https://console.anthropic.com) (free tier works)
- Optional: Tesseract `eng.traineddata` for scanned (image-based) PDF support

## Setup

1. Run `ShipExtract.UI.exe`
2. On first launch, enter your Anthropic API key when prompted
   (get one free at [console.anthropic.com](https://console.anthropic.com))
3. **Optional OCR setup** — to process scanned PDFs:
   - Download [eng.traineddata](https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata)
   - Place the file in `%APPDATA%\ShipExtract\tessdata\`
   - Restart ShipExtract

## Usage

1. **Drag and drop** PDF files onto the drop zone (or click to browse)
2. Set an **output folder** for exports
3. Click **Process Files** — each file is extracted concurrently
4. Click a completed item to **preview extracted fields**
5. Click **Export CSV** or **Export Excel** to save results

## Architecture

ShipExtract follows a clean four-layer architecture:

| Project | Layer | Responsibility |
|---|---|---|
| `ShipExtract.Domain` | Domain | Entities, interfaces, enums, validators |
| `ShipExtract.Application` | Application | Orchestration (ExtractionPipeline, BatchProcessingService) |
| `ShipExtract.Infrastructure` | Infrastructure | PDF parsing (PdfPig), OCR (Tesseract), AI (Anthropic SDK), export (CsvHelper, ClosedXML) |
| `ShipExtract.UI` | Presentation | WPF MVVM UI (CommunityToolkit.Mvvm) |

## Building from Source

```bash
git clone <repo-url>
cd ship-extract
dotnet build ShipExtract.sln
dotnet test
.\publish.ps1
```

## Acknowledgements

- [Claude AI](https://anthropic.com) — AI-powered data extraction
- [PdfPig](https://github.com/UglyToad/PdfPig) — PDF text extraction
- [Tesseract](https://github.com/charlesw/tesseract) — OCR engine
- [ClosedXML](https://github.com/ClosedXML/ClosedXML) — Excel export
- [CsvHelper](https://joshclose.github.io/CsvHelper/) — CSV export
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM framework
