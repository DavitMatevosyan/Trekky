#!/usr/bin/env bash
# Restores graphify-out/ from the `graphify` branch that CI publishes (ADR-0029).
#
# Runs as a Claude Code SessionStart hook (.claude/settings.json) and by hand:
#   bash tools/graphify/fetch-graph.sh
# It never stages or commits anything (graphify-out/ is git-ignored) and needs no graphify install.
# Quiet when there is nothing to do: offline, no access, the branch does not exist yet, or the graph is current.
# GRAPHIFY_REMOTE picks the remote; by default the first one whose URL names Trekky, else origin.
set -u

# Resolve the repo (or worktree) this script belongs to, wherever it is run from.
cd "$(dirname "$0")" 2>/dev/null || exit 0
root=$(git rev-parse --show-toplevel 2>/dev/null) || exit 0
cd "$root" || exit 0

remote=${GRAPHIFY_REMOTE:-}
if [ -z "$remote" ]; then
  for r in $(git remote); do
    case "$(git remote get-url "$r" 2>/dev/null)" in
      *[Tt]rekky*) remote=$r; break ;;
    esac
  done
fi
remote=${remote:-origin}
git remote get-url "$remote" >/dev/null 2>&1 || exit 0

# Never wait for a credential or SSH prompt, or for a dead network.
export GIT_TERMINAL_PROMPT=0
export GIT_SSH_COMMAND="${GIT_SSH_COMMAND:-ssh -o BatchMode=yes}"
limit() { if command -v timeout >/dev/null 2>&1; then timeout 20 "$@"; else "$@"; fi; }

branch_sha=$(limit git ls-remote "$remote" refs/heads/graphify 2>/dev/null | cut -f1)
[ -n "$branch_sha" ] || exit 0

# The stamp holds the branch commit and the hash of the graph it restored, so a graph rebuilt locally since
# (which ADR-0029 rules out, but a tool may suggest) is replaced by the published one.
stamp=graphify-out/.graphify-branch
if [ -f graphify-out/graph.json ] &&
  [ "$(cat "$stamp" 2>/dev/null)" = "$branch_sha $(git hash-object graphify-out/graph.json)" ]; then
  exit 0
fi

ref="refs/remotes/$remote/graphify"
limit git fetch --quiet --no-tags "$remote" "+refs/heads/graphify:$ref" 2>/dev/null || exit 0

tmp=$(mktemp -d 2>/dev/null) || exit 0
trap 'rm -rf "$tmp"' EXIT
git archive "$ref" graphify-out | tar -x -C "$tmp" 2>/dev/null || exit 0
[ -s "$tmp/graphify-out/graph.json" ] || exit 0

# Replace the published files; the stamp is written only once all of them are in place.
mkdir -p graphify-out
for f in "$tmp"/graphify-out/*; do
  mv -f "$f" graphify-out/ || exit 0
done
# Community names from a local build would be put on CI's different clustering.
rm -f graphify-out/.graphify_labels.json
printf '%s %s\n' "$branch_sha" "$(git hash-object graphify-out/graph.json)" > "$stamp"

dev=$(sed -n 's/.*"dev_commit": *"\([0-9a-f]\{7\}\).*/\1/p' graphify-out/source.json 2>/dev/null)
echo "graphify: code graph updated from the graphify branch (built from dev ${dev:-unknown}). Query it with graphify query/path/explain if graphify is installed, otherwise read graphify-out/GRAPH_REPORT.md."
