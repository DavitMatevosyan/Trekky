# ADR-0031: The desktop assistant runs the user's own Claude Code; Trekky never touches Claude credentials

- **Status:** Agreed. The rules are Davit's (TREK-61 and TREK-62: desktop only, the user's own unmodified Claude Code, sign-in only through Anthropic, no Claude credentials in Trekky, no server-side AI, a guarded web app, the naming rule, usage on the user's own plan, a provider interface); the name "Trekky Assistant" is the one TREK-61 and TREK-66 use; accepting Anthropic's Commercial Terms is Davit's decision (asked in the TREK-62 session on 2026-11-25, he chose "You accept them": he accepts them with his own Console organization before the first release that ships the assistant); the details below were decided by Claude under ADR-0023.
- **Date:** 2026-11-25
- **Deciders:** Davit, Claude
- **Builds on:** ADR-0018 (thin desktop shell, screens in the web app), ADR-0019 (desktop sign-in), ADR-0026 (role policies)

## Context

TREK-61 adds a chat to the desktop app: the user asks in plain words ("create a bug under TREK-41 …") and an assistant does it with Trekky's MCP tools (TREK-50). A POC with no paid services should not pay for or resell AI usage, and many users already have Claude Code. Anthropic's [legal and compliance page](https://code.claude.com/docs/en/legal-and-compliance) sets the limits. Third-party developers may not offer Claude.ai login, route requests through users' plan credentials, or "collect, store, or intermediate Claude.ai credentials or session tokens"; sign-in "must complete through Anthropic's own flow". The [Agent SDK overview](https://code.claude.com/docs/en/agent-sdk/overview) says the same for SDK apps. Under "Can customers offer Claude Code in their products?", "preinstalling or running Claude Code in your products" requires the Commercial Terms of Service, an unmodified binary with no sign-in method removed or restricted, no paying for or intermediating users' usage, and no Claude Code or Anthropic name or logo in a product or feature name. The v1.2 plan in Notion (TRK-30, TRK-36: AI delivery workflow, not yet in Trekky) also runs the user's installed Claude Code from the desktop app, so the runner must serve both.

## Decision

The assistant exists **only in the desktop app** and starts **the user's own, unmodified Claude Code CLI** on their machine; Trekky never holds, reads or passes on anything that signs in to Claude.

**For every use of the runner (this assistant and later Notion TRK-36):**

- **Terms:** Davit accepts Anthropic's Commercial Terms with his own Console organization before the first release that ships the runner.
- **The CLI:** the user installs it. Trekky finds `claude` on `PATH` and never bundles, downloads, patches or wraps it. The child process inherits the user's environment unchanged, so every sign-in the CLI supports (Claude account, own API key, Bedrock, Google Cloud, Foundry) works; "signed in" means whatever the CLI accepts.
- **Sign-in:** only Anthropic's own flow. Trekky learns "installed" and "signed in" from the CLI's exit status and output, never from Claude's credential files, keychain or Credential Manager entries. "Not signed in" opens a terminal running `claude`. Trekky never offers "Sign in with Claude", never sets a Claude credential for the CLI, and keeps only messages and the CLI's session id on the device.
- **Shell commands:** the runner lives in the Tauri shell as a narrow exception to ADR-0018's "no general file, shell or HTTP access": it starts only `claude` resolved from `PATH`, with flags fixed in Rust, and takes the prompt over stdin, never as caller-supplied arguments. "Open terminal" only runs `claude`. No generic process command is exposed to the webview.
- **No server-side AI:** no Trekky API endpoint runs a model, calls an AI provider or starts a process, and the server holds no AI key. TREK-66 adds a test that fails on an AI SDK package in `Directory.Packages.props` or on such an endpoint.
- **Usage and billing:** each turn draws from the user's own Claude plan limits or bills their own API key or cloud-provider account. Trekky never pays for, resells or meters it. The setup screen links [Use the Claude Agent SDK with your Claude plan](https://support.claude.com/en/articles/15036540) instead of quoting terms that change.
- **Naming:** the feature is **Trekky Assistant**. Plain text may say it "runs Claude Code on your computer"; no Claude Code or Anthropic name or logo is part of a product or feature name, and nothing suggests Anthropic built or endorses Trekky.
- **Providers:** an agent provider interface (detect, start, stream, stop, resume) with provider-neutral events (text, tool call, tool result, permission request, done, error, usage). Claude Code is the only provider. A later one (for example the user's own Console key, called from the desktop app) needs its own ADR and keeps these rules.

**For Trekky Assistant only:**

- **Tools:** only the Trekky MCP server (`--strict-mcp-config`) and no built-in tools (no shell, file, web or sub-agent tools). The CLI runs in a neutral working folder in app data and does not load project or local Claude settings, so a project's `CLAUDE.md`, settings or hooks cannot change it; the user's own Claude settings still apply, since sign-in may depend on them.
- **Identity:** it acts as the signed-in user with a short-lived Trekky token, under the same role policies and workflow rules as the UI (ADR-0026, TREK-64). The token never goes on a command line (other processes can read those).
- **Writes:** every write waits for Confirm on a card in the chat (TREK-65).
- **Web app:** the platform layer reports the assistant as unavailable; the nav shows "Assistant: available in the Trekky desktop app" with a download link (TREK-66).

## Consequences

- Trekky's server and database hold nothing that can act on anyone's Claude account, and Trekky has no AI bill.
- Only users who install and sign in to Claude Code get the assistant; the "not installed" and "not signed in" screens (TREK-63) are part of the feature. Web-only users get none.
- The assistant sends the work items it reads to Anthropic under the member's own Claude terms, and an Admin cannot switch that off yet. Add an organization setting that turns the assistant off before SaaS starts (with TREK-38, the Azure move).
- Headless CLI details (flags, stream-json events, settings sources) can change between Claude Code releases; they stay inside the Claude Code provider and are tested against a fake agent process.
- Notion TRK-36 reuses this runner. It adds a workspace folder as the working directory and the workspace's own permission rules and tools, under the "every use" rules above. The TRK-30 ADR builds on this one instead of re-deciding them.
- Follow-ups: TREK-63 runner, TREK-64 identity, TREK-65 chat screen, TREK-66 web guard, TREK-67 evals. All need the MCP server (TREK-50).

## Alternatives considered

- **Agent SDK or our own client signed in with the user's claude.ai account** — not allowed for third-party apps without Anthropic's approval.
- **Trekky's own Anthropic API key on the server** — Trekky would pay for and intermediate every user's usage and open a server-side AI path.
- **Bundle or patch the CLI in the MSI** — modifies or redistributes Anthropic's binary.
- **The assistant in the web app through the server** — the same server-side path and cost, plus a CLI per user on the host.
- **Ask every user for their own API key now** — allowed, but leaves out subscription users and makes Trekky store a key; kept as a possible later provider.
