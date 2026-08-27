# DocumentProcessing.Manager.Blazor

This server-side Blazor adapter presents the Manager workshop without taking a
compile-time dependency on Manager Core, PostgreSQL, DPEngine or document
formats. It consumes only the authenticated HTTP contract exposed by
`DocumentProcessing.Manager.Host`.

Required configuration:

```bash
export ManagerApi__BaseAddress='http://127.0.0.1:5080'
export ManagerApi__ApiKey='the-same-at-least-32-character-key-as-the-host'
```

Run locally:

```bash
dotnet run --project src/DocumentProcessing.Manager.Blazor
```

The API key remains in the server process and is never sent to the browser.
The workshop provides Manager controls, read-only pending, active and completed
views, and a reusable sprite-animated librarian whose deterministic states
follow Manager observations. Queue reordering, source submission and result
download remain later increments.

The librarian waits when the queue has no active work, reads while a unit is
active, holds its page while paused, rests while stopped and reports a lost Host
connection. A newly observed successful unit triggers one short celebration;
existing history and failures never do. CSS respects `prefers-reduced-motion`.
The component exposes `ImageSource` when an embedding host needs to serve the
sprite sheet from a different static-assets path.

`Home.razor` is only the standalone route shell. The reusable
`ManagerWorkshop` component owns the workshop presentation and its isolated
styles, so embedding it does not restyle the host application. A server-side
Blazor host such as Apologia Studio can register its dependencies and embed it:

```csharp
builder.Services.AddDocumentProcessingManagerWorkshop(
    builder.Configuration);
```

```razor
<ManagerWorkshop />
```

The UI itself has no end-user authentication yet. Keep it bound to loopback or
behind an authenticated reverse proxy.
