# TREK-82 manual verification: pass (before merge)

Commit ea96c65, PR #66.

| # | Check | Result |
| --- | --- | --- |
| 1 | The PR runs the new `Code graph` workflow: "Build the code graph" passes and "Publish to the graphify branch" is skipped (PRs never publish) | pass (`01-pr-run.png`) |
| 2 | Build steps: the docs-layer tests (3 OK), setup-uv pinned by SHA, `graphifyy[sql]==0.9.113`, code-only build, upload skipped on the PR | pass (`02-build-log.png`) |
| 3 | Build log: 3,630 code nodes, then +244 nodes, +342 links (1 skipped) and +9 hyperedges from the docs layer, re-clustered into 185 communities, in about 10 s | pass (run 45167142451) |
| 4 | Local simulation (bare repo as the remote, the workflow's publish script, a fresh clone): restore, silent second run, a local rebuild replaced, republish, offline silent, `git status` unchanged, the branch stays at 1 commit | pass |
| 5 | Real headless Claude Code session on Windows with graphify removed from PATH: the SessionStart fetch restored the graph, and the Read and Grep hooks ran with no errors | pass |

Known: one harmless annotation. setup-uv's default cache finds no `requirements*.txt` file.

Still open: after the merge, the first push build must create the `graphify` branch with `source.json.dev_commit` equal to the merge commit. That is checked after the merge and reported on the PR.
