# 0010 Rate and connection limits

Status: accepted

Defaults live in `NestOptions` and can be overridden with `Nest__*` environment variables.

| Limit | Default |
| --- | --- |
| HTTP requests per client | 120 burst, 10 per second |
| WebSocket connections per client | 8 |
| WebSocket connections in total | 80, then 503 |
| Entries per client | 6 burst, 1 every 2 s |
| Entries kept by the server | 1,000 |
| Offered entries per circuit | 1,000 |
| Fetched ids per call | 100 |
| Keys per entry | 8 |
| Comment | 4,000 characters |
| Link | 2,048 characters |
| Hub message | 32 KB |

- A client is its IP after `X-Forwarded-For`, or its /64 for IPv6. Keys live in memory only and are never logged or shown.
- The hub (`/_blazor`) only accepts WebSocket connections.
- Invalid entries do not use up the rate limit.
- There is no moderation. The About page says plainly that content spreads to every client and cannot be recalled.
