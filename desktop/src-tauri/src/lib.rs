//! Trekky for Windows (ADR-0018): a Tauri 2 window around the built web app. The web app shows the server and
//! "can't reach server" screens; this side keeps the server address, keeps the window on the app and lets only one
//! instance run.

mod navigation;
mod server;

use std::path::PathBuf;

use tauri::webview::NewWindowResponse;
use tauri::{AppHandle, Manager, WebviewWindowBuilder};

use server::ServerSettings;

/// The server address the user chose, and the default to offer when there is none yet.
#[tauri::command]
fn server_settings(app: AppHandle) -> Result<ServerSettings, String> {
    Ok(ServerSettings {
        server_url: server::load(&settings_path(&app)?),
        default_server_url: server::DEFAULT_SERVER_URL.to_owned(),
    })
}

/// Checks and stores the server address; returns it as stored (an origin), or an error code for the web app.
#[tauri::command]
fn set_server_url(app: AppHandle, url: String) -> Result<String, String> {
    let origin = server::normalize(&url).map_err(|error| error.code().to_owned())?;
    server::save(&settings_path(&app)?, &origin).map_err(|_| "save_failed".to_owned())?;
    Ok(origin)
}

fn settings_path(app: &AppHandle) -> Result<PathBuf, String> {
    app.path()
        .app_config_dir()
        .map(|dir| dir.join(server::SETTINGS_FILE))
        .map_err(|_| "no_config_dir".to_owned())
}

/// Starts the app. A second launch only brings the open window to the front.
pub fn run() {
    tauri::Builder::default()
        // First, so a second instance exits before it opens anything.
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            if let Some(window) = app.get_webview_window("main") {
                let _ = window.unminimize();
                let _ = window.show();
                let _ = window.set_focus();
            }
        }))
        // The window shows only the app; a page trying to load anything else stays where it is.
        .plugin(
            tauri::plugin::Builder::<tauri::Wry>::new("navigation-guard")
                .on_navigation(|_webview, url| navigation::is_app_url(url, cfg!(debug_assertions)))
                .build(),
        )
        .invoke_handler(tauri::generate_handler![server_settings, set_server_url])
        // The main window is created here ("create": false in tauri.conf.json) so it can refuse new windows:
        // window.open and target=_blank would otherwise open pages outside the navigation guard.
        .setup(|app| {
            let config = app
                .config()
                .app
                .windows
                .iter()
                .find(|window| window.label == "main")
                .ok_or("tauri.conf.json has no main window")?
                .clone();
            WebviewWindowBuilder::from_config(app.handle(), &config)?
                .on_new_window(|_url, _features| NewWindowResponse::Deny)
                .build()?;
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("Trekky could not start");
}
