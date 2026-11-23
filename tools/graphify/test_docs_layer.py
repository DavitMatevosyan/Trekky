"""Tests for docs_layer.py: python -m unittest discover -s tools/graphify -p "test_*.py" (the Code graph workflow runs them)."""

from __future__ import annotations

import json
import os
import tempfile
import unittest
from pathlib import Path

import docs_layer


def _write(path: Path, data: dict) -> None:
    path.write_text(json.dumps(data), encoding="utf-8")


class DocsLayerTests(unittest.TestCase):
    def setUp(self) -> None:
        self._cwd = os.getcwd()
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        # merge() checks a doc still exists relative to the repo root, which is the working directory.
        os.chdir(self.root)
        (self.root / "docs").mkdir()
        (self.root / "docs" / "kept.md").write_text("# Kept\n", encoding="utf-8")
        self.graph = self.root / "graph.json"
        self.layer = self.root / "layer.json"

    def tearDown(self) -> None:
        os.chdir(self._cwd)
        self._tmp.cleanup()

    def code_graph(self) -> dict:
        return {
            "directed": True,
            "graph": {"hyperedges": []},
            "nodes": [{"id": "a", "_origin": "ast"}, {"id": "b", "_origin": "ast"}],
            "links": [{"source": "a", "target": "b", "relation": "calls", "_origin": "ast"}],
        }

    def test_merge_adds_doc_nodes_and_links_that_still_fit_the_code(self) -> None:
        _write(self.graph, self.code_graph())
        _write(self.layer, {
            "nodes": [
                {"id": "doc", "source_file": "docs/kept.md", "_origin": "semantic"},
                {"id": "gone", "source_file": "docs/deleted.md", "_origin": "semantic"},
                {"id": "a", "_origin": "semantic"},
            ],
            "links": [
                {"source": "doc", "target": "a", "relation": "describes"},
                {"source": "doc", "target": "renamed_code", "relation": "describes"},
                {"source": "gone", "target": "b", "relation": "describes"},
                {"source": "a", "target": "b", "relation": "calls"},
            ],
            "hyperedges": [
                {"id": "flow", "nodes": ["doc", "a", "renamed_code"]},
                {"id": "lost", "nodes": ["gone", "renamed_code"]},
            ],
        })

        docs_layer.merge(self.graph, self.layer)

        merged = json.loads(self.graph.read_text(encoding="utf-8"))
        self.assertEqual([n["id"] for n in merged["nodes"]], ["a", "b", "doc"])
        self.assertEqual([(lk["source"], lk["target"]) for lk in merged["links"]], [("a", "b"), ("doc", "a")])
        self.assertEqual(merged["graph"]["hyperedges"], [{"id": "flow", "nodes": ["doc", "a"]}])

    def test_export_keeps_only_the_semantic_part_without_community_fields(self) -> None:
        graph = self.code_graph()
        graph["nodes"].append({"id": "doc", "_origin": "semantic", "community": 3, "community_name": "Docs"})
        graph["links"].append({"source": "doc", "target": "a", "relation": "describes", "_origin": "semantic"})
        graph["graph"]["hyperedges"] = [{"id": "flow", "nodes": ["doc", "a"]}]
        _write(self.graph, graph)

        docs_layer.export(self.graph, self.layer)

        layer = json.loads(self.layer.read_text(encoding="utf-8"))
        self.assertEqual(layer["nodes"], [{"id": "doc", "_origin": "semantic"}])
        self.assertEqual(layer["links"], [{"source": "doc", "target": "a", "relation": "describes", "_origin": "semantic"}])
        self.assertEqual(layer["hyperedges"], [{"id": "flow", "nodes": ["doc", "a"]}])

    def test_export_refuses_a_code_only_graph(self) -> None:
        _write(self.graph, self.code_graph())
        with self.assertRaises(SystemExit):
            docs_layer.export(self.graph, self.layer)
        self.assertFalse(self.layer.exists())


if __name__ == "__main__":
    unittest.main()
