# ADR-0020: The desktop MSI is built in CI on every PR and released from a `desktop-v` tag as a draft

- **Status:** Proposed
- **Date:** 2026-11-15
- **Deciders:** Davit, Claude
- **Builds on:** ADR-0018 (desktop shell), ADR-0012 (evidence), ADR-0013 (Claude verifies, reviews and pushes).

## Context

Trekky for Windows (ADR-0018) is built only on a developer's machine. The Rust shell's tests and lints, and the MSI itself, run nowhere else, so a PR can break the desktop app without anyone seeing it. Users need an installer they can download, and a way to tell versions apart. The version lives in three files (`tauri.conf.json`, which names the MSI and sets its ProductVersion, `Cargo.toml` and `package.json`). There is no code-signing certificate, and buying one is not worth it for a POC.

## Decision

A **`Desktop` workflow** on `windows-latest` does the following:
- runs on every PR to `dev`, `staging` or `main` that touches `desktop/` or `frontend/`, and on pushes to `dev`;
- checks that the three versions agree;
- lints and tests the shell;
- builds the **MSI and keeps it as a 14-day artifact**.

Pushing a tag **`desktop-v<version>`** builds the same way, fails unless the tag names the version in the files, and **creates a draft GitHub release** with the MSI, which Davit publishes. The MSI stays **unsigned** for now.

## Consequences

- **Easier:**
  - Every desktop or web change shows whether the app still builds and its Rust tests pass.
  - Anyone can download the MSI from the PR's checks or from a release.
  - A release cannot carry a version that the files do not have.
- **Rules:**
  - Release by bumping the version in all three files in a PR. Once it reaches `main`, Davit tags that commit `desktop-vX.Y.Z` and pushes the tag; Claude does not push tags (ADR-0013).
  - The version is `major.minor.patch` numbers only, because Windows Installer takes no pre-release labels.
  - The first-run server comes from the repository variable `TREKKY_DEFAULT_SERVER_URL` when it is set. It is a public address, so a variable, not a secret.
  - Only the release job may write to the repository (`contents: write`).
  - The Rust version is pinned in `desktop/rust-toolchain.toml`, so a new stable release cannot fail unrelated PRs with new lints. It is bumped in a PR of its own.
  - Each multi-command PowerShell step sets `$PSNativeCommandUseErrorActionPreference = $true`, so a failing command fails the step. Otherwise only the last command's exit code counts.
  - The upgrade code never changes (ADR-0018).
- **Harder:**
  - Windows SmartScreen warns on install ("More info" → "Run anyway") until the MSI is signed.
  - The workflow adds a Windows job to desktop and web PRs: about 10 to 15 minutes cold, less once a push to `dev` has saved a cargo cache that PRs can restore. The cache leaves out the MSI folder, so an old version's MSI is never picked up.
  - The smoke e2e (tauri-driver, TREK-35) still runs only locally, because it needs the API, Postgres and a matching Edge WebDriver on the runner.
- **Follow-ups:**
  - TREK-37 (auto-update) publishes the update feed from these releases.
  - Sign the MSI once a certificate is worth it.
  - Run the smoke e2e in CI.

## Alternatives considered

- **Build only on tags:** cheaper, but a broken desktop build would show up only at release time.
- **Publish releases directly instead of drafts:** one step less, but a release cannot be checked before users see it.
- **Sign with a self-signed certificate:** SmartScreen still warns, and users would be asked to trust a root certificate, which is worse.
- **Use `tauri-action`:** it does build and release in one step, but it is a third-party action with write access. Plain `npm run build` and `gh release create` keep the steps visible and the permissions small.
