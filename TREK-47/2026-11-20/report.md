# TREK-47 manual verification — 2026-11-20

Commit `9a36f3d` (PR #60), local stack: API (Development, own database), Vite dev server, Mailpit. Driven by a Playwright
script in a real Chromium; test users `uitest-trek47-1795187643962-*@example.com`, deleted afterwards.

| # | Step | Result |
| --- | --- | --- |
| 01 | Owner opens Members from the app bar, invites a Viewer and a Member | Pass: "Invite sent to …", both listed under Pending invites with role, inviter and expiry |
| 02 | Invite email in Mailpit | Pass: inviter, organization, role, link, expiry date |
| 03 | Invitee opens the link in a fresh browser | Pass: "… invited you to join Invite Demo as a Viewer.", email fixed |
| 04 | Invitee joins with a name and password | Pass: signed in, "You are the Viewer of Invite Demo.", no New project button |
| 05 | Viewer opens Members | Pass: members listed, no invite form or pending invites |
| 06 | Owner withdraws the second invite and reloads | Pass: "No pending invites.", Vera Viewer listed as Viewer |
| 07 | The accepted link opened again | Pass: "This invite has already been used. Log in instead." |
| 08 | The withdrawn link | Pass: "This invite was withdrawn. …" |
| 09 | An Admin (invited by the Owner) opens Members | Pass: invite form shown; the role list offers only Member and Viewer (checked by the script and the component test; a closed native select cannot show its options in a screenshot) |
| 10 | Mailpit stopped, then an invite sent | Pass: invite kept, "no email went out", link shown to copy |

Noted: right after sending, the expiry reads "Expires in 6 days" (relative time rounds 6.99 days down). Cosmetic; follow-up.
