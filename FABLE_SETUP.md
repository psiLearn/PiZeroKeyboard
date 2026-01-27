# Fable Integration Setup

This guide explains how to enable Fable compilation for the SenderApp client code.

## Current Status

- **Fable Project Structure**: ✅ Configured in `SenderApp/Client/`
- **Pre-build Integration**: ✅ Enabled in `SenderApp.fsproj`
- **Tooling**: ✅ Local `dotnet fable` via `dotnet-tools.json`
- **JavaScript Runtime**: ✅ Active (`wwwroot/src/*.js`, `sender.css`)

## To Enable Fable Compilation

### Prerequisites

1. **Node.js** (v14+)
   ```bash
   node --version
   npm --version
   ```

2. **.NET SDK** (for the `dotnet fable` tool manifest)

### Setup Steps

1. **Restore dotnet tools** (from repo root):
   ```bash
   dotnet tool restore
   ```

2. **Install dependencies**:
   ```bash
   cd SenderApp/Client
   npm install
   ```

3. **Build the client**:
   ```bash
   npm run build
   ```

4. **Or build the entire project** (auto-builds client):
   ```bash
   dotnet build SenderApp/SenderApp.fsproj
   ```

## Project Structure

```
SenderApp/Client/
├── Client.fsproj              # Fable project file (F# to JS compilation)
├── fable.json                 # Fable configuration
├── package.json               # npm configuration
├── src/
│   ├── Sender.fs              # Main client application (F#)
│   ├── History.fs             # History management (F#)
│   └── HistoryCore.fs         # Core history logic (F#)
└── node_modules/              # npm dependencies (generated)
```

## How It Works

1. **Pre-build Hook**: `SenderApp.fsproj` has a `BuildFableClient` target that runs before `Build`
2. **npm Installation**: Dependencies are installed via `npm install`
3. **Fable Compilation**: `npm run build` compiles `src/*.fs` → `../wwwroot/src/*.js`
4. **Static Files**: Compiled JS is included in the server's wwwroot

## Troubleshooting

### npm install fails

**Problem**: Registry errors or missing packages
- **Solution**: Clear npm cache and retry: `npm cache clean --force`

### Fable compiler errors

**Problem**: JSInterop or syntax issues
- **Solution**: Verify `dotnet fable` tool restore and run `dotnet fable --help`
- **Reference**: [Fable Documentation](https://fable.io/)

### Skipping Fable build

If you want to skip the Fable build (use existing JavaScript):
- The pre-build target has `ContinueOnError="true"`, so failures won't block the main build
- To fully disable: Comment out the `<Target>` block in `SenderApp.fsproj`

## Current Implementation

The project now uses **Fable-generated JavaScript** for the client:
- `SenderApp/wwwroot/src/Sender.js` - Fable output for UI behavior
- `SenderApp/wwwroot/src/History.js` - Fable output for history storage
- `SenderApp/wwwroot/src/HistoryCore.js` - Fable output for core history logic
- `SenderApp/wwwroot/sender.css` - Full styling

## Next Steps

To fully enable Fable:
1. Keep Fable sources and outputs in sync (run `npm run build`)
2. Add browser-level tests if needed (Playwright)
3. Monitor bundle size/performance

## See Also

- [Fable Official Docs](https://fable.io/)
- [JSInterop in Fable](https://fable.io/docs/javascript/interop.html)
- [Current JavaScript Implementation](SenderApp/wwwroot/src/Sender.js)
- [Build Configuration](SenderApp/SenderApp.fsproj)
