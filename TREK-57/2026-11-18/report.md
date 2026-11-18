# TREK-57 manual verification: pass

PR #51, head ef8a871. No page uses the new components yet (that is TREK-58). A throwaway preview rendered the real `TypeBadge` and `typeAccent` markup (react-dom/server) next to the production build's CSS. It was served by `vite preview` and viewed in Playwright Chromium at 900 px, light and then dark.

| Check | Result |
|---|---|
| Six types plus one unknown, full badges | Icon and label on the tint, type-coloured text, 1px accent border; the six read as one family, and the unknown type is neutral. |
| Compact badges | Icon only; the label is in the markup for screen readers ("TechDebt") and in the tooltip (title). |
| Cards with the 3px stripe | Stripe in the type accent; the unknown type's stripe uses `input`. |
| Rows, with the Epic tinted | The Epic row sits on its tint; the other rows show only the stripe. |
| Dark | The same layout, equally subtle on charcoal. |

Cleanup: the preview page and server were removed, and the local screenshots were deleted. No users or data were created.
