# Fable Integration Setup

This guide explains how to enable Fable compilation for the SenderApp client code.

## Current Status

- **Fable Project Structure**: ✅ Configured in `SenderApp/Client/`
- **Pre-build Integration**: ✅ Enabled in `SenderApp.fsproj`
- **JavaScript Runtime**: ✅ Active (sender.js, history.js, sender.css)

## To Enable Fable Compilation

### Prerequisites

1. **Node.js** (v14+)
   ```bash
   node --version
   npm --version
   ```

2. **Fable Compiler** (versions 2.x or 3.x are stable)

### Setup Steps

1. **Choose a Fable version** from available releases:
   - Latest stable: [fable-compiler@2.13.0](https://www.npmjs.com/package/fable-compiler/v/2.13.0)
   - Or use a newer version if available

2. **Update `SenderApp/Client/package.json`**:
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

3. **Install dependencies**:
   ```bash
   cd SenderApp/Client
   npm install
   ```

4. **Build the client**:
   ```bash
   npm run build
   ```

5. **Or build the entire project** (auto-builds client):
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
│   └── History.fs             # History management (F#)
└── node_modules/              # npm dependencies (generated)
```

## How It Works

1. **Pre-build Hook**: `SenderApp.fsproj` has a `BuildFableClient` target that runs before `Build`
2. **npm Installation**: Dependencies are installed via `npm install`
3. **Fable Compilation**: `npm run build` compiles `src/*.fs` → `../wwwroot/*.js`
4. **Static Files**: Compiled JS is included in the server's wwwroot

## Troubleshooting

### npm install fails

**Problem**: Package not found (404 error)
- **Solution**: Check npm package versions exist on registry
- **Command**: `npm view fable-compiler versions`

### Fable compiler errors

**Problem**: JSInterop or syntax issues
- **Solution**: Verify Fable version compatibility with F# 5.0
- **Reference**: [Fable Documentation](https://fable.io/)

### Skipping Fable build

If you want to skip the Fable build (use existing JavaScript):
- The pre-build target has `ContinueOnError="true"`, so failures won't block the main build
- To fully disable: Comment out the `<Target>` block in `SenderApp.fsproj`

## Current Implementation

The project currently uses **hand-written JavaScript** for the client:
- `SenderApp/wwwroot/sender.js` - 305 lines, all Phase 4 features
- `SenderApp/wwwroot/history.js` - 132 lines, localStorage management
- `SenderApp/wwwroot/sender.css` - Full styling

This JavaScript is production-ready and the build doesn't depend on Fable.

## Next Steps

To fully enable Fable:
1. Fix Fable F# source files (currently stubs in `src/`)
2. Update npm dependencies to compatible versions
3. Test Fable compilation output
4. Compare Fable JS with current hand-written JS
5. Migrate to Fable output if satisfied with size/performance

## See Also

- [Fable Official Docs](https://fable.io/)
- [JSInterop in Fable](https://fable.io/docs/javascript/interop.html)
- [Current JavaScript Implementation](SenderApp/wwwroot/sender.js)
- [Build Configuration](SenderApp/SenderApp.fsproj)
