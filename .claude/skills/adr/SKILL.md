---
name: adr
description: Write a Trekky architecture decision record in docs/adr from the template. Use when a decision sets a rule (stack, tenancy, auth, hosting, IDs, license) or when a TRK is an ADR task.
---

# Write an ADR

1. Find the next number: list `docs/adr/` and take the highest `NNNN` + 1 (ADR keys in the Notion tasks map directly, e.g. ADR-001 → `0001`).
2. Copy `docs/adr/0000-template.md` to `docs/adr/NNNN-<kebab-title>.md`.
3. Fill it:
   - **Status:** `Proposed` (Davit changes it to `Agreed`).
   - **Context:** the problem and constraints in 3–6 sentences — POC, ~1 h/week, no paid services, open-core goal.
   - **Decision:** one or two sentences, stated as a rule.
   - **Consequences:** what gets easier, what gets harder, follow-up TRKs.
   - **Alternatives considered:** each with the reason it lost.
4. Use what is already decided on the Notion page (Agreements, Contracts) — do not re-decide it. If the ADR contradicts an agreement, stop and ask.
5. Keep it to one page. Never edit an ADR after it is `Agreed`; write a new one that supersedes it.
