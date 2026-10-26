# LIOR_CAL

Vue 3 + TypeScript frontend and ASP.NET Core (.NET 10) backend for the existing SQL Server database manager. The original MAUI application remains intact.

## Structure

- `frontend/`: Vue forms, table availability, per-section change tracking, preview table, and system/light/dark appearance.
- `backend/`: HTTP endpoints and request validation. The original C# models, services, and parser are included in this backend, preserving SQL queries, ID parsing, read-only preview, and transactional Apply. This directory can be copied and built independently of the MAUI app.
- `tests/api_smoke.py`: non-destructive HTTP validation checks.

## Development

Requires Node.js 22.12+ (validated with 24.19), npm, Python 3 for smoke checks, and .NET SDK 10 (validated with 10.0.403). No MAUI workload is needed for the web version.

From `LIOR_CAL/frontend`:

```sh
npm ci
npm run build
npm run dev -- --port 5173
```

In another terminal, from `LIOR_CAL/backend`:

```sh
dotnet build
# Keep the API private to the local machine.
dotnet run --no-build --urls http://127.0.0.1:5080
```

Vite proxies `/api` to port 5080; both processes are needed. The root MAUI solution remains a desktop-only build and is not the web build target. In this cloud environment, the SDK is at `/workspace/.dotnet/dotnet`; set `DOTNET_CLI_HOME=/workspace/.dotnet-home` and `NUGET_PACKAGES=/workspace/.nuget/packages` when invoking it. The container sandbox may require execution permission for .NET shared-memory mutexes.

With both servers running, from `LIOR_CAL`:

```sh
python3 tests/api_smoke.py
```

The smoke test verifies API health, nine invalid-request cases, the Vite-to-API proxy, and the frontend HTML entry. It never connects to or modifies SQL Server. Frontend production compilation includes TypeScript checks.

## Workflow

Connect using a server, SQL username, and password. Select an accessible database. Required-table checks enable Overall, Gain, Amplitude, and Zakan sections where supported. Enable and edit sections; use Preview for read-only insert/update counts and before/after rows. Apply asks for confirmation and sends only changed, enabled sections in one SQL transaction. A successful Apply records per-section baselines; switching databases clears them.

ID lists and ranges are parsed by the original C# parser. Empty time fields resolve to the current epoch milliseconds for each Preview or Apply request. Explicit dates are interpreted using the browser timezone. Preview warns that current-time values are recalculated on Apply. SQL Server certificate validation is always enabled; install a trusted certificate for the server rather than disabling validation.

## API

- `GET /api/health`
- `POST /api/connect`: `{ server, user, password }`
- `POST /api/database`: `{ connection: { server, user, password }, database }`
- `POST /api/preview` and `/api/apply`: connection/database plus optional `gain`, `zakan` (0/1), `overall`, and `amplitude` sections. See the frontend request builder and backend input records for the complete contract.

Credentials are held in browser memory and supplied per request; they are not stored in localStorage or configuration. SQL connections are scoped to each service call. This is a local development application: before exposing it to other users, add application authentication/authorization, HTTPS ingress, and deployment configuration. Do not expose the unauthenticated API publicly.

## Validation limits

The backend and frontend builds and the 12 HTTP checks pass. The new backend explicitly references a patched PKCS package instead of the vulnerable version inherited from SqlClient. A live SQL Server connection, table inspection, preview accuracy, and transactional writes still require validation against your test database. No database changes were attempted during migration. Browser interaction has not been automated. The original desktop app was not rebuilt in this Linux environment.
