# Enabling Fable for SenderApp Client

## Quick Start

Fable compilation is **integrated** in the build process.

### To build the client now:

1. **Restore dotnet tools** (from repo root):
   ```bash
   dotnet tool restore
   ```

2. **Install npm packages:**
   ```bash
   cd SenderApp/Client
   npm install
   ```

3. **Build with Fable:**
   ```bash
   npm run build

   # Or build entire project:
   dotnet build SenderApp/SenderApp.fsproj
   ```

## Current State

### ✅ Enabled Features
- **Pre-build target** in `SenderApp.fsproj` runs the client build before main build
- **Client project** setup: F# sources in `SenderApp/Client/src/`
- **Tooling**: local `dotnet fable` via `dotnet-tools.json`
- **Static file serving**: compiled JS served from `wwwroot/src/`

### 📋 Setup Status
- Client.fsproj: ✅ Configured
- fable.json: ✅ Ready (outputs to `../wwwroot/src`)
- package.json: ✅ Uses `dotnet fable`
- src/Sender.fs: ✅ Implemented
- src/History.fs: ✅ Implemented
- src/HistoryCore.fs: ✅ Implemented

### 🚀 Current Implementation
The app uses **Fable-generated JavaScript**:
- **wwwroot/src/Sender.js** - UI + WebSocket + history interactions
- **wwwroot/src/History.js** - Local storage wrapper
- **wwwroot/src/HistoryCore.js** - Pure history logic
- **sender.css** - Complete styling ✅

## Integration Points

### Build Flow
```
dotnet build SenderApp/SenderApp.fsproj
    ↓
[Pre-build: BuildFableClient target]
    ↓
dotnet tool restore
    ↓
cd SenderApp/Client && npm install
    ↓
npm run build (→ dotnet fable)
    ↓
Outputs to ../wwwroot/src/ (included in static files)
    ↓
[Main build: SenderApp]
    ↓
bin/Release/SenderApp.dll
```

### File Mapping
- `SenderApp/Client/src/Sender.fs` → `SenderApp/wwwroot/src/Sender.js`
- `SenderApp/Client/src/History.fs` → `SenderApp/wwwroot/src/History.js`
- `SenderApp/Client/src/HistoryCore.fs` → `SenderApp/wwwroot/src/HistoryCore.js`

## Troubleshooting

| Problem | Solution |
|---------|----------|
| `npm install` fails | Run `npm cache clean --force` then retry |
| Tool restore fails | Run `dotnet tool restore` from repo root |
| Fable won't compile | Ensure F# syntax is valid, run `dotnet fable --help` |
| JS not updating | Clear `wwwroot/src` and rebuild: `npm run build` |

## Documentation

- [Fable Official Docs](https://fable.io/)
- [Fable + Browser APIs](https://fable.io/docs/javascript/interop.html)
- [MSBuild Targets](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-targets)

## See Also

- [FABLE_SETUP.md](FABLE_SETUP.md) - Detailed setup guide
- [SenderApp/wwwroot/src/Sender.js](SenderApp/wwwroot/src/Sender.js) - Current JS output
- [SenderApp/Client/src/Sender.fs](SenderApp/Client/src/Sender.fs) - F# source
