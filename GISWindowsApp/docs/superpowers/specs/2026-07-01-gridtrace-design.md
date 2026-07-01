# GridTrace — Design Spec

**Date:** 2026-07-01
**Purpose:** 5-hour capstone project for interview/resume prep targeting an Oncor-style GIS/utility engineering role. Complements the existing [GISApplication](../../../GISApplication) web capstone (UCE-ORA: spatial buffer risk analysis on a web map) with a desktop application that emulates a different, equally recognizable Hexagon Intergraph (G/Technology) capability: **connectivity-based outage impact tracing** over an electric distribution network model.

No Hexagon Intergraph API access is available, so the app does not integrate with Intergraph directly — it recreates the *concept* (network model + connectivity trace) using Oracle and a hand-built WPF schematic renderer.

---

## 1. Concept

GridTrace is a WPF desktop app managing a simplified electric distribution network (substation → feeders → poles → transformers → customer meters) as a parent/child tree in Oracle. The user selects any device and simulates an outage; the app traces every downstream device and customer meter affected using Oracle's native hierarchical query (`START WITH ... CONNECT BY PRIOR`), highlights them on a schematic diagram, and reports impact counts — the same mental model as an Outage Management System (OMS) trace, which is exactly the kind of feature Oncor's network model tooling (G/Technology) is built around.

This is deliberately narrower in scope than a real Intergraph system (no real map, no full network editor) — it is a focused demo of the trace concept, buildable in 5 hours.

---

## 2. Architecture & Tech Stack

- **Single WPF (.NET 8) desktop project**, MVVM pattern (Model / ViewModel / View). No separate API/service layer — the app talks directly to Oracle.
- **Database:** reuses the already-running `oracle-spatial` Docker container (port 1521, `XEPDB1`) — one new table, no new container.
- **Data access:** `Oracle.ManagedDataAccess.Core` (ODP.NET managed driver) via raw ADO.NET (`OracleConnection` / `OracleCommand`). Deliberately **no EF Core** — the trace logic is expressed as an Oracle-native hierarchical SQL query, which is more distinctive to show hand-written than hidden behind an ORM (the existing GISApplication project already demonstrates EF Core + Oracle).
- **Rendering:** custom-drawn schematic diagram on a WPF `Canvas` — devices as shapes (rectangle = substation, circle = pole/transformer, small square = customer meter) positioned via pre-seeded coordinates; connections drawn as `Line` elements between parent and child.
- No external mapping library, no map tiles, no GeoJSON — this app is schematic, not geographic (distinguishing it from the web project's Leaflet map).

---

## 3. Data Model

One self-referencing table models the whole tree — a real distribution network is radial, power flows one direction from substation down to meters, so a parent/child column is sufficient (no separate edges table needed):

```sql
CREATE TABLE network_devices (
  id           NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  name         VARCHAR2(100) NOT NULL,
  device_type  VARCHAR2(20) NOT NULL,   -- SUBSTATION, FEEDER, POLE, TRANSFORMER, METER
  parent_id    NUMBER REFERENCES network_devices(id),
  pos_x        NUMBER,                  -- pre-computed schematic layout coordinates
  pos_y        NUMBER,
  status       VARCHAR2(10) DEFAULT 'NORMAL'  -- NORMAL or OUTAGE
);
```

**Seed data:** 1 substation → 2 feeders → ~6 poles → ~6 transformers → ~10 customer meters (~25 rows, 4 levels deep). `pos_x`/`pos_y` are hand-picked simple grid coordinates baked directly into the seed INSERTs — the app never computes layout, it just reads and draws at the given coordinates.

---

## 4. UI & Interaction Flow

**Layout:** `MainWindow.xaml` — left sidebar listing all devices (grouped/indented by type and hierarchy), main area is a `Canvas` rendering the schematic diagram, bottom status bar for trace results.

**Flow:**
1. On startup, `MainViewModel` loads all devices via `DeviceRepository.GetAllDevicesAsync()` into an `ObservableCollection<DeviceViewModel>`.
2. Canvas draws parent→child lines first, then device shapes on top at their seeded `pos_x`/`pos_y`, color-coded by `device_type`; `status = NORMAL` uses the default per-type color, `status = OUTAGE` renders red.
3. User selects a device via the sidebar list (click-to-select directly on canvas shapes is an optional stretch goal, not required for the baseline demo).
4. **"Simulate Outage"** button (enabled only when a device is selected, via `ICommand.CanExecute`) runs:
   ```sql
   SELECT id FROM network_devices
   START WITH id = :selectedId
   CONNECT BY PRIOR id = parent_id
   ```
   returning the selected device plus every descendant in one query.
5. The app updates those rows' `status` to `OUTAGE` — both in Oracle (`UPDATE network_devices SET status = 'OUTAGE' WHERE id IN (...)`) and in the in-memory `ObservableCollection` — canvas redraws affected shapes red, and the status bar reports impact, e.g. *"14 devices affected, 6 customer meters without power."*
6. **"Restore Power"** button resets all statuses to `NORMAL` (DB + in-memory) and redraws the canvas.

---

## 5. Error Handling

- Oracle connection failures (`OracleConnection.Open()`) are caught and surfaced via a friendly `MessageBox`: *"Cannot connect to Oracle at localhost:1521/XEPDB1 — is the container running?"*
- "Simulate Outage" is disabled until a device is selected (no null-selection trace attempts).
- A leaf device (no children) is still a valid trace target — it just highlights itself with an impact count of 1.

---

## 6. Testing

Given the 5-hour budget, testing is intentionally minimal and targeted at the one piece of logic worth verifying — the trace query, not UI wiring:

- One xUnit test project (`GridTrace.Tests`) with an integration test that runs the `CONNECT BY PRIOR` query against the real seeded Oracle data and asserts a known device (e.g., a specific feeder) returns the expected set of descendant IDs.

---

## 7. Out of Scope (for this 5-hour capstone)

- Real Hexagon Intergraph API integration (no access available).
- Geographic/real-world coordinates or map tiles (schematic only).
- Click-to-select directly on canvas shapes (sidebar list selection is the required baseline; canvas click-select is optional if time remains).
- Network editing (add/remove/rewire devices) — the network topology is fixed seed data; only `status` changes at runtime.
- Authentication or multi-user concerns.
