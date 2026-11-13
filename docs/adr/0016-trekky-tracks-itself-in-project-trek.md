# ADR-0016: Trekky tracks itself in project TREK; Notion's TRK keys stay as history

- **Status:** Proposed
- **Date:** 2026-11-13
- **Deciders:** Davit, Claude
- **Amends:** ADR-0013: its "only TRK branches (`TRK-<n>-…`)" limit and its `TRK-<n>` commit key also accept `TREK-<n>` after the switch. The rest of ADR-0013 stays in force.

## Context

TRK-21 moves Trekky's task list from Notion into Trekky (v1.0 Dogfood). Trekky numbers a project's items itself, from 1, in creation order, so the imported items cannot keep their Notion keys (TRK-0, TRK-6.1, …), and the epics take numbers too. Branch names (`TRK-<n>-…`), commit messages (`TRK-<n>: …`), ADR-0013's push rule and the code reviewer all rely on one meaning per key. Importing into project `TRK` would give TRK-1 to about TRK-40 a second meaning. Davit chose on 2026-11-13 to import into a new project key instead.

## Decision

Trekky's own work is tracked in the Trekky project `TREK`; from the switch (the TRK-21 import into the hosted instance), new work uses its `TREK-<n>` key for branches (`TREK-<n>-short-description`) and commits (`TREK-<n>: …`), and every `TRK-<n>` keeps meaning the Notion item it named, written at the start of the imported item's title (`TRK-6.1 · …`).

## Consequences

- **Easier:** no key means two things; old branches, commits, PRs and ADRs stay readable; searching `TRK-6.1` in Trekky finds the imported item.
- **Rules:** wherever CLAUDE.md, ADR-0013, the `/trk` skill or the code reviewer say `TRK-<n>`, a `TREK-<n>` key from Trekky is equally valid after the switch; the task list is the `TREK` board in Trekky instead of the Notion page, and the Notion page is kept read-only as history. Work still in flight at the switch (e.g. TRK-23) keeps its TRK key until it is merged.
- **Harder:** two key prefixes in the history; the importer (`tools/notion-import`) must not be pointed at `TRK`.
- **Follow-ups:** after the import, close the Notion task list with a link to the `TREK` project.

## Alternatives considered

- Import into `TRK` and let Trekky's numbers win from the switch: one prefix, but TRK-1 to about TRK-40 would mean different items before and after, and old commits would point at the wrong items.
- Create placeholder items so Trekky's numbers line up with Notion's: fragile (TRK-6.1 and the epics do not fit a sequence) and fills the project with fake items.
