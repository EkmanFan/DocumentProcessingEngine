# Apologia human session bridge V1

Status: accepted contract for `AS-ID-04`.

## Boundary

Apologia Studio remains the authority for human accounts, groups, roles and
permissions. The Manager does not copy that directory and does not accept an
Apologia password. The Manager Host remains a private machine-to-machine API
protected by its existing service keys; only the Manager Blazor server calls it
on behalf of an authenticated human.

## Sign-in flow

```text
Apologia authenticated session
    -> GET /document-manager/connect
    -> signed, 30-second, one-use ticket
    -> POST Manager /auth/apologia/exchange
    -> Manager HTTP-only session cookie
    -> Manager Blazor workshop
```

The ticket is posted in a form body rather than placed in a URL. It contains the
Apologia user identifier, display name, e-mail address, interface language,
the three Manager permissions, issue and expiry times, and a cryptographically
random nonce. HMAC-SHA-256 authenticates the payload with a shared bridge key.

The Manager verifies the signature, issuer, audience, bounded lifetime and
nonce before opening a session. A nonce can be consumed only once. Tickets and
exchange responses are never cached. Direct access to the Manager challenges
through Apologia instead of presenting an independent login form.

## Authorization

The Manager recognizes only these permission identifiers:

- `manager.operate`: open and operate the workshop;
- `manager.delivery.replay`: make a completed delivery available again;
- `manager.custody.purge`: permanently delete eligible custody data.

The Blazor server checks permissions before exposing or executing replay and
purge operations. UI visibility is convenience only; the server-side check is
the authorization boundary. The existing Host API keys remain separate and
must never be placed in the browser ticket or cookie.

Manager sessions are short-lived and non-persistent. Their authorization state
is re-established from a fresh Apologia ticket after expiry, which bounds stale
access after a role change or account suspension. V1 uses an in-memory nonce
store and therefore targets the current single Manager UI instance. A future
multi-instance deployment must replace it with a distributed atomic store.

## Deployment requirements

- Both applications must receive the same bridge key through secret
  configuration. The checked-in local launcher default is development-only.
- Production uses HTTPS and hosts Studio and Manager in a same-site topology so
  the Manager session cookie works when embedded.
- The Manager Host stays private or loopback-only.
- Logs must not contain tickets, shared keys or session cookies.

## Acceptance cases

- a Reader cannot obtain or use a Manager session;
- a Document Operator can operate and replay but cannot purge custody;
- an Administrator can operate, replay and purge custody;
- an expired, altered, wrong-audience or replayed ticket is rejected;
- opening the Manager directly returns through the Apologia connection flow;
- backend result notification and claim/ack continue to use service identities.
