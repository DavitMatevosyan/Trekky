//! Updates (ADR-0024): the app asks the release feed whether a newer version exists and installs it when the user
//! agrees. The updater plugin checks the download against the public key in tauri.conf.json, and the version it was
//! signed for, so only an MSI signed with Trekky's private key (kept as a CI secret) for that version is installed.
//!
//! Debug builds never look at the real feed, so a development build cannot install a release over itself. Tests set
//! `TREKKY_UPDATE_FEED` to a feed of their own and `TREKKY_UPDATE_LOG` to a file: installing then downloads and
//! verifies the update and writes "verified <version>" there instead of running the installer (desktop/e2e).

use serde::Serialize;
use tauri::AppHandle;
use tauri_plugin_updater::{Updater, UpdaterExt};

/// What the web app shows about an available update.
#[derive(Debug, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct AvailableUpdate {
    pub version: String,
    pub notes: Option<String>,
}

/// A newer version from the release feed, or None when this one is the newest.
pub async fn check(app: &AppHandle) -> Result<Option<AvailableUpdate>, String> {
    let Some(updater) = updater(app)? else {
        return Ok(None);
    };
    let update = updater.check().await.map_err(|error| error.to_string())?;
    Ok(update.map(|update| AvailableUpdate {
        version: update.version,
        notes: update.body,
    }))
}

/// Downloads the newer version, verifies its signature and starts its installer. On Windows the installer closes the
/// app, upgrades it in place (fixed upgrade code, ADR-0018) and starts it again.
pub async fn install(app: &AppHandle) -> Result<(), String> {
    let updater = updater(app)?.ok_or_else(|| "no_update".to_owned())?;
    let update = updater
        .check()
        .await
        .map_err(|error| error.to_string())?
        .ok_or_else(|| "no_update".to_owned())?;

    #[cfg(debug_assertions)]
    if let Some(log) = std::env::var_os("TREKKY_UPDATE_LOG") {
        use std::io::Write;
        // download() verifies the signature and the signed version before it returns.
        update
            .download(|_chunk, _total| {}, || {})
            .await
            .map_err(|error| error.to_string())?;
        let mut file = std::fs::OpenOptions::new()
            .create(true)
            .append(true)
            .open(log)
            .map_err(|error| error.to_string())?;
        return writeln!(file, "verified {}", update.version).map_err(|error| error.to_string());
    }

    update
        .download_and_install(|_chunk, _total| {}, || {})
        .await
        .map_err(|error| error.to_string())?;
    app.restart()
}

/// The updater for the release feed in tauri.conf.json; in debug builds only the test feed, if one is set.
fn updater(app: &AppHandle) -> Result<Option<Updater>, String> {
    let builder = app.updater_builder();
    #[cfg(debug_assertions)]
    let builder = {
        let Some(feed) = std::env::var_os("TREKKY_UPDATE_FEED") else {
            return Ok(None);
        };
        let feed = feed
            .to_str()
            .and_then(|feed| feed.parse().ok())
            .ok_or_else(|| "TREKKY_UPDATE_FEED is not a URL".to_owned())?;
        builder
            .endpoints(vec![feed])
            .map_err(|error| error.to_string())?
    };
    builder.build().map(Some).map_err(|error| error.to_string())
}
