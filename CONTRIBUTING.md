# Contributing to Jellybox

Thanks for helping improve the Jellyfin × Letterboxd plugins.

## Before opening a change

- Search existing issues and keep each pull request focused on one behavior.
- Discuss substantial behavior, catalog, or compatibility changes in an issue first.
- Never commit credentials, private Letterboxd exports, server paths, or production logs.

## Development workflow

Install the .NET SDK selected by [`global.json`](global.json), then run:

```bash
dotnet restore Jellybox.sln
dotnet build Jellybox.sln -c Release --no-restore
dotnet test Jellybox.Tests/Jellybox.Tests.csproj -c Release --no-build
```

Run `./build.sh` on a Unix-like shell to produce installable ZIP archives. Do not commit generated archives unless preparing a catalog release intentionally.

## Pull request expectations

- Add or update tests for changed behavior, especially parsing and matching edge cases.
- Preserve compatibility with the documented Jellyfin 10.11.x target unless the pull request explicitly updates the compatibility policy.
- Update the README when configuration, privacy behavior, installation, or release steps change.
- Keep changes to one plugin isolated unless a shared behavior is deliberately being changed.

## Reporting issues

Use the provided issue forms for bugs and feature requests. For security-sensitive reports, follow [SECURITY.md](SECURITY.md) instead of creating a public issue.
