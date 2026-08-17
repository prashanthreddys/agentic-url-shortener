# URL Shortener + Agentic SDLC Orchestration

A local, end-to-end C#/.NET solution built for the "Agentic Software Engineering System" assignment.
It has two halves that reinforce each other:

1. **The product**: a working **URL shortener** service (ASP.NET Core Web API + MongoDB) with core
   APIs, click analytics, and reliability/security features.
2. **The differentiator**: an **agentic SDLC orchestration engine** that coordinates the full software
   lifecycle (requirements, design, implementation, testing, documentation, release) as a governed,
   stateful, non-linear workflow, and demonstrates it on three scenarios (greenfield, brownfield,
   ambiguous).

> Principle followed throughout: **agents execute inside defined autonomy boundaries; humans own
> oversight, approvals, and final quality.**

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the system design and
[docs/SCENARIOS.md](docs/SCENARIOS.md) for a walkthrough of the three scenarios.

---

## Solution layout

| Project | Type | Responsibility |
| --- | --- | --- |
| [src/UrlShortener.Core](src/UrlShortener.Core) | class lib | Domain entities, base62 encoding, validation (SSRF guardrails), shortening service, MongoDB persistence, Zookeeper-style counter allocator |
| [src/UrlShortener.Api](src/UrlShortener.Api) | web api | REST endpoints, redirect, rate limiting, health, Swagger |
| [src/UrlShortener.Orchestration](src/UrlShortener.Orchestration) | class lib | The agentic SDLC engine: dependency graph, gates, guardrails, approvals, retries, rollback, re-planning, observability, metrics |
| [src/UrlShortener.Orchestration.Runner](src/UrlShortener.Orchestration.Runner) | console | Runs the three governed SDLC scenarios with real LLM (Ollama) agents and prints audit-grade reports |
| [tests/UrlShortener.Core.Tests](tests/UrlShortener.Core.Tests) | xUnit | Unit tests for the product (encoding, validation, service, counter allocation) |
| [tests/UrlShortener.Api.Tests](tests/UrlShortener.Api.Tests) | xUnit | In-process HTTP integration tests (`WebApplicationFactory`) over the real API |
| [tests/UrlShortener.Orchestration.Tests](tests/UrlShortener.Orchestration.Tests) | xUnit | Engine tests (graph, gates, retry, rollback, approvals, re-plan, parallelism) |

---

## Prerequisites

- .NET SDK 8.0 or later (the projects target `net8.0`). Check with `dotnet --version`.
- A **MongoDB** server running on `localhost:27017` (the only persistence provider). Install MongoDB
  Community Server natively (it runs as a Windows service), or point at a MongoDB Atlas cluster.
- **Ollama** with a pulled model (`ollama pull llama3.2`) for the orchestration demo: every SDLC stage
  is a real local LLM agent. Install from https://ollama.com/download.
- The API integration tests require the running MongoDB on `:27017`; the orchestration demo requires
  Ollama (no database). The unit and engine tests need neither. No Docker is required anywhere.

See [docs/SETUP.md](docs/SETUP.md) for full, step-by-step setup instructions.

## Setup and run

From the repository root (`c:\Code\URL-Shortner`):

```powershell
# 1. Restore + build everything
dotnet build UrlShortener.slnx

# 2. Run the full test suite (unit + engine tests need no DB; API tests use the local MongoDB)
dotnet test UrlShortener.slnx

# 3. Run the agentic orchestration demo with real LLM agents (requires Ollama; see below)
dotnet run --project src/UrlShortener.Orchestration.Runner -- all
#   or a single scenario:
dotnet run --project src/UrlShortener.Orchestration.Runner -- greenfield
dotnet run --project src/UrlShortener.Orchestration.Runner -- brownfield
dotnet run --project src/UrlShortener.Orchestration.Runner -- ambiguous
#   greenfield can also emit a runnable project scaffold to a chosen folder:
dotnet run --project src/UrlShortener.Orchestration.Runner -- greenfield --out C:\Test_Greenfield

# 4. Run the URL shortener API (requires a MongoDB running on localhost:27017)
$env:ASPNETCORE_URLS="http://localhost:5080"; $env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/UrlShortener.Api
#   Swagger UI: http://localhost:5080/swagger
```

### Persistence (MongoDB)

The service uses **MongoDB** exclusively (a NoSQL, horizontally scalable document store, the natural
fit for the `code -> longUrl` key-value access pattern). Run a native MongoDB Community Server (it
listens on `localhost:27017`) or use a MongoDB Atlas cluster. Configuration:

- Connection string: `ConnectionStrings:Mongo` (default `mongodb://localhost:27017`).
- Database name: `Persistence:Database` (default `urlshortener`).

On startup the API creates the required indexes. Short codes are allocated from a **distributed atomic
counter document** in MongoDB (the Zookeeper-style range allocator), so codes never collide across
servers and inserts need no per-row check. Persistence sits behind `IShortUrlRepository`, so the
store is swappable without touching the service or controllers.

### Real AI agents (Ollama)

The orchestration engine runs stages as `IStageAgent` implementations. Every stage in all three
scenarios (requirements, design, implementation, testing, documentation, release) is a **real local
LLM agent** (`LlmStageAgent` backed by Ollama), running under the governance (gates, security
guardrail, human approval, bounded retries, parallel test/documentation wave, rollback, re-plan,
metrics).

Setup (free, local, no API key):

1. Install Ollama from https://ollama.com/download.
2. Pull a model: `ollama pull llama3.2` (or any chat model; `ollama serve` usually starts on boot).
3. Run any scenario, optionally with your own requirement and model:

   ```powershell
   dotnet run --project src/UrlShortener.Orchestration.Runner -- all
   #   optional: provide your own requirement and/or model
   $env:OLLAMA_MODEL="llama3.2"
   dotnet run --project src/UrlShortener.Orchestration.Runner -- greenfield "Build a link shortener with QR codes"
   ```

The run prints the governed audit trail and metrics plus the full model-generated artifacts (also
written to `output\<timestamp>_<scenario>\`). If Ollama is not running or the model is not pulled, the
command prints setup instructions and exits cleanly. The `IStageAgent` seam means a different agent
(a tool, a script, or a stronger model) could be swapped in without changing the orchestration engine.

---

## API reference

Base URL in the examples below: `http://localhost:5080`.

| Method | Route | Purpose | Success | Errors |
| --- | --- | --- | --- | --- |
| `POST` | `/api/urls` | Create a short link | `201 Created` | `400` invalid URL, `429` rate limited |
| `GET` | `/{code}` | Resolve + redirect (records a click) | `302 Found` | `404` unknown, `410` disabled |
| `GET` | `/api/urls/{code}` | Fetch link metadata | `200 OK` | `404` |
| `GET` | `/api/urls/{code}/stats` | Click analytics | `200 OK` | `404` |
| `DELETE` | `/api/urls/{code}` | Delete a link | `204 No Content` | `404` |
| `GET` | `/health` | Liveness probe | `200 OK` | |

### Examples

```powershell
# Create a short link (a collision-free counter-based Base62 code is generated)
Invoke-RestMethod -Method Post http://localhost:5080/api/urls -ContentType application/json `
  -Body '{"longUrl":"https://example.com/some/long/path"}'

# Follow the short link (302 to the destination, records a click). Use the returned code:
curl.exe -i http://localhost:5080/<code>

# Analytics
Invoke-RestMethod http://localhost:5080/api/urls/<code>/stats
```

---

## Engineering features (the product)

- **Collision-free code generation (Zookeeper-style)**: short codes are the Base62 encoding of a
  unique counter value served from reserved counter *ranges* (`ICounterRangeProvider` +
  `IRangeAllocator`). Each server consumes its own range locally, so codes never collide across
  servers and new links are inserted **without a per-row collision check**. The allocator is
  file-backed locally and would be Zookeeper-backed in a distributed deployment. A random base62
  generator is also included as an alternative strategy.
- **Idempotent create**: shortening the same (normalized) destination URL again returns the existing
  short link instead of creating a duplicate.
- **Analytics**: each redirect records a click (timestamp, referer, user-agent) with the client IP
  stored only as a salted hash, so unique-visitor counts work without retaining raw PII.
- **Security guardrails**: only `http`/`https` destinations; links to loopback/private ranges
  (`localhost`, `127.0.0.0/8`, `10/8`, `172.16/12`, `192.168/16`, `169.254/16`) are rejected to
  reduce SSRF / cloud-metadata abuse.
- **Reliability**: fixed-window rate limiting on link creation; health endpoint; clean typed error
  results mapped to correct HTTP status codes.
- **Clean layering**: controllers -> `UrlShorteningService` -> `IShortUrlRepository` (MongoDB). The
  service has no dependency on the web host or on the database driver, which keeps it unit-testable.

---

## Testing approach

- **Unit tests** (`UrlShortener.Core.Tests`) cover base62 encoding, URL validation (including
  SSRF rejection), the shortening service using a fast in-memory repository fake, and the
  Zookeeper-style counter range allocation.
- **HTTP integration tests** (`UrlShortener.Api.Tests`) host the real API in-process with
  `WebApplicationFactory`, connected to the locally running MongoDB (a throwaway database that is
  dropped after the run), and exercise the full stack: create, 302 redirect + click recording, stats,
  and the error paths including SSRF rejection.
- **Engine tests** (`UrlShortener.Orchestration.Tests`) verify graph validation (cycle/missing-dep
  detection, topological order), the happy path, entry-gate holds, bounded-retry recovery, terminal
  failure -> rollback + safe-stop, rejected-approval safe-stop (no rollback), guardrail-denial
  rollback, dynamic re-planning, and real parallel execution of independent stages.

Run all with `dotnet test UrlShortener.slnx` (a MongoDB running on localhost:27017 is required for the API integration tests).

---

## Limitations and trade-offs
- **Small local LLM agents.** Every SDLC stage is a real local LLM agent (Ollama). Output quality is
  bounded by the small local model, so generated code is a skeleton rather than a guaranteed-compiling
  project, and runs take longer than a mocked pipeline. The engineering value is the governed
  orchestration around the agents (gates, guardrails, approvals, retries, rollback, re-planning,
  metrics); the `IStageAgent` seam lets a stronger model or a real code/test/doc generator plug in
  unchanged.
- **Index creation on startup.** The API ensures MongoDB indexes exist on startup for a frictionless
  demo. A production build would manage indexes and any schema/versioning via a migration process.
- **Rollback is compensating, not transactional.** Stage rollbacks are logical compensations
  (`IRollbackAction`), appropriate for cross-system SDLC steps that cannot share a DB transaction.
- **Wave-based parallelism.** The orchestrator schedules ready stages in synchronized parallel waves.
  This is simple and correct for a DAG, but a faster independent branch waits at the wave barrier
  rather than racing fully ahead. A work-stealing scheduler would remove that barrier.
- **In-process, single node.** Governance, audit, and metrics live in memory for the run. Durable,
  resumable, multi-node execution (persisted state, external queue) is out of scope for the prototype.
- **Analytics are synchronous.** Clicks are written inline on the redirect path. The brownfield
  scenario explicitly models moving this off the hot path as a re-planned improvement.
- **Counter codes are sequential (guessable).** Zookeeper-style counter codes trade non-guessability
  for collision-free, check-free inserts, so codes can be enumerated. Switch to the random generator
  where unpredictable codes matter. Locally the counter is file-backed and its range reservation is
  process-safe but not a substitute for real distributed coordination.
See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full rationale and control-flow details.
