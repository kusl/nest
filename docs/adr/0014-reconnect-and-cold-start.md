# 0014 Reconnect, backoff, cold start

Status: accepted

- The free plan sleeps after 15 idle minutes and restarts without warning.
- Retries: first at once, then exponential with equal jitter, capped at 30 s, 30 attempts.
- `ReconnectBanner` shows "Reconnecting", then "Waking up the server", then a retry button. Retrying also happens when the tab becomes visible.
- A rejected reconnect means the server lost the circuit, so the page reloads and rebuilds from the server and IndexedDB.
- While not connected, pages show what this browser has, the status line says so, and send buttons are disabled.
