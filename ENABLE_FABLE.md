# Enabling Fable for SenderApp Client

## Quick Start

Fable compilation is **integrated but optional** in the build process.

### To use Fable now:

1. **Install npm packages:**
   ```bash
   cd SenderApp/Client
   npm install
   ```

2. **Choose Fable version** and update `package.json`:
   - Check available versions: `npm view fable-compiler versions`
   - Recommended: v2.13.0 or latest stable (v4.x)

3. **Edit `SenderApp/Client/package.json`:**
   ```json
   {
     "devDependencies": {
       "fable-compiler": "2.13.0"
     },
     "dependencies": {
       "fable-core": "1.3.8"
     }
   }
   ```

4. **Build with Fable:**
   ```bash
   cd SenderApp/Client
   npm install
   npm run build
   
   # Or build entire project:
   dotnet build SenderApp/SenderApp.fsproj
   ```

## Current State

### ✅ Enabled Features
- **Pre-build target** in `SenderApp.fsproj` runs Fable before main build
- **Client project** setup: F# sources in `SenderApp/Client/src/`
- **Graceful fallback**: Build succeeds even if Fable build fails (ContinueOnError=true)
- **Static file serving**: wwwroot compiled JS served automatically

### 📋 Setup Status
- Client.fsproj: ✅ Configured
- fable.json: ✅ Ready (outputs to ../wwwroot)
- package.json: ✅ Configured (stub commands for now)
- src/Sender.fs: ⏳ Stubs present (needs completion)
- src/History.fs: ⏳ Stubs present (needs completion)

### 🚀 Current Implementation
The app currently uses **hand-written JavaScript** (not Fable):
- **sender.js** (305 lines) - Full Phase 4 implementation ✅
- **history.js** (132 lines) - Storage + migration ✅  
- **sender.css** (300+ lines) - Complete styling ✅

This provides the same functionality and is currently **production-ready**.

## Integration Points

### Build Flow
```
dotnet build SenderApp/SenderApp.fsproj
    ↓
[Pre-build: BuildFableClient target]
    ↓
cd SenderApp/Client && npm install
    ↓
npm run build (→ fable compile when enabled)
    ↓
Outputs to ../wwwroot/ (included in static files)
    ↓
[Main build: SenderApp]
    ↓
bin/Release/SenderApp.dll
```

### File Mapping
- `SenderApp/Client/src/Sender.fs` → `SenderApp/wwwroot/sender.js`
- `SenderApp/Client/src/History.fs` → `SenderApp/wwwroot/history.js`

## Next: Completing Fable Implementation

To fully switch from JavaScript to Fable:

1. **Install Fable CLI globally** (optional):
   ```bash
   npm install -g fable-compiler@2.13.0
   ```

2. **Complete F# source files**:
   - Review current JavaScript implementation
   - Rewrite in F# with proper JSInterop
   - Test compilation

3. **Verify output**:
   - Compare generated JS with hand-written version
   - Check bundle size and performance
   - Test in browser

4. **Switch to Fable output**:
   ```bash
   npm run build
   dotnet build SenderApp
   ```

## Troubleshooting

| Problem | Solution |
|---------|----------|
| `npm install` fails | Run `npm cache clean --force` then retry |
| Package not found | Check version exists: `npm view <package> versions` |
| Fable won't compile | Ensure F# syntax is valid, run `fable --help` |
| JS not updating | Clear `wwwroot` and rebuild: `npm run build` |

## Documentation

- [Fable Official Docs](https://fable.io/)
- [Fable + Browser APIs](https://fable.io/docs/javascript/interop.html)
- [MSBuild Targets](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-targets)

## See Also

- [FABLE_SETUP.md](FABLE_SETUP.md) - Detailed setup guide
- [SenderApp/wwwroot/sender.js](SenderApp/wwwroot/sender.js) - Current JS implementation
- [SenderApp/Client/src/Sender.fs](SenderApp/Client/src/Sender.fs) - F# stubs to complete
