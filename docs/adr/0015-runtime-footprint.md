# 0015 Memory and CPU settings

Status: accepted

- Workstation, non-concurrent GC, `ConserveMemory=7`, no tiered PGO, invariant globalization, English resources only.
- Disconnected circuits: 20 kept for one minute. Two unacknowledged render batches per circuit.
- WebSocket compression is off. Response compression (Brotli, gzip) at the fastest level.
- No ReadyToRun and no trimming: velixir runs a portable `dotnet publish`, and Blazor Server is not trim-safe.
- The server's home list is computed once per change of its entries and cached.
- Logs at `Warning`, except host lifetime messages. `/health` is exempt from rate limiting. Shutdown waits up to 10 s.
