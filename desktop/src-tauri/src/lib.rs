//! Trekky for Windows (ADR-0018): a Tauri 2 window around the built web app. The web app shows every screen; this
//! side keeps the server address and the refresh token, opens the system browser for sign-in and outside links,
//! hands the `trekky://` sign-in callback to the window (ADR-0019), keeps the window on the app and lets only one
//! instance run.

mod links;
mod navigation;
mod server;
mod tokens;

use std::path::PathBuf;

use tauri::webview::NewWindowResponse;
use tauri::{AppHandle, Emitter, Manager, WebviewWindowBuilder};
use tauri_plugin_deep_link::DeepLinkExt;
use tauri_plugin_opener::OpenerExt;

use server::ServerSettings;

/// The event the web app listens to for a sign-in callback; its payload is the full `trekky://auth/callback?...` URL.
const AUTH_CALLBACK_EVENT: &str = "auth-callback";

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

/// Opens a web address in the system browser: sign-in (ADR-0019) and links that lead out of the app.
#[tauri::command]
fn open_in_browser(app: AppHandle, url: String) -> Result<(), String> {
    let url = links::browser_url(&url)
        .ok_or_else(|| "not_a_web_address".to_owned())?
        .to_string();
    // Debug builds only: tests play the browser themselves, so they ask for the address instead (desktop/e2e).
    #[cfg(debug_assertions)]
    if let Some(log) = std::env::var_os("TREKKY_OPEN_URL_LOG") {
        use std::io::Write;
        let mut file = std::fs::OpenOptions::new()
            .create(true)
            .append(true)
            .open(log)
            .map_err(|error| error.to_string())?;
        return writeln!(file, "{url}").map_err(|error| error.to_string());
    }
    app.opener()
        .open_url(url, None::<&str>)
        .map_err(|error| error.to_string())
}

/// The refresh token stored for this server, if any.
#[tauri::command]
fn load_refresh_token(server: String) -> Result<Option<String>, String> {
    tokens::load(tokens::SERVICE, &server_origin(&server)?)
}

/// Stores the server's refresh token, or removes it (`token: null`).
#[tauri::command]
fn save_refresh_token(server: String, token: Option<String>) -> Result<(), String> {
    tokens::save(tokens::SERVICE, &server_origin(&server)?, token.as_deref())
}

/// Credentials are named after a valid server origin only, never after whatever the page sends.
fn server_origin(server: &str) -> Result<String, String> {
    server::normalize(server).map_err(|error| error.code().to_owned())
}

fn settings_path(app: &AppHandle) -> Result<PathBuf, String> {
    app.path()
        .app_config_dir()
        .map(|dir| dir.join(server::SETTINGS_FILE))
        .map_err(|_| "no_config_dir".to_owned())
}

fn focus_main_window(app: &AppHandle) {
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.unminimize();
        let _ = window.show();
        let _ = window.set_focus();
    }
}

/// Starts the app. A second launch, including Windows opening a `trekky://` link, only reaches the open window.
pub fn run() {
    tauri::Builder::default()
        // First, so a second instance exits before it opens anything. With the deep-link feature it also passes
        // the second instance's trekky:// URL on to the deep-link plugin below.
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            focus_main_window(app)
        }))
        .plugin(tauri_plugin_deep_link::init())
        .plugin(tauri_plugin_opener::init())
        // The window shows only the app; a page trying to load anything else stays where it is.
        .plugin(
            tauri::plugin::Builder::<tauri::Wry>::new("navigation-guard")
                .on_navigation(|_webview, url| navigation::is_app_url(url, cfg!(debug_assertions)))
                .build(),
        )
        .invoke_handler(tauri::generate_handler![
            server_settings,
            set_server_url,
            open_in_browser,
            load_refresh_token,
            save_refresh_token
        ])
        .setup(|app| {
            // The main window is created here ("create": false in tauri.conf.json) so it can refuse new windows:
            // window.open and target=_blank would otherwise open pages outside the navigation guard.
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

            // The MSI registers trekky:// on install; a development build registers itself (for this user only).
            #[cfg(debug_assertions)]
            app.deep_link().register_all()?;

            // The browser finished sign-in and Windows opened trekky://auth/callback?...: hand it to the window.
            let handle = app.handle().clone();
            app.deep_link().on_open_url(move |event| {
                for url in event.urls() {
                    if links::is_auth_callback(&url) {
                        let _ = handle.emit_to("main", AUTH_CALLBACK_EVENT, url.as_str());
                    }
                }
                focus_main_window(&handle);
            });
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("Trekky could not start");
}
