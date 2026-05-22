# TLD-Save-Editor
Edit The Long Dark save files

Downloads from ModDB: http://www.moddb.com/mods/the-long-dark-save-editor-2/downloads

## Cross-platform web editor (Linux / macOS / Windows)

The original editor is a Windows-only WPF app. This repository also includes a
cross-platform port under `src/`: a portable core library (`TldSaveEditor.Core`)
and a small local web app (`TldSaveEditor.Server`) that runs in your browser.

**Requirements:** the [.NET 10 SDK](https://dotnet.microsoft.com/download).

**Run it:**

- Linux / macOS: `./run.sh`
- Windows: `run.cmd` (double-click) — or any platform: `dotnet run --project src/TldSaveEditor.Server -c Release`

It builds on first run, serves `http://127.0.0.1:5173` (loopback only) and opens
your browser. Your save folder is detected automatically (Windows `%LOCALAPPDATA%`,
Linux Steam/Proton prefix, macOS Application Support); you can also point it elsewhere
in the UI. Every save writes a timestamped backup to a `backups/` folder next to the
save, and the UI can restore any backup.

The editor covers the Player, Skills, Inventory, Afflictions, Map and Profile tabs.
