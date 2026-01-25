#!/bin/bash
# Build Fable client code before building SenderApp

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
CLIENT_DIR="$SCRIPT_DIR/SenderApp/Client"

echo "Building Fable client..."
cd "$CLIENT_DIR"

# Install npm dependencies if needed
if [ ! -d "node_modules" ]; then
    echo "Installing npm dependencies..."
    npm install
fi

# Run Fable compiler
npm run build

echo "Fable build complete!"
