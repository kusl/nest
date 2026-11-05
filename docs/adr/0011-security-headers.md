# 0011 Security headers, CSP, origin check

Status: accepted

- CSP: `default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'`. No nonce is needed.
- `<ImportMap />` is left out because it emits an inline script. Modules load from fingerprinted URLs through `Assets[...]`.
- `Blazor.start` is called from `App.razor.js`. The reconnect UI is our own element. No markup uses `style` attributes.
- Also sent: `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Permissions-Policy` denying camera, microphone, geolocation, payment and USB, `Cross-Origin-Opener-Policy`, `Cross-Origin-Resource-Policy`, HSTS outside development. No `Server` header.
- Outbound listing links open in a new tab with `rel="noopener noreferrer"`.
- `/_blazor` requests with an `Origin` header must match the request host or `Nest:AllowedOrigins`.
- User text is only ever set with `textContent` or Razor encoding.
