# ShipExtract Privacy Policy

Last updated: June 2026

## What ShipExtract Does Not Collect

ShipExtract is a local-first desktop application.
We do not collect, store, or transmit your data.

Specifically:
- Your PDF documents are processed locally on your machine
- Extracted shipment data stays on your machine
- No usage analytics or telemetry is sent anywhere
- No account or registration is required

## What Leaves Your Machine

When using **Ollama (local mode)**: nothing leaves your machine.
All AI processing happens locally.

When using **Anthropic Claude**: the text extracted from your PDF is sent to
Anthropic's API for processing. This is subject to Anthropic's privacy policy
at https://www.anthropic.com/privacy.
Your PDF files themselves are never sent — only the extracted text.

## API Keys

Your Anthropic API key is stored securely in Windows Credential Manager.
It is never written to disk in plain text and never transmitted to ShipExtract
servers (there are none).

## Auto-Update Checks

If enabled, ShipExtract checks GitHub's public API for new releases.
This sends your current version number and a User-Agent string to GitHub.
No personal information is sent.
You can disable this in Settings → Updates.

## Local Storage

ShipExtract stores the following on your machine:
- Settings file: `%APPDATA%\ShipExtract\settings.json`
- Batch history: `%APPDATA%\ShipExtract\history\`
- Log files: `%APPDATA%\ShipExtract\logs\`
- Tessdata: `%APPDATA%\ShipExtract\tessdata\`

You can delete these at any time.

## Contact

For privacy questions: [your email here]
