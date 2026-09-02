# Result Publication V1

Status: current integration contract.

## Guarantees

The Manager inserts `ResultAvailable` in the same PostgreSQL statement that
fences the active lease and changes its processing unit to `Succeeded`. A
consumer therefore cannot observe an event for a non-terminal unit. Delivery is
at-least-once and isolated by stable consumer identifier.

The canonical JSON remains the content-addressed result artifact. When a visual
destination is configured, the filesystem adapter also publishes this readable
export by staging and one atomic directory rename:

```text
<destination>/<title>--<processing-unit-id>/
├── result.dpengine.json
├── visual-assets.manifest.json
└── visuals/
    └── ...
```

The readable directory is an export, not a second source of truth. PostgreSQL
retains its absolute location so a later Settings change cannot redirect an
already published result. A crash can leave an idempotently reusable export
before PostgreSQL completion because a filesystem rename and a PostgreSQL
transaction cannot share an atomic commit; it cannot create `ResultAvailable`
or a false `Succeeded` state.

## Authentication and claim lifecycle

Consumer routes use a service credential distinct from the administrative UI:

```text
X-Manager-Consumer-Key: <service credential>
X-Consumer-Id: <stable consumer identity>
```

Claim a result:

```http
POST /api/manager-consumers/results/claims
```

HTTP 200 returns `ResultReference`, `SubmissionId`, `ProcessingUnitId`, scope,
schema version, media type, byte length, SHA-256, availability time, claim token,
claim expiry and the finalized `SubmissionManifest`. HTTP 204 means no result
is currently claimable.

The manifest is format-neutral and contains the submission identity, immutable
revision, source SHA-256, original filename, finalization time and the complete
ordered set of expected processing-unit identities and scopes. Revision 1
describes the initial processing plan. Replacing that plan through a split
appends a new revision instead of mutating the previous one.

Every result claim carries the latest finalized manifest. A downstream consumer
can therefore distinguish a complete work from a partially delivered one and
must group parts by `SubmissionId`, never by filename or arrival order.

Payload and visual reads additionally require:

```text
X-Result-Claim-Token: <claim token>
```

```http
GET /api/manager-consumers/results/{resultReference}/content
GET /api/manager-consumers/results/{resultReference}/visuals
GET /api/manager-consumers/results/{resultReference}/visuals/{assetId}
```

The Manager verifies visual length and SHA-256 before returning its stream. The
consumer must independently verify every advertised hash before persistence.

After the raw result and required visuals have been durably persisted
downstream, acknowledge it:

```http
POST /api/manager-consumers/results/{resultReference}/ack
Content-Type: application/json

{"claimToken":"00000000-0000-0000-0000-000000000000"}
```

ACK is idempotent for the same consumer and token. It must not occur before the
downstream transaction commits. An expired, unacknowledged claim becomes
claimable again with a new token. An ACK from another consumer, another token or
an expired attempt is rejected.

## Downstream idempotence

Apologia owns its import transaction and must enforce uniqueness by
`ResultReference`. A replay with the same reference must also have the same
SHA-256; disagreement is an integrity failure, not a new result. Preserve the
raw DPEngine JSON before producing relational enrichments, chunks or embeddings.
