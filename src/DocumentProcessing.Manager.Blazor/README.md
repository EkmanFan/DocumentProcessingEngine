# DocumentProcessing.Manager.Blazor

This server-side Blazor adapter presents the Manager workshop without taking a
compile-time dependency on Manager Core, PostgreSQL, DPEngine or document
formats. It consumes only the authenticated HTTP contract exposed by
`DocumentProcessing.Manager.Host`.

Required configuration:

```bash
export ManagerApi__BaseAddress='http://127.0.0.1:5080'
export ManagerApi__ApiKey='the-same-at-least-32-character-key-as-the-host'
# Optional; defaults to 2 GiB and should not exceed the Host custody limit.
export ManagerApi__MaximumUploadBytes='2147483648'
# Optional; source streaming defaults to one hour independently of short API calls.
export ManagerApi__SubmissionTimeoutSeconds='3600'
# Optional; result streaming through the authenticated Blazor circuit also defaults to one hour.
export ManagerApi__ResultDownloadTimeoutSeconds='3600'
```

Run locally:

```bash
./scripts/run-manager-dev.sh
```

The repository-level launcher starts or reuses the development PostgreSQL
container, waits for the Manager Host readiness endpoint and then starts this
Blazor application. Both application processes stop together on `Ctrl+C`;
PostgreSQL remains available for the next run. The launcher also creates and
prints the development visual directory that can be selected in Settings.

The API key remains in the server process and is never sent to the browser.
The workshop provides Manager controls, pending, active and completed views,
and a reusable sprite-animated librarian whose deterministic states follow
Manager observations. Successful retained results can be streamed from the
authenticated server-side client and downloaded without exposing the Manager
API key or an unauthenticated result URL to the browser. The current browser
adapter materializes the result payload before saving it, which is appropriate
for the JSON-only V1 result but not for future large visual-asset bundles.

The complete animation stage is a permanent PDF/EPUB file input: clicking it or
dropping one file on the librarian streams the exact source to the authenticated
Host without buffering it in application memory. Drag-ready, uploading and
failure affordances are layered over the scene; they never replace the
animation. Host rejection details are displayed in the failure overlay. The
submission adapter sends the standard filename metadata and, for ASCII names,
the legacy header required by earlier Manager Hosts during a rolling restart.
A successful custody registration briefly shows the librarian's
reception reaction and refreshes the queue. The Host remains solely responsible
for content hashing, immutable custody and atomic processing registration.

The persisted reception toggle initially defaults to `Shelve`: the source
enters the durable global order but cannot be claimed until the user selects
`Run` on that queue item.
Selecting `Run` before reception makes the new unit immediately eligible while
still respecting the Manager's global Start/Pause/Stop state and strictly
sequential dispatcher. Queue items can be moved earlier or later across
documents and future chapter units. The same order can be changed by dragging
the dedicated grip on a pending card; the explicit move commands remain
available in its actions menu for keyboard access and as a deterministic
fallback. The public drop-zone component
applies the default submission behavior configured in the Manager settings;
the setting remains editable in the Manager settings dialog.

The settings dialog also accepts an absolute visual-destination path. The Host,
not the browser, verifies that the directory exists and is writable before the
setting is persisted. The standalone Linux adapter can open `kdialog` or
`zenity`; pasting a path always remains available. An embedding host such as
Apologia Studio can replace `IManagerVisualDestinationPicker` with its own
desktop-shell adapter without changing the reusable workshop component.

The desktop workshop grid has a bounded height so a growing result history does
not move the librarian. `Processed` scrolls independently; its scrollbar is
revealed on hover or keyboard focus and a small persistent marker indicates
hidden overflow. The retention period is persisted in Settings (30 days by
default). Older succeeded and failed units remain available in a separate,
paged Archives dialog with title/date filters and title/date ordering.

The librarian waits when the queue has no active work, reads while a unit is
active, holds its page while paused, rests while stopped and reports a lost Host
connection. A newly observed successful unit triggers one short celebration;
existing history and failures never do. CSS respects `prefers-reduced-motion`.
The component exposes `ImageSource` when an embedding host needs to serve the
sprite sheet from a different static-assets path.

English and French resources cover the complete workshop and animation. The
standalone executable uses English as its default culture. The reusable
component does not own a language setting: it reads the ambient .NET
`CurrentCulture` and `CurrentUICulture`. An embedding application such as
Apologia Studio therefore applies its application-wide language setting once,
and `ManagerWorkshop` follows it without a component parameter or a second
configuration source. The standalone host honors the standard
`.AspNetCore.Culture` cookie and otherwise falls back to English.

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

`AddDocumentProcessingManagerWorkshop` registers localization services but
does not configure or override the host application's supported cultures or
request-culture providers. Its assembly-owned resource location also remains
independent from any resource directory configured by the host.

The UI itself has no end-user authentication yet. Keep it bound to loopback or
behind an authenticated reverse proxy.
