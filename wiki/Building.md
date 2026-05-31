# Building & Installation Guide 🛠️

Jellybox uses a unified build script to compile and package both the **Letterboxd Sync** and **Letterboxd Ratings** plugins.

---

## 🏗️ Building the Plugins

### Prerequisites
* .NET SDK (version 9.0 or higher)
* Bash shell environment (Linux/macOS/WSL)

### Compiling and Packaging
To build both plugins, run the automated build script from the repository root:

```bash
chmod +x build.sh
./build.sh
```

The script will:
1. Clean previous build artifacts.
2. Compile both projects targeting `.NET 9.0`.
3. Exclude debugging `.deps.json` and `.pdb` files to prevent assembly resolution conflicts on Jellyfin.
4. Output ready-to-deploy ZIP archives to the `build/` directory:
   * `build/LetterboxdSync.zip`
   * `build/LetterboxdRatings.zip`

---

## 📥 Installation

### Manual Installation
1. Copy the compiled `.dll` assembly file from the build folder into your Jellyfin plugins directory:
   ```bash
   # Example plugin directory
   /var/lib/jellyfin/plugins/LetterboxdSync/Jellyfin.Plugin.LetterboxdSync.dll
   /var/lib/jellyfin/plugins/LetterboxdRatings/Jellyfin.Plugin.LetterboxdRatings.dll
   ```
2. Restart your Jellyfin server.

### Repository Installation (Recommended)
If deploying via a self-hosted repository catalog:
1. Upload the packaged ZIP archives to your hosting server.
2. Update the repository `manifest.json` with the new version entries, checksums, and download URLs.
3. Add the repository URL to Jellyfin under **Dashboard → Plugins → Repositories**.
4. Install the plugins from the **Plugin Catalog** and restart Jellyfin.
