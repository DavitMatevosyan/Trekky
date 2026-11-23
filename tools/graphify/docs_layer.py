"""The docs layer of the code graph (ADR-0029).

CI builds the graph from code only (no model, no API key). What a model reads out of the docs, ADRs and images
(concept nodes, doc-to-code links, hyperedges) is kept in tools/graphify/docs-layer.json and merged into every CI
build, so the published graph keeps it between manual docs passes.

    python tools/graphify/docs_layer.py export [graph.json] [docs-layer.json]   # after a full local build
    python tools/graphify/docs_layer.py merge  [graph.json] [docs-layer.json]   # CI: into a code-only build

Standard library only, so it runs on any Python 3.9+.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

GRAPH = Path("graphify-out/graph.json")
LAYER = Path("tools/graphify/docs-layer.json")
# Recomputed by `graphify cluster-only` after a merge, so they never go into the layer.
CLUSTER_FIELDS = ("community", "community_name")


def _read(path: Path) -> dict:
    with path.open(encoding="utf-8") as f:
        return json.load(f)


def _links_key(graph: dict) -> str:
    return "links" if "links" in graph else "edges"


def _hyperedges(graph: dict) -> list:
    return graph.get("graph", {}).get("hyperedges") or graph.get("hyperedges") or []


def _semantic(item: dict) -> bool:
    return item.get("_origin") == "semantic"


def export(graph_path: Path, layer_path: Path) -> None:
    graph = _read(graph_path)
    nodes = [{k: v for k, v in n.items() if k not in CLUSTER_FIELDS} for n in graph["nodes"] if _semantic(n)]
    links = [link for link in graph[_links_key(graph)] if _semantic(link)]
    if not nodes:
        sys.exit(f"{graph_path} has no semantic nodes: run the docs pass (a full build) first, see tools/graphify/README.md")
    nodes.sort(key=lambda n: n["id"])
    links.sort(key=lambda link: (link["source"], link["target"], link.get("relation", "")))
    hyperedges = sorted(_hyperedges(graph), key=lambda h: h["id"])
    source = {"built_at_commit": graph.get("built_at_commit"), "graphify_version": graph.get("graph", {}).get("graphify_version")}
    # One item per line, so a new docs pass shows up as a readable diff.
    parts = [
        "{",
        f'"source": {json.dumps(source, sort_keys=True)},',
        '"nodes": [',
        ",\n".join(json.dumps(n, sort_keys=True, ensure_ascii=False) for n in nodes),
        "],",
        '"links": [',
        ",\n".join(json.dumps(link, sort_keys=True, ensure_ascii=False) for link in links),
        "],",
        '"hyperedges": [',
        ",\n".join(json.dumps(h, sort_keys=True, ensure_ascii=False) for h in hyperedges),
        "]",
        "}",
    ]
    layer_path.parent.mkdir(parents=True, exist_ok=True)
    layer_path.write_text("\n".join(parts) + "\n", encoding="utf-8", newline="\n")
    print(f"docs layer: {len(nodes)} nodes, {len(links)} links, {len(hyperedges)} hyperedges -> {layer_path}")


def merge(graph_path: Path, layer_path: Path) -> None:
    graph = _read(graph_path)
    layer = _read(layer_path)
    ids = {n["id"] for n in graph["nodes"]}
    # Run from the repo root: a node from a doc that has since been deleted is left out.
    added_nodes = [
        n for n in layer["nodes"] if n["id"] not in ids and (not n.get("source_file") or Path(n["source_file"]).exists())
    ]
    graph["nodes"].extend(added_nodes)
    ids.update(n["id"] for n in added_nodes)

    key = _links_key(graph)
    seen = {(link["source"], link["target"], link.get("relation")) for link in graph[key]}
    added_links = 0
    for link in layer["links"]:
        edge = (link["source"], link["target"], link.get("relation"))
        # A link to code that has since been renamed or deleted is dropped, not left dangling.
        if link["source"] in ids and link["target"] in ids and edge not in seen:
            graph[key].append(link)
            seen.add(edge)
            added_links += 1

    hyperedges = _hyperedges(graph)
    known = {h["id"] for h in hyperedges}
    added_hyper = 0
    for h in layer.get("hyperedges", []):
        members = [m for m in h.get("nodes", []) if m in ids]
        if h["id"] not in known and len(members) >= 2:
            hyperedges.append({**h, "nodes": members})
            added_hyper += 1
    graph.setdefault("graph", {})["hyperedges"] = hyperedges
    if "hyperedges" in graph:
        graph["hyperedges"] = hyperedges

    with graph_path.open("w", encoding="utf-8") as f:
        json.dump(graph, f, ensure_ascii=False)
    dropped = len(layer["links"]) - added_links
    print(f"docs layer merged: +{len(added_nodes)} nodes, +{added_links} links ({dropped} skipped), +{added_hyper} hyperedges")


def main(argv: list[str]) -> None:
    if len(argv) < 2 or argv[1] not in ("export", "merge"):
        sys.exit(__doc__)
    graph_path = Path(argv[2]) if len(argv) > 2 else GRAPH
    layer_path = Path(argv[3]) if len(argv) > 3 else LAYER
    (export if argv[1] == "export" else merge)(graph_path, layer_path)


if __name__ == "__main__":
    main(sys.argv)
