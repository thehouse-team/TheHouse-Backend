// NV-09 T16 (1.2.0-u2): prueba de performance del backend con k6.
//
// Corre solo contra el entorno efímero de cd-extended.yml (nunca contra
// producción), con los límites de peticiones de producción. Cada iteración
// declara una IP del rango de pruebas de rendimiento 198.18.0.0/15 (RFC 2544)
// en X-Forwarded-For: la API solo acepta ese encabezado del proxy local, igual
// que con el ingress de Azure, así que la prueba representa muchos estudiantes
// sin desactivar los límites por IP.
//
// El reporte no incluye correos, contraseñas ni tokens.
import http from "k6/http";
import { check, fail } from "k6";
import exec from "k6/execution";
import { Counter } from "k6/metrics";

const BASE = __ENV.BASE_URL || "http://127.0.0.1:8080";
const OUT = __ENV.OUT_DIR || ".";
const ACCOUNTS = 200;
const WARMUP = "30s";
const MEASURE = "3m";

// Presupuesto p95 propuesto (ms) en el runner; informativo (spec §6.1).
const BUDGET = { catalogo: 200, busqueda: 500, smart_match: 800, sesion: 1000 };
const RATES = { catalogo: 10, busqueda: 8, smart_match: 4, sesion: 2 };

const rateLimited = new Counter("rate_limited");
const unexpected = new Counter("unexpected_status");

function scenario(name, rate) {
  return {
    executor: "ramping-arrival-rate",
    exec: name,
    startRate: 1,
    timeUnit: "1s",
    preAllocatedVUs: rate * 4,
    maxVUs: rate * 15,
    stages: [
      { target: rate, duration: WARMUP },
      { target: rate, duration: MEASURE },
    ],
  };
}

const thresholds = {
  // El job falla solo si los errores superan el 5 %; la latencia es informativa.
  http_req_failed: ["rate<0.05"],
};
for (const name of Object.keys(RATES)) {
  // Umbrales que siempre pasan: hacen que el resumen incluya cada escenario.
  thresholds[`http_req_duration{scenario:${name}}`] = ["max>=0"];
  thresholds[`http_req_failed{scenario:${name}}`] = ["rate>=0"];
  thresholds[`http_reqs{scenario:${name}}`] = ["count>=0"];
  thresholds[`rate_limited{scenario:${name}}`] = ["count>=0"];
}

export const options = {
  scenarios: Object.fromEntries(Object.entries(RATES).map(([name, rate]) => [name, scenario(name, rate)])),
  thresholds,
  setupTimeout: "180s",
  summaryTrendStats: ["avg", "min", "med", "p(95)", "p(99)", "max"],
  discardResponseBodies: false,
  noConnectionReuse: false,
};

function randomInt(max) {
  return Math.floor(Math.random() * max);
}

function clientIp() {
  return `198.${18 + randomInt(2)}.${randomInt(256)}.${1 + randomInt(254)}`;
}

function headers(token) {
  const h = {
    "Content-Type": "application/json",
    Accept: "application/json",
    "X-Forwarded-Proto": "https",
    "X-Forwarded-For": clientIp(),
  };
  if (token) {
    h.Authorization = `Bearer ${token}`;
  }
  return h;
}

function randomText(length) {
  const chars = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  let text = "";
  for (let i = 0; i < length; i++) {
    text += chars[randomInt(chars.length)];
  }
  return text;
}

function record(response, expected, name) {
  if (response.status === 429) {
    rateLimited.add(1);
  }
  const ok = check(response, { [`${name} ${expected}`]: (r) => r.status === expected });
  if (!ok) {
    unexpected.add(1, { endpoint: name, status: String(response.status) });
  }
  return ok;
}

function json(response) {
  try {
    return response.json();
  } catch (_) {
    return null;
  }
}

const PREFERENCES = {
  budget: { min: 1000, max: 10000 },
  maxDistanceKm: 30,
  essentialServices: ["wifi", "furnished"],
  livingWith: "any",
  placeType: "room",
};

export function setup() {
  const ready = http.get(`${BASE}/health/ready`);
  if (ready.status !== 200) {
    fail(`/health/ready respondió ${ready.status}`);
  }

  const universities = json(http.get(`${BASE}/api/v1/universities/search?q=uttt`, { headers: headers() }));
  const uttt = ((universities && universities.items) || []).find((u) => u.code === "UTTT");
  if (!uttt) {
    fail("No se encontró la UTTT en el seed de universidades.");
  }

  const search = json(http.get(`${BASE}/api/v1/rooms/search?universityId=${uttt.id}&maxDistanceKm=30&pageSize=50`, { headers: headers() }));
  const rooms = ((search && search.items) || []).map((room) => room.id);
  if (rooms.length === 0) {
    fail("La búsqueda no devolvió cuartos: falta el seed de demostración.");
  }

  const accounts = [];
  for (let start = 0; start < ACCOUNTS; start += 20) {
    const batch = [];
    for (let i = start; i < Math.min(start + 20, ACCOUNTS); i++) {
      batch.push({
        email: `perf-${randomText(10).toLowerCase()}-${i}@example.com`,
        password: `Perf-${randomText(18)}-9a!`,
      });
    }
    const responses = http.batch(batch.map((account) => ({
      method: "POST",
      url: `${BASE}/api/v1/auth/register`,
      body: JSON.stringify(account),
      params: { headers: headers(), tags: { name: "setup_register" } },
    })));
    responses.forEach((response, index) => {
      if (response.status === 201) {
        accounts.push(batch[index]);
      }
    });
  }
  if (accounts.length < ACCOUNTS * 0.9) {
    fail(`Solo se registraron ${accounts.length} de ${ACCOUNTS} cuentas.`);
  }

  return { universityId: uttt.id, rooms, accounts };
}

export function catalogo() {
  record(http.get(`${BASE}/api/v1/universities?page=1&pageSize=10`, { headers: headers(), tags: { name: "GET /universities" } }), 200, "GET /universities");
  record(http.get(`${BASE}/api/v1/properties?page=1&pageSize=12`, { headers: headers(), tags: { name: "GET /properties" } }), 200, "GET /properties");
}

export function busqueda(data) {
  const url = `${BASE}/api/v1/rooms/search?universityId=${data.universityId}&maxDistanceKm=30&pageSize=10`;
  record(http.get(url, { headers: headers(), tags: { name: "GET /rooms/search" } }), 200, "GET /rooms/search");
}

export function smart_match(data) {
  const body = JSON.stringify({ universityId: data.universityId, preferences: PREFERENCES });
  record(
    http.post(`${BASE}/api/v1/smart-match/recommendations/query?pageSize=10`, body, { headers: headers(), tags: { name: "POST /smart-match/recommendations/query" } }),
    200,
    "POST /smart-match/recommendations/query");
}

export function sesion(data) {
  const index = exec.scenario.iterationInTest;
  const account = data.accounts[index % data.accounts.length];
  const login = http.post(`${BASE}/api/v1/auth/login`, JSON.stringify(account), { headers: headers(), tags: { name: "POST /auth/login" } });
  if (!record(login, 200, "POST /auth/login")) {
    return;
  }
  const token = (json(login) || {}).accessToken;
  record(http.get(`${BASE}/api/v1/users/me`, { headers: headers(token), tags: { name: "GET /users/me" } }), 200, "GET /users/me");
  const room = data.rooms[index % data.rooms.length];
  record(http.get(`${BASE}/api/v1/rooms/${room}`, { headers: headers(token), tags: { name: "GET /rooms/{id}" } }), 200, "GET /rooms/{id}");
}

function ms(value) {
  return value === undefined ? "—" : `${value.toFixed(1)}`;
}

export function handleSummary(data) {
  const m = data.metrics;
  const rows = [];
  const summary = { generatedAt: new Date().toISOString(), budgetsP95Ms: BUDGET, ratesPerSecond: RATES, scenarios: {} };
  for (const name of Object.keys(RATES)) {
    const d = (m[`http_req_duration{scenario:${name}}`] || {}).values || {};
    const f = (m[`http_req_failed{scenario:${name}}`] || {}).values || {};
    const c = (m[`http_reqs{scenario:${name}}`] || {}).values || {};
    const l = (m[`rate_limited{scenario:${name}}`] || {}).values || {};
    const p95 = d["p(95)"];
    const within = p95 === undefined ? "—" : p95 <= BUDGET[name] ? "✅ dentro" : "⚠️ fuera";
    rows.push(`| \`${name}\` | ${RATES[name]}/s | ${c.count || 0} | ${ms(d.med)} | ${ms(p95)} | ${ms(d["p(99)"])} | ${ms(d.max)} | ${((f.rate || 0) * 100).toFixed(2)} % | ${l.count || 0} | < ${BUDGET[name]} ms ${within} |`);
    summary.scenarios[name] = {
      requests: c.count || 0,
      p50Ms: d.med, p95Ms: p95, p99Ms: d["p(99)"], maxMs: d.max,
      errorRate: f.rate || 0,
      rateLimited: l.count || 0,
      withinBudget: p95 === undefined ? null : p95 <= BUDGET[name],
    };
  }
  const failed = (m.http_req_failed || {}).values || {};
  const dropped = ((m.dropped_iterations || {}).values || {}).count || 0;
  const total = ((m.http_reqs || {}).values || {}).count || 0;
  summary.totalRequests = total;
  summary.errorRate = failed.rate || 0;
  summary.droppedIterations = dropped;

  const report = [
    "## NV-09 T16 — Performance con k6 (entorno efímero)",
    "",
    `Calentamiento ${WARMUP} y medición ${MEASURE} por escenario, con los límites de peticiones de producción. Latencias en ms. **Runner compartido de GitHub: sirve para comparar versiones, no es la capacidad de Azure.**`,
    "",
    "| Escenario | Tasa | Peticiones | p50 | p95 | p99 | Máx. | Errores | 429 | Presupuesto p95 |",
    "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |",
    ...rows,
    "",
    `Total: ${total} peticiones; errores ${((failed.rate || 0) * 100).toFixed(2)} % (el job falla con más de 5 %); iteraciones descartadas: ${dropped}.`,
    "",
  ].join("\n");

  return {
    stdout: report + "\n",
    [`${OUT}/perf-report.md`]: report,
    [`${OUT}/perf-summary.json`]: JSON.stringify(summary, null, 2),
  };
}
