# Building and releasing Trekky for Windows

CI builds and signs the MSI; Claude checks it and publishes it (ADR-0020, ADR-0023), and installed apps then offer it as an update (ADR-0024).

## On every PR

The **Desktop** workflow (`.github/workflows/desktop.yml`) runs on PRs to `dev`, `staging` or `main` that touch `desktop/` or `frontend/`, and on pushes to `dev` (so later PRs can reuse its cargo cache). On Windows, with the Rust version pinned in `rust-toolchain.toml`, it:

1. Checks that `src-tauri/tauri.conf.json`, `src-tauri/Cargo.toml` and `package.json` have the same version (`node scripts/release.mts`).
2. Runs `npm run lint` (`cargo fmt --check`, `clippy -D warnings`) and `npm test` (the Rust tests).
3. Builds the MSI with `npm run build` and attaches it to the run as the artifact `trekky-desktop-<version>-msi`, kept for 14 days. PR and `dev` builds are not signed for the updater: the signing key is used only for release tags (below).

To try a PR's build, open the workflow run from the PR's checks and download the artifact.

## Releasing a version

**Paused under ADR-0032:** no promotions and no release tags until Davit restarts them.

Releases come from `main`. Claude creates and pushes the tags (ADR-0023); a tag is the one thing besides TRK/TREK branches and `ui-evidence` that Claude pushes.

1. In a PR, set the new version (`major.minor.patch`, numbers only) in all three files. `node scripts/release.mts` checks they agree.
2. Promote it as usual: `dev` → `staging` → `main`.
3. Tag the `main` commit that has the new version, and push the tag:

   ```bash
   git fetch Trekky
   git tag desktop-v0.2.0 Trekky/main
   git push Trekky desktop-v0.2.0
   ```

4. The workflow builds the MSI again, signs it for the updater in a step of its own (`tauri signer sign --app-version <version>`, which writes `<msi>.sig`), writes the update feed `latest.json` (`node scripts/update-manifest.mts`) and creates a **draft** release named "Trekky for Windows 0.2.0" with the MSI and `latest.json` attached. A tag that does not match the version in the files, or a missing updater key, fails the run before anything is released.
5. Download the MSI from the draft, install it over the previous version, and check that it upgrades in place and starts.
6. Publish the release as the **latest** release. Installed apps read `releases/latest/download/latest.json`, so they find it on their next start and offer the update. A draft reaches nobody.

Any other release in this repository (not a desktop one) must be created with `--latest=false`: if it became the latest release, every installed app would find no `latest.json` and stop seeing updates.

To build a tag again (for example after a failed run), delete its draft release first. Creating a release for a tag that already has one fails.

## First-run server

Set the repository variable `TREKKY_DEFAULT_SERVER_URL` (Settings → Secrets and variables → Actions → Variables) to the home host's address, `https://<name>.<tailnet>.ts.net`. Builds then offer that address on first run. Without it they offer the local dev server.

## Updater key (ADR-0024)

Release MSIs are signed for the updater with Trekky's own minisign key. The public key is `plugins.updater.pubkey` in `src-tauri/tauri.conf.json`; the private key and its password are the repository secrets `TAURI_SIGNING_PRIVATE_KEY` and `TAURI_SIGNING_PRIVATE_KEY_PASSWORD`, which only the release signing step receives. GitHub secrets cannot be read back, so the key also needs a copy outside GitHub. When this was written, the only one was `trekky-secrets` on the home machine; an offline backup is Davit's to keep (the key's owner, ADR-0023). Never print, commit or share the private key or its password.

- **Lost key:** installed apps trust only the public key they were built with, so they cannot be updated any more; users install a new MSI by hand. Generate a new pair (`npx tauri signer generate`), put the public key in `tauri.conf.json`, store the private key as the secrets.
- **Leaked key:** release a version with a new public key, signed with the old key, so installed apps move to the new key; then replace the secrets.

To try an update locally, run a debug build with `TREKKY_UPDATE_FEED=<url of a latest.json>` and `TREKKY_UPDATE_LOG=<file>`: **Install and restart** then downloads and verifies the update and writes `verified <version>` to the file instead of installing it. Without `TREKKY_UPDATE_FEED`, debug builds never check for updates. The smoke e2e does this with a feed of its own; with `TREKKY_E2E_SIGNED_UPDATE=<msi>` (signed with `tauri signer sign --app-version 99.0.0`, so it needs the key) it checks a real signed update.

## Rust version

`rust-toolchain.toml` pins the Rust version, so a new stable release cannot add clippy lints that fail unrelated PRs. rustup installs it the first time you build in `desktop/`. Bump it in a PR of its own.

## Installer not code-signed

The MSI is signed for the updater but not code-signed, so Windows SmartScreen warns on a first install: "Windows protected your PC". Choose **More info**, then **Run anyway**. Code signing is a follow-up in ADR-0020.
