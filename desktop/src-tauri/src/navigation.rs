//! Keeps the window on the app (ADR-0018): the webview shows only the bundled web app, never a server's or another
//! site's page. Sign-in and outside links go to the system browser instead (TREK-35).

use url::Url;

/// The origin the bundled web app is served from on Windows (`useHttpsScheme: false`).
const APP_ORIGIN: &str = "http://tauri.localhost";

/// The Vite dev server `tauri dev` loads (`devUrl`); allowed in debug builds only.
const DEV_ORIGIN: &str = "http://localhost:5173";

/// True when the window may load `url`.
pub fn is_app_url(url: &Url, debug: bool) -> bool {
    let origin = url.origin().ascii_serialization();
    origin == APP_ORIGIN || (debug && origin == DEV_ORIGIN) || url.as_str() == "about:blank"
}

#[cfg(test)]
mod tests {
    use super::*;

    fn allowed(url: &str, debug: bool) -> bool {
        is_app_url(&Url::parse(url).unwrap(), debug)
    }

    #[test]
    fn the_bundled_app_is_allowed() {
        assert!(allowed("http://tauri.localhost/", false));
        assert!(allowed(
            "http://tauri.localhost/projects/TREK?tab=board",
            false
        ));
    }

    #[test]
    fn servers_and_other_sites_are_not() {
        assert!(!allowed(
            "http://localhost:5080/connect/authorize?client_id=x",
            false
        ));
        assert!(!allowed("https://trekky.example.ts.net/login", false));
        assert!(!allowed("https://tauri.localhost/", false));
        assert!(!allowed("http://tauri.localhost.evil.example/", false));
        assert!(!allowed("trekky://auth/callback?code=x", false));
    }

    #[test]
    fn the_dev_server_is_allowed_in_debug_builds_only() {
        assert!(allowed("http://localhost:5173/login", true));
        assert!(!allowed("http://localhost:5173/login", false));
    }
}
