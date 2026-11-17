# Building and releasing Trekky for Windows

CI builds the MSI; you check it and publish it (ADR-0020).

## On every PR

The **Desktop** workflow (`.github/workflows/desktop.yml`) runs on PRs to `dev`, `staging` or `main` that touch `desktop/` or `frontend/`, and on pushes to `dev` (so later PRs can reuse its cargo cache). On Windows, with the Rust version pinned in `rust-toolchain.toml`, it:

1. Checks that `src-tauri/tauri.conf.json`, `src-tauri/Cargo.toml` and `package.json` have the same version (`node scripts/release.mts`).
2. Runs `npm run lint` (`cargo fmt --check`, `clippy -D warnings`) and `npm test` (the Rust tests).
3. Builds the MSI with `npm run build` and attaches it to the run as the artifact `trekky-desktop-<version>-msi`, kept for 14 days.

To try a PR's build, open the workflow run from the PR's checks and download the artifact.

## Releasing a version

Releases come from `main`. Davit creates and pushes the tags: Claude pushes only TRK/TREK branches and `ui-evidence` (ADR-0013).

1. In a PR, set the new version (`major.minor.patch`, numbers only) in all three files. `node scripts/release.mts` checks they agree.
2. Promote it as usual: `dev` → `staging` → `main`.
3. Tag the `main` commit that has the new version, and push the tag:

   ```bash
   git fetch Trekky
   git tag desktop-v0.2.0 Trekky/main
   git push Trekky desktop-v0.2.0
   ```

4. The workflow builds the MSI again and creates a **draft** release named "Trekky for Windows 0.2.0" with the MSI attached. A tag that does not match the version in the files fails the run before anything is built.
5. Download the MSI from the draft, install it over the previous version, and check that it upgrades in place and starts. Then publish the release.

To build a tag again (for example after a failed run), delete its draft release first. Creating a release for a tag that already has one fails.

## First-run server

Set the repository variable `TREKKY_DEFAULT_SERVER_URL` (Settings → Secrets and variables → Actions → Variables) to the home host's address, `https://<name>.<tailnet>.ts.net`. Builds then offer that address on first run. Without it they offer the local dev server.

## Rust version

`rust-toolchain.toml` pins the Rust version, so a new stable release cannot add clippy lints that fail unrelated PRs. rustup installs it the first time you build in `desktop/`. Bump it in a PR of its own.

## Unsigned installer

The MSI is not code-signed yet, so Windows SmartScreen warns: "Windows protected your PC". Choose **More info**, then **Run anyway**. Signing is a follow-up in ADR-0020.
