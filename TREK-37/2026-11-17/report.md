# TREK-37 manual verification, 2026-11-17

PR #39, head 7ddaa69. Real debug app (`npm run build:debug`, rebuilt from this tree), driven by tauri-driver against the local stack (API + web dev server), using the desktop smoke e2e (`desktop/e2e/smoke.test.ts`).

| Run | Feed served by the test | Result |
| --- | --- | --- |
| Default | 99.0.0, signature not Trekky's | pass: notice on the first-run screen and after sign-in; Install shows "could not be installed"; nothing verified or installed |
| `TREKKY_E2E_SIGNED_UPDATE` | 99.0.0, MSI signed with Trekky's key for 99.0.0 | pass: the 208 MB MSI is downloaded and verified ("verified 99.0.0") |
| Replay check | 99.0.0, a real Trekky signature bound to 0.1.0 | refused, as expected (the run times out waiting for verification) |

Screenshots:
1. `01-update-on-first-run.png`: the notice above the server setup screen, before sign-in.
2. `02-update-available-signed-in.png`: the notice above the app header after sign-in.
3. `03-update-refused-foreign-signature.png`: the error after Install with a foreign signature.

Known cosmetic follow-up: while the notice shows, screens that fill the window get a vertical scrollbar (the notice adds its height).

Cleanup: e2e users deleted from the local database, app settings and Credential Manager entry restored by the test, servers, tauri-driver and the app stopped, local screenshots deleted.
