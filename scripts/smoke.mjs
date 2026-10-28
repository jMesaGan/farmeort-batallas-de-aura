// Prueba HTTP contra una demo aislada, recién restablecida. Node >=20.
// API_URL y DEMO_ORGANIZER_PASSWORD son obligatorias. Crea un jugador de prueba.
import assert from "node:assert/strict";
const base = process.env.API_URL;
const organizerPassword = process.env.DEMO_ORGANIZER_PASSWORD;
if (!base || !organizerPassword)
  throw new Error("Exportar API_URL y DEMO_ORGANIZER_PASSWORD.");
async function call(path, { body, token, expected = 200 } = {}) {
  const response = await fetch(base + path, {
    method: body === undefined ? "GET" : "POST",
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: "Bearer " + token } : {}),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  assert.equal(response.status, expected, `${path}: ${response.status}`);
  return response.status === 204 ? null : response.json();
}
await call("/health");
await call("/api/tournaments/demo", { expected: 401 });
const org = await call("/api/auth/login", {
  body: { email: "organizador@farmeort.example", password: organizerPassword },
});
const tournament = await call("/api/tournaments/demo", { token: org.token });
assert.equal(tournament.players.length, 1, "Restablecer demo antes del smoke");
await call(`/api/tournaments/${tournament.id}/round`, {
  token: org.token,
  body: {},
  expected: 409,
});
const nickname = "Smoke" + Date.now();
const email = nickname.toLowerCase() + "@example.com";
const password = "JugadorDeDemo123!";
const fields = {
  name: "Demo",
  surname: "Prueba",
  email,
  nickname,
  password,
  birthDate: "2000-01-01",
};
await call("/api/auth/register", {
  body: { ...fields, password: "weak" },
  expected: 400,
});
const user = await call("/api/auth/register", {
  body: { ...fields, role: "Organizador", elo: 9999 },
});
assert.equal(user.role, "Jugador");
assert.equal(user.elo, 1200);
assert.equal(user.passwordHash, undefined);
await call("/api/auth/register", { body: fields, expected: 409 });
await call("/api/auth/login", {
  body: { email, password: "wrong" },
  expected: 401,
});
const player = await call("/api/auth/login", { body: { email, password } });
await call(`/api/tournaments/${tournament.id}/round`, {
  token: player.token,
  body: {},
  expected: 403,
});
await call(`/api/tournaments/${tournament.id}/registrations`, {
  token: player.token,
  body: {},
});
await call(`/api/tournaments/${tournament.id}/registrations`, {
  token: player.token,
  body: {},
});
await call("/api/auth/logout", {
  token: player.token,
  body: {},
  expected: 204,
});
await call("/api/auth/me", { token: player.token, expected: 401 });
const battles = await Promise.all(
  [0, 1, 2].map(() =>
    call(`/api/tournaments/${tournament.id}/round`, {
      token: org.token,
      body: {},
    }),
  ),
);
assert(battles.every((b) => b.id === battles[0].id));
assert.equal(battles[0].status, "Pendiente");
assert.notEqual(battles[0].playerOne.id, battles[0].playerTwo.id);
const detail = await call("/api/battles/" + battles[0].id, {
  token: org.token,
});
assert.deepEqual(detail, battles[0]);
console.log("Recorrido HTTP correcto. Batalla persistida:", detail.id);
console.log(
  "Reiniciar backend y consultar el mismo detalle para verificar persistencia entre procesos.",
);
