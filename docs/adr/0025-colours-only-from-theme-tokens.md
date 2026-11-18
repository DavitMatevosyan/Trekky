# ADR-0025: Colours come only from theme tokens

- **Status:** Agreed. The rule is Davit's (TREK-56: "ADR: colours only come from theme tokens, never raw values in components"); the details were decided by Claude under ADR-0023.
- **Date:** 2026-11-18
- **Deciders:** Davit, Claude
- **Builds on:** ADR-0010 (React, Tailwind and shadcn/ui)

## Context

Trekky now has two designed themes, a warm beige light theme and a warm charcoal dark theme. The user picks Light, Dark or System on each device (TREK-56). Work item types get their own colours next (TREK-57), and statuses may follow (TREK-59). Each colour has to work in both themes and meet WCAG AA there. A colour written into a component (`#c2410c`, `text-orange-700`, an inline `style`) does neither: it ignores the theme, it is invisible to the contrast checks, and it has to be found and changed by hand for every new theme. The app has about 60 components and gets about an hour of work a week, so a colour change must be one edit in one place.

## Decision

Components take colours only from the theme tokens in `frontend/src/index.css`, through their Tailwind utilities (`bg-surface`, `text-muted-foreground`, `border-input`, and later `text-type-bug` and the like). Raw colour values and Tailwind's own palette (`red-500`, `white`, `black`) are not used in components. Each token is defined once per theme. Every pair of tokens the app paints together has its WCAG AA contrast checked by a unit test that reads `index.css` (`src/theme/themeTokens.test.ts`). A new colour is a new token in both themes, with its pairs added to that test.

## Consequences

- A theme is one block of tokens. Changing a colour, or adding a theme later (high contrast, for example), touches `index.css` and nothing else.
- Contrast is checked in CI rather than by eye. A token change that breaks AA fails the build.
- The token set has to grow on purpose: 14 tokens today (background, surface, surface-raised, foreground, muted, muted-foreground, accent, accent-foreground, primary, primary-foreground, destructive, border, input, ring). Type and status colours come as `--type-<type>-{tint,accent,text}` and `--status-<status>-{tint,accent,text}` in TREK-57 and TREK-59.
- SVG icons draw with `currentColor`, so they follow the text token they sit in.
- Reviews check for raw colours. There is no lint rule yet; if one slips through, add one (for example a `no-restricted-syntax` pattern on palette class names).

## Alternatives considered

- **Tailwind's palette in components, with `dark:` variants.** It is quick at first, but every component carries two colours, and the contrast and the beige look have to be checked by hand in each.
- **CSS-in-JS theme objects.** They add a dependency and a runtime for what CSS custom properties already do, and they don't fit the Tailwind and shadcn/ui setup of ADR-0010.
- **Tokens without a contrast test.** Cheaper, but AA in both themes would then rest on review alone, and TREK-56 asks for the pairs to be checked by a test.
