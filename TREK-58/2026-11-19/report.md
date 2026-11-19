# TREK-58 manual verification: pass

PR #53, head 97f28fa, local stack (API :5080, Vite :5173), Playwright Chromium, 1280×800, light and then dark. One throwaway user, project TRK, one item of each type: TRK-1 Epic, TRK-2 Story, TRK-3 Task (In Progress), TRK-4 SubTask, TRK-5 Bug and TRK-6 TechDebt.

| # | Step | Result |
|---|------|--------|
| 1, 6 | List, hovering the Epic row | Type badges, a 3px stripe per row, and the Epic row on a faint tint that darkens on hover. |
| 2, 7 | Board, hovering the Epic card | A compact badge before each key, stripes, the Epic card tinted with its accent ring on hover, the SubTask card flat. |
| 3, 8 | Keyboard drag of the Epic (Space, ArrowUp) | The dragged card is opaque at the full Epic tint, with no other card showing through. The key link's description reads "Epic \| Press Enter to open this card…". |
| 4, 9 | Item page TRK-3 | TypeBadge in the header, a compact SubTask badge on the child, and a compact Story badge on the parent line. |
| 5, 10 | New item form, type Bug, parent picker open | The Bug icon in the type select, and the type icons in the parent options. |

Cleanup: the test user and org were deleted with their data; the API, Vite and browser were stopped; the local screenshots were deleted.
