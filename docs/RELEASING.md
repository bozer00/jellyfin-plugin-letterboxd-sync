# Releasing Jellybox plugins

Releases are created only from `main` after the build-and-test workflow is green. Each release gets an immutable tag such as `v2026.08.30`; do not reuse a tag or replace an uploaded asset.

## Publish

1. Verify that each plugin version in its project file represents the intended release. The release workflow writes those versions into the generated catalog manifest.
2. Run **Publish release artifacts** from the `main` branch, supplying a new tag.
3. The workflow restores, builds, and tests all plugins; packages one ZIP per plugin; verifies that every ZIP contains only its expected DLL; calculates MD5 checksums; and creates a GitHub Release containing the ZIPs plus a generated `manifest.json`.
4. Review the release and generated manifest. Its URLs are tied to the immutable release tag and its checksums are calculated from the uploaded ZIPs.
5. In a reviewed follow-up commit, promote the generated manifest to the repository root. It intentionally advertises only artifacts produced by that run, rather than retaining version records that point to mutable `main`-branch binaries.

Never hand-edit a release checksum. If an artifact changes, create a new plugin version and a new immutable release.

## Recovery

If a release is faulty, publish a newer corrected version. Do not replace an existing release asset: catalog clients rely on the checksum and immutable URL.
