# DocumentProcessing.Manager.Host

This executable ASP.NET Core adapter composes the durable Manager, PostgreSQL,
filesystem custody and the consumer-facing DPEngine Host. It owns no processing
policy and never processes documents concurrently.

Required configuration:

```bash
export ConnectionStrings__ManagerPostgres='Host=127.0.0.1;Port=5432;Database=dpengine_manager;Username=...;Password=...'
export ManagerHost__ApiKey='replace-with-at-least-32-random-characters'
export ManagerHost__ConsumerApiKey='replace-with-a-distinct-32-character-service-key'
export ManagerHost__SourceRoot='/absolute/custody/sources'
export ManagerHost__ResultRoot='/absolute/custody/results'
```

The API key is required on every `/api/manager` request through the
`X-Manager-Api-Key` header. Do not expose plaintext HTTP beyond loopback; use a
TLS reverse proxy for remote access.

The distinct consumer key is required on `/api/manager-consumers` through
`X-Manager-Consumer-Key`. Consumer calls also identify their durable delivery
state with `X-Consumer-Id`; this identifier must remain stable across restarts.

Submit exact source bytes idempotently with a caller-owned UUID:

```bash
curl --request PUT \
  --header 'X-Manager-Api-Key: ...' \
  --header 'X-Document-File-Name: document.pdf' \
  --header 'Content-Type: application/pdf' \
  --data-binary '@document.pdf' \
  http://127.0.0.1:5080/api/manager/submissions/00000000-0000-0000-0000-000000000001
```

Submission without a `dispatch` query uses the persisted Manager default,
initially `shelve`, which retains the ordered unit but keeps it ineligible for
dispatch. Use `?dispatch=run` or `?dispatch=shelve` to override that default for
one submission. This eligibility is independent of the global Manager state.

HTTP clients may send the original filename through the standard
`Content-Disposition: attachment; filename*=UTF-8''...` content header. The
legacy `X-Document-File-Name` request header remains supported for command-line
and existing clients.

Control and observation endpoints:

```text
GET  /api/manager/state
GET  /api/manager/settings
PUT  /api/manager/settings
POST /api/manager/control/start
POST /api/manager/control/pause
POST /api/manager/control/resume
POST /api/manager/control/stop
GET  /api/manager/queue
GET  /api/manager/archive
PUT  /api/manager/queue/order
POST /api/manager/queue/{unitId}/release
GET  /api/manager/results/{resultReference}
GET  /health/live
GET  /health/ready
```

Durable result-consumer endpoints:

```text
POST /api/manager-consumers/results/claims
GET  /api/manager-consumers/results/{resultReference}/content
POST /api/manager-consumers/results/{resultReference}/ack
GET  /api/manager-consumers/results/{resultReference}/visuals
GET  /api/manager-consumers/results/{resultReference}/visuals/{assetId}
```

`claims` returns the oldest available result and an expiring `claimToken`, or
HTTP 204 when no result is currently claimable. Persist and verify the streamed
payload using the advertised byte length and SHA-256, then ACK with
`{"claimToken":"..."}`. An expired unacknowledged claim is delivered again;
repeating a successful ACK with the same token is idempotent. The default claim
duration is five minutes and can be changed with
`ManagerHost__ConsumerClaimSeconds`.

Content and visual reads require both `X-Consumer-Id` and the active
`X-Result-Claim-Token`. See
[`Result Publication V1`](../../docs/integration/result-publication-v1.md) for
the complete downstream transaction and integrity contract.

Manager settings persist the default submission behavior, an absolute
visual-destination directory and the number of days terminal work remains in
the recent queue view. The Host validates that this directory already exists
and is writable before accepting it. `/archive` applies the same durable
retention boundary and provides bounded title/date filtering and title/date
sorting without changing the processing-unit lifecycle state. During
processing, visual bytes and `result.dpengine.json` are staged below that root,
verified against their byte lengths and SHA-256 digests, then atomically
published into one read-only subdirectory per processing unit with a manifest
and `visuals/` child. A document that requires visual preservation
fails with an actionable diagnostic when no destination has been configured.

The Host applies Manager schema migrations before it starts listening. A
persisted `Running` state resumes automatically after restart; PostgreSQL
runtime and unit leases fence stale processes and recover expired work.

PP-StructureV3 and PaddleOCR are managed lazily by `DocumentProcessingHost` by
default: Docker is not touched for native-only work, and the required pinned
provider starts only when the Engine plans that enrichment. Set
`ManagerHost__ProviderLifecycle=external` when deployment infrastructure owns
both endpoints instead. `ManagerHost__ProviderRepositoryRoot` can identify an
absolute source checkout when a missing pinned image must be built locally.
