# TREK-81 test race fix (PR #65): manual verification, pass

This change touches only a test. The app's behaviour is the one verified for PR #64 (TREK-81/2026-11-23).

## Before the fix (dev 56d75cb): the full frontend suite, 4 runs in parallel
```
      Tests  2 failed | 614 passed (616)
      Tests  2 failed | 614 passed (616)
      Tests  2 failed | 614 passed (616)
      Tests  2 failed | 614 passed (616)
 FAIL  src/pages/WorkItemPage.test.tsx > WorkItemPage > opens a child from its title, with the whole row as one link, keeping the way back
Error: expect(element).toHaveAttribute("href", "/TRK?status=todo") // element.getAttribute("href") === "/TRK?status=todo"

Expected the element to have attribute:
  href="/TRK?status=todo"
Received:
  href="/TRK"
 ❯ src/pages/WorkItemPage.test.tsx:98:111
     96|     expect(await screen.findByRole('heading', { level: 1, name: 'Task …
     97|     expect(router.state.location.pathname).toBe('/TRK/items/TRK-15')
```

(The second failure in each run is the existing description test hitting its 5 s timeout under this load. It is unrelated, and it did not recur in the after run.)

## After the fix (dfce8dc): the same test alone, 4 runs in parallel
```
      Tests  1 passed | 44 skipped (45)
      Tests  1 passed | 44 skipped (45)
      Tests  1 passed | 44 skipped (45)
      Tests  1 passed | 44 skipped (45)
```

## After the fix (dfce8dc): the full frontend suite, 4 runs in parallel
```
      Tests  616 passed (616)
      Tests  616 passed (616)
      Tests  616 passed (616)
      Tests  616 passed (616)
```
