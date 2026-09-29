# BoardRoom

A multi-tenant project board — think a small Trello/Linear. Teams work inside **workspaces**,
each with its own members, roles, and Kanban-style **boards**.

Built to demonstrate a real full-stack SaaS pattern: JWT auth, per-workspace role-based
authorization (not just a global admin flag), and a typed React client — end to end, with
automated tests on both sides.

## Features

- **Auth** — register/login with JWT, PBKDF2-hashed passwords (no third-party auth dependency)
- **Workspaces** — create a workspace, invite teammates by email, assign them a role
- **Roles** — `Member < Admin < Owner`, enforced server-side on every workspace-scoped request,
  not just hidden in the UI
- **Boards** — each board gets three default columns (To Do / In Progress / Done); add more
- **Cards** — create, edit, assign to a workspace member, drag-and-drop between columns, delete
- **Dark/light aware** on the design side (light SaaS dashboard theme)

## Stack

| Layer | Tech |
| --- | --- |
| API | ASP.NET Core 9 (.NET 9), EF Core, SQLite |
| Auth | JWT bearer tokens, PBKDF2-SHA256 password hashing |
| Client | React 19, TypeScript, Vite, React Router |
| Tests | xUnit + `WebApplicationFactory` (API), Vitest + React Testing Library (client) |

No paid services or cloud accounts are required to run this locally.

## Project layout

```
server/
  BoardRoom.Api/         # the Web API (controllers, EF Core models, JWT auth)
  BoardRoom.Api.Tests/    # xUnit integration tests, run against an in-memory SQLite db
client/                   # React + TypeScript frontend (Vite)
```

## Running it locally

### API

```bash
cd server/BoardRoom.Api
dotnet run
```

The first run applies EF Core migrations automatically and creates `boardroom.db` (SQLite) next
to the project — no separate migration step needed. The API listens on the port .NET picks
(check the console output); update `client/.env.local` to match if it's not `5199`.

Run the backend tests:

```bash
cd server
dotnet test
```

### Client

```bash
cd client
cp .env.example .env.local   # adjust VITE_API_BASE_URL if needed
npm install
npm run dev
```

Run the frontend tests:

```bash
cd client
npm test
```

## Notes on the auth design

- Roles live on the **membership** (`WorkspaceMember`), not the user — the same person can be an
  `Owner` of one workspace and a plain `Member` of another.
- Every workspace-scoped endpoint checks membership *and* role server-side
  (`WorkspaceAuthorizationService`) — a non-member gets `403` even if they know a valid URL, and a
  plain `Member` can't invite people even by calling the API directly.
- The JWT signing key in `appsettings.json` is a placeholder for local development only — replace
  it (and move it to a secret store) before deploying anywhere real.

## What's intentionally left out

This is a portfolio-scale MVP, not a production SaaS. A few things that would come next:
real-time board updates (SignalR), email delivery for invites instead of "they must already have
an account", and audit logging on role changes.
