# DocumentProcessing.Manager.Host

This executable ASP.NET Core adapter composes the durable Manager, PostgreSQL,
filesystem custody and the consumer-facing DPEngine Host. It owns no processing
policy and never processes documents concurrently.

Required configuration:

```bash
export ConnectionStrings__ManagerPostgres='Host=127.0.0.1;Port=5432;Database=dpengine_manager;Username=...;Password=...'
export ManagerHost__ApiKey='replace-with-at-least-32-random-characters'
export ManagerHost__SourceRoot='/absolute/custody/sources'
export ManagerHost__ResultRoot='/absolute/custody/results'
```

The API key is required on every `/api/manager` request through the
`X-Manager-Api-Key` header. Do not expose plaintext HTTP beyond loopback; use a
TLS reverse proxy for remote access.

Submit exact source bytes idempotently with a caller-owned UUID:

```bash
curl --request PUT \
  --header 'X-Manager-Api-Key: ...' \
  --header 'X-Document-File-Name: document.pdf' \
  --header 'Content-Type: application/pdf' \
  --data-binary '@document.pdf' \
  http://127.0.0.1:5080/api/manager/submissions/00000000-0000-0000-0000-000000000001
```

Submission defaults to `?dispatch=shelve`, which retains the ordered unit but
keeps it ineligible for dispatch. Use `?dispatch=run` to create an immediately
eligible unit. This eligibility is independent of the global Manager state.

HTTP clients may send the original filename through the standard
`Content-Disposition: attachment; filename*=UTF-8''...` content header. The
legacy `X-Document-File-Name` request header remains supported for command-line
and existing clients.

Control and observation endpoints:

```text
GET  /api/manager/state
POST /api/manager/control/start
POST /api/manager/control/pause
POST /api/manager/control/resume
POST /api/manager/control/stop
GET  /api/manager/queue
PUT  /api/manager/queue/order
POST /api/manager/queue/{unitId}/release
GET  /api/manager/results/{resultReference}
GET  /health/live
GET  /health/ready
```

The Host applies Manager schema migrations before it starts listening. A
persisted `Running` state resumes automatically after restart; PostgreSQL
runtime and unit leases fence stale processes and recover expired work.
