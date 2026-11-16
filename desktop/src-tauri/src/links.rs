//! What the app hands to the system browser (ADR-0019): sign-in pages and outside links, web addresses only.

use url::Url;

/// The http(s) address the system browser may open, as parsed (so what was checked is what opens); never a file, a
/// program or another scheme.
pub fn browser_url(url: &str) -> Option<Url> {
    Url::parse(url)
        .ok()
        .filter(|parsed| matches!(parsed.scheme(), "https" | "http") && parsed.host().is_some())
}

/// True for the sign-in callback `trekky://auth/callback` (ADR-0017), as opposed to some other `trekky://` link.
pub fn is_auth_callback(url: &Url) -> bool {
    url.scheme() == "trekky" && url.host_str() == Some("auth") && url.path() == "/callback"
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn only_web_addresses_go_to_the_browser() {
        assert_eq!(
            browser_url("https://trekky.example.ts.net/connect/authorize?client_id=trekky-desktop")
                .map(String::from),
            Some(
                "https://trekky.example.ts.net/connect/authorize?client_id=trekky-desktop"
                    .to_owned()
            )
        );
        assert!(browser_url("http://localhost:5173/sign-up").is_some());
        // What opens is the parsed address, not the raw text around it.
        assert_eq!(
            browser_url(
                "  https://trekky.example.com/x 
"
            )
            .map(String::from),
            Some("https://trekky.example.com/x".to_owned())
        );
        for url in [
            "file:///C:/Windows/System32/calc.exe",
            "C:\\Windows\\notepad.exe",
            "trekky://auth/callback",
            "javascript:alert(1)",
            "ms-settings:privacy",
            "https://",
            "",
        ] {
            assert!(browser_url(url).is_none(), "{url:?}");
        }
    }

    #[test]
    fn only_the_sign_in_callback_counts() {
        let callback = |url: &str| is_auth_callback(&Url::parse(url).unwrap());
        assert!(callback("trekky://auth/callback?code=abc&state=xyz"));
        assert!(callback("trekky://auth/callback"));
        assert!(!callback("trekky://auth/other?code=abc"));
        assert!(!callback("trekky://evil/callback?code=abc"));
        assert!(!callback("https://auth/callback?code=abc"));
    }
}
