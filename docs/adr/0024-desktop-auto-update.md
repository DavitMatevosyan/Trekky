# ADR-0024: The desktop app updates itself from signed GitHub releases

- **Status:** Agreed. The requirement is Davit's (TRK-28: "Tauri updater with its own signing key, update feed on GitHub Releases"), and so is the key handling (2026-11-16: the key pair generated locally and kept as GitHub secrets). The rest was decided by Claude under ADR-0023.
- **Date:** 2026-11-17
- **Deciders:** Davit, Claude
- **Builds on:** ADR-0018 (desktop shell, MSI with a fixed upgrade code), ADR-0020 (builds and draft releases in CI)

## Context

Trekky for Windows is installed from an MSI on a GitHub release (ADR-0020). Without updates, every user keeps the version they installed until they download a new MSI by hand. The web app the desktop app shows is bundled into it, so an old desktop app also means an old UI against a newer API, which may even break sign-in. The MSI is not code-signed (no certificate for a POC), so Windows alone cannot tell a real Trekky installer from a fake one. An update mechanism must therefore check where an update comes from by itself.

## Decision

The app uses **Tauri's updater plugin** with a **minisign key pair of Trekky's own**:

- **Keys:**
  - The public key is in `tauri.conf.json`.
  - The private key and its password are the repository secrets `TAURI_SIGNING_PRIVATE_KEY` and `TAURI_SIGNING_PRIVATE_KEY_PASSWORD`.
  - GitHub cannot give secrets back, so the key also needs a copy outside GitHub. When this was written, the only one was a file on the home machine. An offline backup is Davit's to keep: the key is his credential (ADR-0023).
- **CI:** only a `desktop-v*` tag uses the key, in a step of its own after the build. The step signs the MSI for that version (`<msi>.sig`) and writes the update feed `latest.json`; the draft release carries the MSI and `latest.json`. A tag without the key fails. PR and `dev` builds are never signed: the build runs the whole npm and cargo tree, which never sees the key.
- **Feed:** the app reads `https://github.com/DavitMatevosyan/Trekky/releases/latest/download/latest.json`. That is the newest **published** release marked latest, so a draft reaches nobody.
- **Checking and installing:**
  - Once per app start, the app checks the feed. The notice sits above every desktop screen, including server setup and sign-in, so a version that fixes sign-in can still reach the user.
  - When a newer version exists, the app shows "Trekky x.y.z is available", says that Trekky closes and Windows asks for permission, and offers **Install and restart** and **Later**. **Later** lasts until the next start.
  - Installing downloads the MSI. It verifies the signature, and that the signature was made for the announced version (`requireSignedVersion`). It then runs the MSI in passive mode, which upgrades in place (fixed upgrade code) and starts the app again.
  - A feed that cannot be reached is silent.
- **Debug builds:** they never read the real feed, so a development build cannot install a release over itself. Tests point them at a feed of their own (`TREKKY_UPDATE_FEED`) and verify instead of installing (`TREKKY_UPDATE_LOG`).

## Consequences

- **Easier:**
  - Users get every release without looking for it.
  - Publishing a release (Claude, ADR-0023) is the only step needed to ship a version.
  - Only a file signed with Trekky's private key, for the version the feed announces, is installed. A changed MSI, a swapped `latest.json` or a replayed old release is refused, and the updater never installs an older version.
- **Harder:**
  - **The private key cannot be replaced without stranding users:** an app only trusts the public key it was built with.
    - If the key is lost, installed apps can no longer be updated, and users must install a new MSI by hand.
    - If it leaks, an attacker who can also serve the feed could push an update. Rotate it by releasing a version with the new public key, signed with the old key.
    - Hence the offline copy, the secrets-only storage and the tag-only signing step: the key is never printed, committed or shared.
  - Installing needs administrator rights (per-machine MSI): Windows asks for consent on each update. The installer closes Trekky first, so if the user declines, the update stops and Trekky has to be started again by hand.
  - Every start of a release build contacts github.com. A self-hosted user's app does this too, and there is no switch to turn it off yet.
  - Any other GitHub release in the repository must not be marked latest (`--latest=false`), or the feed address stops finding `latest.json` and updates stop silently.
  - Windows SmartScreen still warns on a first install from the release page until the MSI is code-signed (ADR-0020).
- **Next:**
  - Code-signing the MSI is still a follow-up (ADR-0020).
  - Release notes in `latest.json` are a fixed line for now.
  - Turning update checks off is a setting for later, if self-hosters ask for it.

## Alternatives considered

- **No auto-update; users download new MSIs:** most users would stay on old versions, and the bundled UI would drift from the API.
- **Sign every CI build:** this would test the key on every PR, but the key would sit in the environment of every npm and cargo build script on every PR. Signing only release tags keeps it out of reach of PR code.
- **Check on a timer as well as at start:** the app is restarted often enough for a POC, and a timer adds a second code path to test.
- **Install silently at start without asking:** the user would lose unsaved input and get a Windows consent prompt with no warning. Asking first is clearer.
- **Host the feed on the home server:** the feed would move with the server address, and a self-hosted user's server would have to carry it. GitHub releases are already where the MSIs live.
