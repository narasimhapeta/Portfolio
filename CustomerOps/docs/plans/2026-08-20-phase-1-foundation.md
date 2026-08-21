# CustomerOps Phase 1 — Project Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the CustomerOps solution/repo skeleton so `React → .NET API` works end-to-end locally (health check round-trip), with Clean Architecture project boundaries and environment-based config already in place.

**Architecture:** ASP.NET Core Web API (controllers) split into Api/Application/Domain/Infrastructure projects; a thin Vite + React + TypeScript SPA with one page (`CustomerOperationsPage`) using TanStack Query to call the API. No database, Redis, or Service Bus yet — those are follow-up plans. This plan corresponds to the original "Phase 1 — Project Foundation" step of the roadmap; SQL-backed Customer CRUD (the rest of the roadmap's coarse "Phase 1: React + .NET + SQL" milestone) is deliberately a **separate, subsequent plan** so this one stays reviewable in one sitting.

**Tech Stack:** .NET 10 SDK (verified 10.0.302), ASP.NET Core Web API with controllers, xUnit + `Microsoft.AspNetCore.Mvc.Testing`; Node 24.18.0 / npm 11.16.0, Vite + React 18 + TypeScript, `@tanstack/react-query`, Vitest + React Testing Library.

**Spec:** [../architecture.md](../architecture.md)

## Global Constraints

- .NET SDK 10.0.302, target framework `net10.0` (default for `dotnet new` on this machine)
- Node v24.18.0 / npm 11.16.0
- Backend layout: `src/CustomerPortal.{Api,Application,Domain,Infrastructure}` per architecture.md §5 — no extra projects, no microservices
- Frontend: React + TypeScript + Vite, server state via TanStack Query only — no Redux
- Environments: `Development`, `Test`, `Production` — no environment-specific code branches, config only
- No authentication, no SignalR, no real persistence in this plan (deferred per architecture.md §9)

---

### Task 1: .NET Solution Skeleton + Health Endpoint

**Files:**
- Create: `CustomerOps/CustomerPortal.sln`
- Create: `CustomerOps/src/CustomerPortal.Api/` (from `dotnet new webapi`)
- Create: `CustomerOps/src/CustomerPortal.Application/` (from `dotnet new classlib`, emptied of template cruft)
- Create: `CustomerOps/src/CustomerPortal.Domain/` (from `dotnet new classlib`, emptied of template cruft)
- Create: `CustomerOps/src/CustomerPortal.Infrastructure/` (from `dotnet new classlib`, emptied of template cruft)
- Create: `CustomerOps/tests/CustomerPortal.ApiTests/` (from `dotnet new xunit`)
- Modify: `CustomerOps/src/CustomerPortal.Api/Program.cs`
- Test: `CustomerOps/tests/CustomerPortal.ApiTests/HealthEndpointTests.cs`

**Interfaces:**
- Produces: `GET /health` → `200 OK`, body `Healthy` (plain text). Task 2 and Task 3 depend on this exact contract.

- [ ] **Step 1: Scaffold solution and projects**

Run from `CustomerOps/`:

```bash
dotnet new sln -n CustomerPortal
dotnet new webapi -n CustomerPortal.Api -o src/CustomerPortal.Api -controllers
dotnet new classlib -n CustomerPortal.Application -o src/CustomerPortal.Application
dotnet new classlib -n CustomerPortal.Domain -o src/CustomerPortal.Domain
dotnet new classlib -n CustomerPortal.Infrastructure -o src/CustomerPortal.Infrastructure
dotnet new xunit -n CustomerPortal.ApiTests -o tests/CustomerPortal.ApiTests
```

- [ ] **Step 2: Add projects to the solution and wire references**

```bash
dotnet sln add src/CustomerPortal.Api/CustomerPortal.Api.csproj src/CustomerPortal.Application/CustomerPortal.Application.csproj src/CustomerPortal.Domain/CustomerPortal.Domain.csproj src/CustomerPortal.Infrastructure/CustomerPortal.Infrastructure.csproj tests/CustomerPortal.ApiTests/CustomerPortal.ApiTests.csproj

dotnet add src/CustomerPortal.Application reference src/CustomerPortal.Domain
dotnet add src/CustomerPortal.Infrastructure reference src/CustomerPortal.Application src/CustomerPortal.Domain
dotnet add src/CustomerPortal.Api reference src/CustomerPortal.Application src/CustomerPortal.Infrastructure
dotnet add tests/CustomerPortal.ApiTests reference src/CustomerPortal.Api
dotnet add tests/CustomerPortal.ApiTests package Microsoft.AspNetCore.Mvc.Testing
```

- [ ] **Step 3: Remove template cruft**

Delete these generated files (no content belongs in them yet — Domain/Application/Infrastructure logic starts in the next plan):

```bash
rm src/CustomerPortal.Api/WeatherForecast.cs
rm src/CustomerPortal.Api/Controllers/WeatherForecastController.cs
rm src/CustomerPortal.Application/Class1.cs
rm src/CustomerPortal.Domain/Class1.cs
rm src/CustomerPortal.Infrastructure/Class1.cs
rm tests/CustomerPortal.ApiTests/UnitTest1.cs
```

- [ ] **Step 4: Replace `Program.cs` with a testable baseline (no health check yet)**

Replace the full contents of `src/CustomerPortal.Api/Program.cs` with:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
```

The trailing `public partial class Program { }` is required so `WebApplicationFactory<Program>` in the test project can see the entry point — top-level statement programs don't expose one otherwise.

- [ ] **Step 5: Write the failing health check test**

Create `tests/CustomerPortal.ApiTests/HealthEndpointTests.cs`:

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CustomerPortal.ApiTests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
                          .CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsOkWithHealthyBody()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", body);
    }
}
```

- [ ] **Step 6: Run the test and verify it fails**

Run: `dotnet test tests/CustomerPortal.ApiTests`
Expected: FAIL — `GetHealth_ReturnsOkWithHealthyBody` fails because `/health` returns `404 Not Found` (no such route yet).

- [ ] **Step 7: Add the health check endpoint**

In `src/CustomerPortal.Api/Program.cs`, add `builder.Services.AddHealthChecks();` directly below `builder.Services.AddOpenApi();`, and add `app.MapHealthChecks("/health");` directly below `app.MapControllers();`. Full resulting file:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
```

- [ ] **Step 8: Run the test and verify it passes**

Run: `dotnet test tests/CustomerPortal.ApiTests`
Expected: PASS

- [ ] **Step 9: Note the assigned local port for Task 2**

Open `src/CustomerPortal.Api/Properties/launchSettings.json` and note the `http` profile's `applicationUrl` port (e.g. `http://localhost:5186`) — Task 2 needs it.

- [ ] **Step 10: Commit**

```bash
git add CustomerPortal.sln src/ tests/
git commit -m "feat: scaffold CustomerPortal solution with health check endpoint"
```

---

### Task 2: React Skeleton with CustomerOperationsPage

**Files:**
- Create: `CustomerOps/frontend/customer-portal/` (from `npm create vite@latest`)
- Create: `CustomerOps/frontend/customer-portal/.env.development`
- Create: `CustomerOps/frontend/customer-portal/src/CustomerOperationsPage.tsx`
- Create: `CustomerOps/frontend/customer-portal/src/CustomerOperationsPage.test.tsx`
- Create: `CustomerOps/frontend/customer-portal/src/setupTests.ts`
- Modify: `CustomerOps/frontend/customer-portal/src/main.tsx`
- Modify: `CustomerOps/frontend/customer-portal/src/App.tsx`
- Modify: `CustomerOps/frontend/customer-portal/vite.config.ts`
- Modify: `CustomerOps/frontend/customer-portal/package.json`

**Interfaces:**
- Consumes: `GET /health` from Task 1 → `200 OK`, body `Healthy` (plain text)
- Produces: `CustomerOperationsPage` component, rendered by `App.tsx`, showing `API Status: <status>`. Task 3 relies on this text existing for manual verification.

- [ ] **Step 1: Scaffold the Vite React-TS project**

Run from `CustomerOps/`:

```bash
mkdir frontend
cd frontend
npm create vite@latest customer-portal -- --template react-ts
cd customer-portal
npm install
```

- [ ] **Step 2: Install runtime and test dependencies**

Run from `CustomerOps/frontend/customer-portal/`:

```bash
npm install @tanstack/react-query
npm install -D vitest @testing-library/react @testing-library/jest-dom @testing-library/user-event jsdom
```

- [ ] **Step 3: Configure Vitest**

Replace the full contents of `vite.config.ts` with:

```ts
/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/setupTests.ts'],
    globals: true,
  },
})
```

Create `src/setupTests.ts`:

```ts
import '@testing-library/jest-dom/vitest'
```

In `package.json`, add a `test` script inside `"scripts"`:

```json
"test": "vitest run"
```

- [ ] **Step 4: Record the API base URL**

Create `.env.development` (values from Task 1 Step 9 — replace the port with what your `launchSettings.json` actually assigned):

```
VITE_API_BASE_URL=http://localhost:5186
```

- [ ] **Step 5: Write the failing component test**

Create `src/CustomerOperationsPage.test.tsx`:

```tsx
import { render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { CustomerOperationsPage } from './CustomerOperationsPage'

function renderWithClient(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
  )
}

describe('CustomerOperationsPage', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        text: () => Promise.resolve('Healthy'),
      })
    )
  })

  it('shows the API health status once loaded', async () => {
    renderWithClient(<CustomerOperationsPage />)

    expect(screen.getByText('API Status: Checking...')).toBeInTheDocument()

    await waitFor(() =>
      expect(screen.getByText('API Status: Healthy')).toBeInTheDocument()
    )
  })
})
```

- [ ] **Step 6: Run the test and verify it fails**

Run: `npm test` (from `CustomerOps/frontend/customer-portal/`)
Expected: FAIL — `CustomerOperationsPage` module does not exist yet.

- [ ] **Step 7: Implement CustomerOperationsPage**

Create `src/CustomerOperationsPage.tsx`:

```tsx
import { useQuery } from '@tanstack/react-query'

async function fetchHealth(): Promise<string> {
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/health`)
  if (!response.ok) {
    throw new Error(`Health check failed: ${response.status}`)
  }
  return response.text()
}

export function CustomerOperationsPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['health'],
    queryFn: fetchHealth,
  })

  const status = isLoading ? 'Checking...' : isError ? 'Unreachable' : data

  return (
    <main>
      <h1>Customer Operations</h1>
      <p>{`API Status: ${status}`}</p>
    </main>
  )
}
```

- [ ] **Step 8: Wire the page into the app**

Replace the full contents of `src/App.tsx` with:

```tsx
import { CustomerOperationsPage } from './CustomerOperationsPage'

function App() {
  return <CustomerOperationsPage />
}

export default App
```

Replace the full contents of `src/main.tsx` with:

```tsx
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import './index.css'
import App from './App.tsx'

const queryClient = new QueryClient()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>
)
```

- [ ] **Step 9: Run the test and verify it passes**

Run: `npm test`
Expected: PASS

- [ ] **Step 10: Commit**

```bash
git add frontend/
git commit -m "feat: scaffold React CustomerOperationsPage with health status query"
```

---

### Task 3: CORS + Environment Config, End-to-End Verification

**Files:**
- Create: `CustomerOps/src/CustomerPortal.Api/appsettings.Development.json` (full replace)
- Modify: `CustomerOps/src/CustomerPortal.Api/Program.cs`
- Modify: `CustomerOps/tests/CustomerPortal.ApiTests/HealthEndpointTests.cs`

**Interfaces:**
- Consumes: Task 1's `/health` endpoint, Task 2's dev-server origin (`http://localhost:5173`, Vite's default)
- Produces: CORS policy named `LocalDevelopment` allowing the configured origins — later plans (Redis, Service Bus) reuse this same config pattern for their own settings sections.

- [ ] **Step 1: Write the failing CORS test**

Replace the full contents of `tests/CustomerPortal.ApiTests/HealthEndpointTests.cs` with:

```csharp
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CustomerPortal.ApiTests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
                          .CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsOkWithHealthyBody()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task GetHealth_WithAllowedOrigin_IncludesCorsHeader()
    {
        _client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");

        var response = await _client.GetAsync("/health");

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Equal("http://localhost:5173", values!.Single());
    }
}
```

- [ ] **Step 2: Run the tests and verify the new one fails**

Run: `dotnet test tests/CustomerPortal.ApiTests`
Expected: `GetHealth_ReturnsOkWithHealthyBody` PASSES, `GetHealth_WithAllowedOrigin_IncludesCorsHeader` FAILS (no `Access-Control-Allow-Origin` header yet).

- [ ] **Step 3: Add the allowed-origins config**

Create `src/CustomerPortal.Api/appsettings.Development.json` (full replace of the template-generated file):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173"]
  }
}
```

- [ ] **Step 4: Wire the CORS policy in Program.cs**

Full resulting `src/CustomerPortal.Api/Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDevelopment", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("LocalDevelopment");
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
```

- [ ] **Step 5: Run the tests and verify both pass**

Run: `dotnet test tests/CustomerPortal.ApiTests`
Expected: PASS (both tests)

- [ ] **Step 6: Commit**

```bash
git add src/CustomerPortal.Api/appsettings.Development.json src/CustomerPortal.Api/Program.cs tests/CustomerPortal.ApiTests/HealthEndpointTests.cs
git commit -m "feat: enable CORS for local React dev server"
```

- [ ] **Step 7: Manual end-to-end verification**

Terminal 1, from `CustomerOps/`:

```bash
dotnet run --project src/CustomerPortal.Api
```

Terminal 2, from `CustomerOps/frontend/customer-portal/`:

```bash
npm run dev
```

Open the URL Vite prints (default `http://localhost:5173`).

**Expected result:** the page shows "Customer Operations" and, within a second, "API Status: Healthy". If it stays on "Checking..." or shows "Unreachable", check: the API's http port matches `VITE_API_BASE_URL` in `.env.development`, both processes are running, and the browser console/network tab for CORS or connection errors.

---

## Self-Review Notes

- **Spec coverage:** architecture.md §4 (local dev, no Azure resources) — satisfied, everything runs locally. §5 backend structure — satisfied, all four projects created with correct reference graph. §6 frontend structure — satisfied, one page, TanStack Query, no Redux. §7 tech choices — satisfied (ASP.NET Core, React/TS/Vite, no DB/Redis/Service Bus touched yet, correctly deferred). §8 Phase 1 — this plan covers the "foundation" half; Customer CRUD + SQL is the next plan, called out explicitly above so it isn't mistaken for scope creep.
- **Placeholder scan:** no TBD/TODO markers; every step has literal file contents or exact commands.
- **Type consistency:** `CustomerOperationsPage` export name matches between `CustomerOperationsPage.tsx`, `CustomerOperationsPage.test.tsx`, and `App.tsx`; `Program` partial class name matches between `Program.cs` and both `WebApplicationFactory<Program>` usages; CORS policy name `LocalDevelopment` matches between registration and `UseCors` call.
