#!/bin/bash

# Exit immediately if a command exits with a non-zero status
set -e

# Repository Root Directory
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="${REPO_ROOT}/build"

echo "=== Starting Jellybox Build Script ==="
echo "Repo Root: ${REPO_ROOT}"
echo "Build Dir: ${BUILD_DIR}"

# Clean up previous build outputs
echo "Cleaning up build directory..."
rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}"

# 1. Build Letterboxd Watchlist Sync
echo "Building Letterboxd Watchlist Sync..."
cd "${REPO_ROOT}/LetterboxdSync"
dotnet build -c Release -o "${BUILD_DIR}/LetterboxdSync"

# 2. Build Letterboxd Ratings
echo "Building Letterboxd Ratings..."
cd "${REPO_ROOT}/LetterboxdRatings"
dotnet build -c Release -o "${BUILD_DIR}/LetterboxdRatings"

# 3. Create ZIP Archives
echo "Packaging plugins into ZIP archives..."
cd "${BUILD_DIR}"

# Check if zip command is available, fallback to python if not
if command -v zip >/dev/null 2>&1; then
    # Package LetterboxdSync
    cd "${BUILD_DIR}/LetterboxdSync"
    zip -r "${BUILD_DIR}/LetterboxdSync.zip" .
    
    # Package LetterboxdRatings
    cd "${BUILD_DIR}/LetterboxdRatings"
    zip -r "${BUILD_DIR}/LetterboxdRatings.zip" .
else
    echo "Warning: 'zip' command not found. Falling back to python3 to create zip archives..."
    if command -v python3 >/dev/null 2>&1; then
        python3 -c "import shutil; shutil.make_archive('${BUILD_DIR}/LetterboxdSync', 'zip', '${BUILD_DIR}/LetterboxdSync')"
        python3 -c "import shutil; shutil.make_archive('${BUILD_DIR}/LetterboxdRatings', 'zip', '${BUILD_DIR}/LetterboxdRatings')"
    else
        echo "Error: Neither 'zip' nor 'python3' is available. Skipping ZIP archive generation."
    fi
fi

echo "=== Build and Packaging Complete! ==="
echo "Build contents:"
ls -lh "${BUILD_DIR}"
