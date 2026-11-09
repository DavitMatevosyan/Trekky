# TRK-14 manual verification: pass

Local stack on `af6cd3b`, synthetic user `uitest-trk14@example.com`, driven in a real browser.

| Check | Result |
| --- | --- |
| A new item's history shows its creation | Pass |
| A status change appears without a reload | Pass (633 ms for To Do/In Progress → In Review) |
| An edit shows its fields in one sentence | Pass |
| Events are newest first with who and when | Pass |
| History is the same after a reload | Pass |
| Browser console | Only the React DevTools notice |

Failed reads, unknown event types and paging are covered by the component tests.
