# Changelog

All notable changes to ShipExtract are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [1.0.0] — 2026-06-12

### First Public Release

#### Core Features
- PDF ingestion with PdfPig text extraction
- Tesseract OCR fallback for scanned PDFs (English, German, French)
- Dual AI provider: Anthropic Claude and Ollama (local, free)
- Carrier detection: DHL, FedEx, UPS, TNT, DPD, GLS and more
- Carrier-specific extraction prompts for higher accuracy
- Export to Excel (.xlsx) and CSV

#### Data Quality
- 6-stage text pre-processing pipeline
- Confidence threshold filtering with Review sheet
- Soft validation warnings vs hard errors
- Mistral JSON fallback extraction

#### User Experience
- Drag-and-drop PDF input with multi-file support
- Batch processing queue with live progress
- Result preview panel with extracted field display
- Batch history — persist and re-export previous sessions
- Custom extraction fields
- Onboarding tour for new users
- Keyboard shortcuts (F5, Ctrl+E, Ctrl+H, Ctrl+,)
- Right-click context menu on queue items

#### Settings & Configuration
- Windows Credential Manager for API key storage
- Confidence threshold slider (default 60%)
- Export filename prefix customisation
- Window size and position persistence
- Auto-update notifications via GitHub releases

#### Reliability
- Password-protected PDF detection
- Corrupt/invalid PDF detection
- Large PDF smart truncation
- Anthropic rate limit retry with backoff
- Network offline detection
- User-friendly error messages
