# Phase 5 — Integration & End-to-End Smoke Test

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring all four services up together via Docker Compose and verify the full happy path end-to-end: sensor stream flows to the Angular dashboard, a chat question returns a streamed response, an ML prediction job completes, and a live alert banner appears.

**Architecture:** All services run as Docker containers behind Docker Compose. No code changes in this phase — pure wiring verification. If anything breaks, a per-service health check pinpoints which service is at fault before diving into logs.

**Tech Stack:** Docker 27+ · Docker Compose · RabbitMQ Management UI · curl

**Depends on:** Phases 0, 1, 2, 3, and 4 all complete and their Docker images buildable.

---

### Task 15: Docker Compose smoke test (end-to-end)

**Files:** No new files. This task verifies the system as a whole.

- [ ] **Step 1: Train the Python model (if `ml/screen_out_rf.pkl` does not exist)**

```bash
cd fractureguard-predictor
source .venv/bin/activate        # Windows: .venv\Scripts\activate
python scripts/train_model.py
cd ..
```

Expected: `Model saved to ml/screen_out_rf.pkl`

- [ ] **Step 2: Build all Docker images**

```bash
docker compose build
```

Expected: All 4 service images build without errors. Build output ends with `[+] Building ... FINISHED`.

- [ ] **Step 3: Start the stack**

```bash
docker compose up -d
```

Wait 20 seconds for RabbitMQ and Cosmos DB emulator to initialise before proceeding.

- [ ] **Step 4: Verify all containers are running**

```bash
docker compose ps
```

Expected: All services show status `running` or `Up`. No services in `Exit` state.

- [ ] **Step 5: Health-check each service**

```bash
curl -s http://localhost:8001/health   # Python predictor
curl -s http://localhost:3001/health   # Node.js notifier
```

Expected responses:
```json
{"status":"ok"}
{"status":"ok"}
```

If either fails, run `docker compose logs <service-name>` to inspect the error before continuing.

- [ ] **Step 6: Verify RabbitMQ is healthy**

Open `http://localhost:15672` in your browser (user: `guest`, pass: `guest`).

Expected: RabbitMQ management UI loads. Under **Queues** tab, `analysis-requests` and `analysis-results` queues are present (they are declared on first publish/consume).

- [ ] **Step 7: Verify sensor stream reaches Angular**

Open `http://localhost:4200` in your browser. Open DevTools → Network → WS tab.

Expected: A WebSocket connection to `ws://localhost:3001` is active. `sensor:reading` frames arrive every ~500 ms with `pressure_psi`, `flow_rate_bpm`, `vibration_g`, and `temperature_c` fields. The KPI cards on the dashboard update in real time.

- [ ] **Step 8: Send a fast-path chat message**

In the Angular dashboard, open the chat panel and send:

```
What is the current pressure reading?
```

Expected:
1. The AI Analyst response starts streaming within 2 seconds.
2. The response references the live sensor value (e.g., "Current pressure is 712 PSI...").
3. The streaming cursor `▌` disappears when the response completes.

- [ ] **Step 9: Trigger a heavy-path ML prediction**

Send:

```
What is the risk of a screen-out in the next hour?
```

Expected sequence:
1. Immediate streamed acknowledgement: "Screen-out simulation submitted. I'll push the results to your dashboard when the analysis completes."
2. RabbitMQ management UI shows 1 message processed in `analysis-requests`.
3. Within 10–15 seconds: a live alert banner appears at the top of the monitoring panel containing the risk percentage and recommended action.
4. RabbitMQ management UI shows 1 message processed in `analysis-results`.

- [ ] **Step 10: Verify Cosmos DB chat history is persisted**

```bash
curl -s "http://localhost:5000/api/chat/<your-session-id>" \
  -H "Authorization: Bearer dev-engineer-token"
```

Replace `<your-session-id>` with the UUID logged in the .NET API container output.

Expected: JSON array of chat messages including both user and assistant turns.

- [ ] **Step 11: Tear down**

```bash
docker compose down
```

Expected: All containers stopped and removed cleanly.

- [ ] **Step 12: Final commit**

```bash
git add .
git commit -m "chore: end-to-end integration smoke test verified"
```

---

## What a Successful Integration Confirms

| Check | Verified by |
|---|---|
| Sensor data flows Node.js → Angular | Step 7: WebSocket frames in DevTools |
| Fast-path LLM response | Step 8: streamed chat response |
| RabbitMQ publish (analysis-requests) | Step 9 + RabbitMQ UI |
| Python ML model processes job | Step 9: alert appears within 15 s |
| RabbitMQ consume (analysis-results) | Step 9 + RabbitMQ UI |
| Node.js webhook delivers alert to Angular | Step 9: live alert banner |
| Cosmos DB persists chat history | Step 10: GET history returns messages |

---

*Phase 5 complete — FractureGuard AI is fully operational locally.*  
*Next step: deploy to Azure Container Apps following the infrastructure section in [design.md](design.md).*
