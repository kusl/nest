# 0009 Per-client outboxes over the circuit

Status: accepted

- Transport is the Blazor circuit: JavaScript calls `[JSInvokable]` methods (`Join`, `Ping`, `Fetch`, `Offer`, `Post`); the server calls the module's `apply`. No separate SignalR hub.
- Each island owns a bounded `Outbox<NestUpdate>` drained by one pump that sends batches in order and waits for the browser. A full outbox, a failed call or a 20 s stall drops the subscriber.
- The pump waits until the module is attached, because `Join` can arrive before `attach` returns to .NET.
- Subscribing enqueues the snapshot under the same lock as updates, so nothing is missed or duplicated.
- Deltas carry `version` and `previous`; a gap triggers a fresh `Join`. `apply` does not wait for fetching and offering, so the outbox never stalls on them.
- The browser pings every 25 s and rejoins when the server says it was dropped, and after every reconnect.
