//! The refresh token in Windows Credential Manager (ADR-0019), one per server, so the app starts signed in. Only the
//! refresh token is kept; access tokens live in the web app's memory.

use std::collections::HashMap;

use keyring_core::api::CredentialStoreApi;
use keyring_core::Error;

/// The service part of the credential's name; the server's origin is the user part.
pub const SERVICE: &str = "Trekky";

/// The stored refresh token for `server`, or None.
pub fn load(service: &str, server: &str) -> Result<Option<String>, String> {
    match entry(service, server)?.get_password() {
        Ok(token) => Ok(Some(token)),
        Err(Error::NoEntry) => Ok(None),
        Err(error) => Err(error.to_string()),
    }
}

/// Stores the refresh token for `server`, or removes it (None) after logout or a rejected token.
pub fn save(service: &str, server: &str, token: Option<&str>) -> Result<(), String> {
    let entry = entry(service, server)?;
    match token {
        Some(token) => entry.set_password(token).map_err(|error| error.to_string()),
        None => match entry.delete_credential() {
            Ok(()) | Err(Error::NoEntry) => Ok(()),
            Err(error) => Err(error.to_string()),
        },
    }
}

fn entry(service: &str, server: &str) -> Result<keyring_core::Entry, String> {
    let store = windows_native_keyring_store::Store::new().map_err(|error| error.to_string())?;
    // Local: kept on this computer only. The store's default (Enterprise) would copy the token to every computer a
    // roaming domain profile signs in to.
    let modifiers = HashMap::from([("persistence", "Local")]);
    store
        .build(service, server, Some(&modifiers))
        .map_err(|error| error.to_string())
}

#[cfg(test)]
mod tests {
    use super::*;

    /// A service name of its own, so the tests never touch the app's real credentials.
    fn test_service() -> String {
        format!("Trekky-test-{}", std::process::id())
    }

    #[test]
    fn a_saved_token_is_loaded_back_per_server_and_removed_on_request() {
        let service = test_service();
        let a = "https://a.example.com";
        let b = "https://b.example.com";

        assert_eq!(load(&service, a), Ok(None));
        save(&service, a, Some("token-a")).unwrap();
        save(&service, b, Some("token-b")).unwrap();
        assert_eq!(load(&service, a), Ok(Some("token-a".to_owned())));
        assert_eq!(load(&service, b), Ok(Some("token-b".to_owned())));

        save(&service, a, Some("token-a2")).unwrap();
        assert_eq!(load(&service, a), Ok(Some("token-a2".to_owned())));

        save(&service, a, None).unwrap();
        save(&service, b, None).unwrap();
        assert_eq!(load(&service, a), Ok(None));
        assert_eq!(load(&service, b), Ok(None));
    }

    #[test]
    fn the_token_stays_on_this_computer() {
        let service = test_service();
        let server = "https://local.example.com";
        save(&service, server, Some("token")).unwrap();

        let persistence = entry(&service, server)
            .unwrap()
            .get_attributes()
            .unwrap()
            .get("persistence")
            .cloned();
        save(&service, server, None).unwrap();

        assert_eq!(
            persistence.as_deref().map(str::to_lowercase).as_deref(),
            Some("local")
        );
    }

    #[test]
    fn removing_a_missing_token_is_not_an_error() {
        assert_eq!(
            save(&test_service(), "https://none.example.com", None),
            Ok(())
        );
    }
}
