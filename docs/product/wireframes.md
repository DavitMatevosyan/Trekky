# Wireframes — v0.1 to v1.0

Low-fidelity layouts for the main screens. They fix **what is on each screen and where**, not colors or exact sizes. The UI should feel quiet: no sidebars full of features, no settings we don't have yet.

## App shell

```
+--------------------------------------------------------------------------+
| Trekky   [ Projects ▾ TRK ]          [ ⌕ Go to item…  Ctrl+K ]  [ DM ▾ ] |
+--------------------------------------------------------------------------+
| TRK · Trekky        [ Board ]  [ List ]                    [ + New item ]|
+--------------------------------------------------------------------------+
|                                                                          |
|                         (screen content)                                 |
|                                                                          |
+--------------------------------------------------------------------------+
```

- Top bar: project switcher, "go to item" by key, user menu (log out).
- Project bar: project key and name, view tabs, the one primary action.
- No left sidebar in v1.0.

## Projects (after sign-up)

```
+--------------------------------------------------------------------------+
| Projects                                              [ + New project ]  |
|                                                                          |
|  TRK   Trekky                                   12 items                 |
|  WEB   Website                                   3 items                 |
|                                                                          |
|  (empty state) No projects yet. Create one to start tracking work.       |
+--------------------------------------------------------------------------+

New project
  Name  [ Trekky                    ]
  Key   [ TRK ]   ← suggested from the name, can't be changed later
                                     [ Cancel ]  [ Create project ]
```

## List view

```
+--------------------------------------------------------------------------+
| Key     Type   Title                       Status       Parent  Updated  |
|--------------------------------------------------------------------------|
| TRK-14  Story  Comments on items           In Progress  TRK-2   2h ago   |
| TRK-13  Task   Board drag and drop         In Review    TRK-2   5h ago   |
| TRK-12  Bug    Key not shown on new item   To Do        —       1d ago   |
| TRK-2   Epic   v1.0 Dogfood                In Progress  —       2d ago   |
|                                                                          |
|                            [ Load more ]                                 |
+--------------------------------------------------------------------------+
```

- Sorted by updated, newest first. Rows are links to the detail page.
- Filters and search are v1.1; nothing here until then.

## Item detail

```
+--------------------------------------------------------------------------+
| ‹ TRK / TRK-14                                                           |
|                                                                          |
| [Story]  Comments on items                                   [ Edit ]    |
|                                                                          |
| +-------------------------------------------+  +-----------------------+ |
| | Description                               |  | Status  [In Progress▾]| |
| |                                           |  | Assignee [ Davit    ▾]| |
| | Members can discuss an item in Markdown   |  | Parent   TRK-2 Epic   | |
| | comments, shown oldest first.             |  | Created  Oct 6        | |
| |                                           |  | Updated  2h ago       | |
| | Children                                  |  +-----------------------+ |
| |  TRK-15 Task  Edit own comments   To Do   |                            |
| |  TRK-16 Task  Delete own comments Done    |                            |
| +-------------------------------------------+                            |
|                                                                          |
| [ Comments ]  [ History ]                                                |
| ------------------------------------------------------------------------ |
| Davit · 2h ago                                                           |
|   Let's keep comments flat — no threads in v1.0.                         |
|                                                                          |
| [ Write a comment… (Markdown)                               ] [ Post ]   |
+--------------------------------------------------------------------------+

History tab
  Davit  changed status  To Do → In Progress            2h ago
  Davit  set parent      — → TRK-2                      1d ago
  Davit  created         Story                          1d ago
```

- The status dropdown only offers allowed transitions.
- The URL is `/TRK/items/TRK-14`.

## Board

```
+----------------------------------------------------------------------------+
| To Do (3)          | In Progress (2)    | In Review (1)      | Done (8)    |
|--------------------|--------------------|--------------------|-------------|
| +----------------+ | +----------------+ | +----------------+ | +---------+ |
| | TRK-12   Bug   | | | TRK-14  Story  | | | TRK-13  Task   | | | TRK-9   | |
| | Key not shown  | | | Comments on    | | | Board drag and | | | ...     | |
| | on new item    | | | items          | | | drop           | | +---------+ |
| |            DM  | | |            DM  | | |            DM  | |             |
| +----------------+ | +----------------+ | +----------------+ |             |
| +----------------+ | +----------------+ |                    |             |
| | TRK-17  Task   | | | TRK-15  Task   | |                    |             |
| | ...            | | | ...            | |                    |             |
| +----------------+ | +----------------+ |                    |             |
+----------------------------------------------------------------------------+
```

- Cards: key, type, title (max 3 lines), assignee initials.
- Drop on a disallowed column → the card returns and a short message names the allowed columns.
- Done shows the latest 20 cards with "Show all".

## Sign-up and log in

```
+------------------------------+     +------------------------------+
| Create your Trekky account   |     | Log in to Trekky             |
|                              |     |                              |
| Email     [               ]  |     | Email     [               ]  |
| Password  [               ]  |     | Password  [               ]  |
|           at least 12 chars  |     |                              |
| Organization [            ]  |     |            [ Log in ]        |
|                              |     |                              |
|        [ Create account ]    |     | No account? Create one       |
| Have an account? Log in      |     +------------------------------+
+------------------------------+
```

## Keyboard shortcut map

Designed now so screens leave room for it; implemented in v1.1 ("core keyboard shortcuts"). Shortcuts never fire while typing in a field.

| Keys | Where | Action |
| --- | --- | --- |
| `Ctrl/⌘ K` | anywhere | Go to item by key |
| `?` | anywhere | Show this shortcut list |
| `g` then `b` | in a project | Go to board |
| `g` then `l` | in a project | Go to list |
| `g` then `p` | anywhere | Go to projects |
| `c` | in a project | Create item |
| `j` / `k` | list, board | Next / previous item |
| `Enter` | list, board | Open selected item |
| `e` | item detail | Edit item |
| `s` | item detail, board | Change status |
| `a` | item detail, board | Change assignee |
| `m` | item detail | Write a comment |
| `Esc` | anywhere | Close dialog / go back |
| `Ctrl/⌘ Enter` | text field | Save / post |
