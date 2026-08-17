# Setup Instructions

This guide gets the solution building, tested, and running end to end on a local Windows machine.
It covers the two halves of the project: the **URL shortener API** (the product) and the **agentic
SDLC orchestration** demo (the differentiator).

---

## 1. Prerequisites

| Tool | Why | Needed for |
| --- | --- | --- |
| **.NET SDK 8.0+** | Builds and runs every project (all target `net8.0`). | Everything |
| **MongoDB** (native) | The only persistence provider; listens on `localhost:27017`. | The API + the API integration tests |
| **Ollama** + a model | Every orchestration stage is a real local LLM agent. | The orchestration demo (section 4) |

- Install the .NET SDK from https://dotnet.microsoft.com/download and verify with `dotnet --version`.
- Install **MongoDB Community Server** from https://www.mongodb.com/try/download/community. It installs
  as a Windows service and listens on `localhost:27017`. A MongoDB Atlas connection string also works.
  **No Docker is required anywhere.**
- Install **Ollama** from https://ollama.com/download, then pull a model:

  ```powershell
  ollama pull llama3.2
  ```

  Ollama normally starts as a background service after install. Confirm it is up with `ollama list`.

---

## 2. Get the code and build

From the repository root (`c:\Code\URL-Shortner`):

```powershell
# Restore and build the whole solution
dotnet build UrlShortener.slnx
```

A clean build reports `0 Error(s)`.

---

## 3. Run the tests

```powershell
# Full suite: unit + engine + API integration tests
dotnet test UrlShortener.slnx
```

- The **unit** tests (`UrlShortener.Core.Tests`) and **engine** tests
  (`UrlShortener.Orchestration.Tests`) need no database.
- The **API integration** tests (`UrlShortener.Api.Tests`) connect to the native MongoDB on
  `localhost:27017`, using a throwaway database that is dropped after the run. Start MongoDB first.

To run one project only:

```powershell
dotnet test tests/UrlShortener.Orchestration.Tests/UrlShortener.Orchestration.Tests.csproj
```

---

## 4. Run the agentic orchestration demo (real LLM agents)

Every stage (requirements, design, implementation, testing, documentation, release) is a real local
LLM agent running under the governed pipeline (gates, guardrails, approvals, retries, rollback,
re-planning, metrics). Make sure Ollama is running and the model is pulled (section 1).

```powershell
# All three scenarios back to back
dotnet run --project src/UrlShortener.Orchestration.Runner -- all

# Or one scenario at a time
dotnet run --project src/UrlShortener.Orchestration.Runner -- greenfield
dotnet run --project src/UrlShortener.Orchestration.Runner -- brownfield
dotnet run --project src/UrlShortener.Orchestration.Runner -- ambiguous
```

- When a stage pauses for a **human approval**, type `y` and press Enter.
- Each run writes its model-generated files to `output\<timestamp>_<scenario>\`.
- You can pass your own requirement text and pick the model with environment variables:

  ```powershell
  $env:OLLAMA_MODEL = "llama3.2"          # optional; defaults to llama3.2
  $env:OLLAMA_URL   = "http://localhost:11434"  # optional; default Ollama endpoint
  dotnet run --project src/UrlShortener.Orchestration.Runner -- greenfield "Build a link shortener with QR codes and per-link analytics"
  ```

- If Ollama is not reachable or the model is not pulled, the runner prints setup instructions and
  exits cleanly.

### Generate a runnable project scaffold (greenfield)

The greenfield pipeline's implementation stage emits a validated scaffold document,
`SCAFFOLD_UrlShortener.md`. Pass `--out <dir>` to drop it at a location of your choice:

```powershell
dotnet run --project src/UrlShortener.Orchestration.Runner -- greenfield --out C:\Test_Greenfield
```

The console prints `Scaffold written to: <dir>\SCAFFOLD_UrlShortener.md`. That file contains a
PowerShell script (and instructions) that generate a complete, compiling, runnable URL shortener.
A developer, or an AI assistant, can then run the embedded script verbatim to produce the project;
it is deterministic and known-good, so the result compiles and its tests pass every time.

Expected outcome per scenario:

| Scenario | Expected result |
| --- | --- |
| `greenfield` | `Succeeded` (full pipeline, parallel test + docs, human-approved release) |
| `brownfield` | `Succeeded`, with a bounded retry and a dynamic re-plan |
| `ambiguous` | `SafeStopped`: holds for clarification, then a security guardrail denies release and rolls back |

---

## 5. Run the URL shortener API (the product)

Make sure native MongoDB is running on `localhost:27017`, then:

```powershell
$env:ASPNETCORE_URLS="http://localhost:5080"; $env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/UrlShortener.Api
```

Open the Swagger UI at **http://localhost:5080/swagger** and try:

| Do this | Expected result |
| --- | --- |
| `POST /api/urls` with `{ "longUrl": "https://example.com/very/long/path" }` | A 7-character Base62 code |
| `POST` the same URL again | The same code (idempotent create / dedupe) |
| Open the short URL in a browser | `302` redirect to the original URL |
| `GET /api/urls/{code}/stats` | Click count and recent clicks |
| `GET /api/urls` | Paginated list |
| `DELETE /api/urls/{code}` | `204 No Content` |
| `POST` with `{ "longUrl": "http://169.254.169.254/..." }` | `400 Bad Request` (SSRF / private host blocked) |

---

## 6. Configuration

Settings live in [src/UrlShortener.Api/appsettings.json](../src/UrlShortener.Api/appsettings.json) and
can be overridden with environment variables:

| Setting | Default | Override (env var) |
| --- | --- | --- |
| MongoDB connection string | `mongodb://localhost:27017` | `ConnectionStrings__Mongo` |
| Database name | `urlshortener` | `Persistence__Database` |
| Ollama endpoint | `http://localhost:11434` | `OLLAMA_URL` |
| Ollama model | `llama3.2` | `OLLAMA_MODEL` |

On startup the API creates the required MongoDB indexes automatically.

---

## 7. Troubleshooting

| Symptom | Fix |
| --- | --- |
| Runner prints "Cannot reach Ollama" | Start Ollama (`ollama serve`) and confirm with `ollama list`. |
| Runner prints "model ... is not pulled" | Run `ollama pull llama3.2` (or set `OLLAMA_MODEL` to an installed model). |
| API or API tests fail to connect | Ensure the MongoDB Windows service is running and listening on `localhost:27017`. |
| Port 5080 is in use | Change `ASPNETCORE_URLS` to a free port. |

---

See [ARCHITECTURE.md](ARCHITECTURE.md) for the system design and control flow, and
[SCENARIOS.md](SCENARIOS.md) for a walkthrough of the three scenarios.
