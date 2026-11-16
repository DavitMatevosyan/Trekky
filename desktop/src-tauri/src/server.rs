//! The Trekky server the app talks to: one address, chosen on first run and changeable later (ADR-0018).

use std::fs;
use std::io;
use std::net::Ipv4Addr;
use std::path::Path;

use serde::{Deserialize, Serialize};
use url::{Host, Url};

/// Offered on first run. Set `TREKKY_DEFAULT_SERVER_URL` when building to point it at the hosted instance (ADR-0015).
pub const DEFAULT_SERVER_URL: &str = match option_env!("TREKKY_DEFAULT_SERVER_URL") {
    Some(url) => url,
    None => "http://localhost:5080",
};

/// The file in the app's config folder (`%APPDATA%\app.trekky.desktop`) that keeps the address.
pub const SETTINGS_FILE: &str = "settings.json";

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ServerSettings {
    pub server_url: Option<String>,
    pub default_server_url: String,
}

#[derive(Debug, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct StoredSettings {
    server_url: Option<String>,
}

/// Why an address was refused; `code()` is what the web app turns into a message.
#[derive(Debug, PartialEq, Eq)]
pub enum ServerUrlError {
    /// Not a web address.
    Invalid,
    /// Plain http to a host other than `localhost` / `127.0.0.1`: tokens would cross the network unencrypted.
    Insecure,
}

impl ServerUrlError {
    pub fn code(&self) -> &'static str {
        match self {
            ServerUrlError::Invalid => "invalid",
            ServerUrlError::Insecure => "insecure",
        }
    }
}

/// Turns what the user typed into the server's origin (`https://host[:port]`). A bare host name gets `https://`;
/// paths, queries and fragments are dropped. Plain http is allowed only for `localhost` and `127.0.0.1` (local
/// development); not `[::1]`, because the window's content security policy cannot list IPv6 addresses.
pub fn normalize(input: &str) -> Result<String, ServerUrlError> {
    let trimmed = input.trim();
    if trimmed.is_empty() {
        return Err(ServerUrlError::Invalid);
    }
    let with_scheme = if trimmed.contains("://") {
        trimmed.to_owned()
    } else {
        format!("https://{trimmed}")
    };
    let url = Url::parse(&with_scheme).map_err(|_| ServerUrlError::Invalid)?;
    if !url.username().is_empty() || url.password().is_some() {
        return Err(ServerUrlError::Invalid);
    }
    match url.scheme() {
        "https" => {}
        "http" if is_loopback(&url) => {}
        "http" => return Err(ServerUrlError::Insecure),
        _ => return Err(ServerUrlError::Invalid),
    }
    match url.host() {
        Some(Host::Domain(domain)) if !domain.is_empty() => {}
        Some(Host::Ipv4(_)) | Some(Host::Ipv6(_)) => {}
        _ => return Err(ServerUrlError::Invalid),
    }
    Ok(url.origin().ascii_serialization())
}

fn is_loopback(url: &Url) -> bool {
    match url.host() {
        Some(Host::Domain(domain)) => domain.eq_ignore_ascii_case("localhost"),
        // Exactly 127.0.0.1: the content security policy lists only that address.
        Some(Host::Ipv4(ip)) => ip == Ipv4Addr::LOCALHOST,
        Some(Host::Ipv6(_)) => false,
        None => false,
    }
}

/// The stored address, or None on first run. A missing, unreadable or hand-edited invalid file counts as none, so
/// the user is simply asked again.
pub fn load(path: &Path) -> Option<String> {
    let text = fs::read_to_string(path).ok()?;
    let stored: StoredSettings = serde_json::from_str(&text).ok()?;
    stored.server_url.and_then(|url| normalize(&url).ok())
}

/// Stores the address, creating the config folder on first use.
pub fn save(path: &Path, server_url: &str) -> io::Result<()> {
    if let Some(dir) = path.parent() {
        fs::create_dir_all(dir)?;
    }
    let stored = StoredSettings {
        server_url: Some(server_url.to_owned()),
    };
    let json = serde_json::to_string_pretty(&stored).map_err(io::Error::other)?;
    fs::write(path, json)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;

    #[test]
    fn https_addresses_become_their_origin() {
        assert_eq!(
            normalize(" https://trekky.example.ts.net/projects?x=1#top "),
            Ok("https://trekky.example.ts.net".to_owned())
        );
        assert_eq!(
            normalize("https://Trekky.Example.com:8443/"),
            Ok("https://trekky.example.com:8443".to_owned())
        );
    }

    #[test]
    fn a_bare_host_name_gets_https() {
        assert_eq!(
            normalize("trekky.example.ts.net"),
            Ok("https://trekky.example.ts.net".to_owned())
        );
    }

    #[test]
    fn plain_http_is_allowed_only_for_this_computer() {
        assert_eq!(
            normalize("http://localhost:5080"),
            Ok("http://localhost:5080".to_owned())
        );
        assert_eq!(
            normalize("http://127.0.0.1:5080/api"),
            Ok("http://127.0.0.1:5080".to_owned())
        );
        assert_eq!(
            normalize("http://[::1]:5080"),
            Err(ServerUrlError::Insecure)
        );
        assert_eq!(
            normalize("https://[::1]:5443"),
            Ok("https://[::1]:5443".to_owned())
        );
        assert_eq!(
            normalize("http://trekky.example.com"),
            Err(ServerUrlError::Insecure)
        );
        assert_eq!(
            normalize("http://127.0.0.2:5080"),
            Err(ServerUrlError::Insecure)
        );
        assert_eq!(
            normalize("http://192.168.1.10"),
            Err(ServerUrlError::Insecure)
        );
    }

    #[test]
    fn non_web_addresses_and_credentials_are_refused() {
        for input in [
            "",
            "   ",
            "ftp://trekky.example.com",
            "file:///C:/trekky",
            "https://",
            "https://user:secret@trekky.example.com",
            "trekky://auth/callback",
            "not a url",
        ] {
            assert_eq!(normalize(input), Err(ServerUrlError::Invalid), "{input:?}");
        }
    }

    #[test]
    fn the_default_server_is_a_valid_address() {
        assert_eq!(
            normalize(DEFAULT_SERVER_URL).as_deref(),
            Ok(DEFAULT_SERVER_URL)
        );
    }

    #[test]
    fn a_saved_address_is_loaded_back() {
        let path = temp_settings("roundtrip");
        save(&path, "https://trekky.example.ts.net").unwrap();
        assert_eq!(
            load(&path),
            Some("https://trekky.example.ts.net".to_owned())
        );
        cleanup(&path);
    }

    #[test]
    fn a_missing_or_broken_file_means_first_run() {
        let path = temp_settings("broken");
        assert_eq!(load(&path), None);
        fs::create_dir_all(path.parent().unwrap()).unwrap();
        fs::write(&path, "{ not json").unwrap();
        assert_eq!(load(&path), None);
        fs::write(&path, r#"{ "serverUrl": "http://evil.example.com" }"#).unwrap();
        assert_eq!(load(&path), None);
        cleanup(&path);
    }

    fn temp_settings(name: &str) -> PathBuf {
        std::env::temp_dir()
            .join(format!("trekky-desktop-test-{name}-{}", std::process::id()))
            .join(SETTINGS_FILE)
    }

    fn cleanup(path: &Path) {
        let _ = fs::remove_dir_all(path.parent().unwrap());
    }
}
