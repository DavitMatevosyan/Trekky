# TRK-21 manual verification: pass (local import; the real switch is Davit's step)

Snapshot as of 2026-11-13, imported with tools/notion-import into a local API (dev stack), throwaway user uitest-trk21-*@example.com. The screenshots were taken on the importer run just before commit 1f1c668; the only change after it was the importer refusing project key TRK and repeated Notion keys.

| # | Check | Result |
| --- | --- | --- |
| 1 | List: project TREK holds the imported items, each title starting with its Notion key | pass |
| 2 | Board: To Do 16, In Progress 1 (TRK-21), In Review 1 (TRK-9), Done 24 | pass |
| 3 | An imported item (TREK-29 = TRK-21 · Dogfood switch) with its description and history | pass |
| — | Second run: 0 created, 0 status moves | pass |
