// Runs before the page paints (TREK-56), so it never flashes the wrong theme. A file, not an inline script, because
// the web CSP (frontend/Caddyfile) and the desktop app allow only 'self' scripts. Keep in step with src/theme/theme.ts.
;(function () {
  var choice = 'system'
  try {
    var saved = window.localStorage.getItem('trekky.theme')
    if (saved === 'light' || saved === 'dark' || saved === 'system') choice = saved
  } catch (e) {
    // Storage blocked (private window, policy): follow the system.
  }
  var dark =
    choice === 'dark' ||
    (choice === 'system' && typeof window.matchMedia === 'function' && window.matchMedia('(prefers-color-scheme: dark)').matches)
  document.documentElement.classList.toggle('dark', dark)
})()
