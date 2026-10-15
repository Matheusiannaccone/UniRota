"use strict";

/**
 * UniRota - Firebase Emulator com dataset padrão
 *
 * Uso (na raiz do repositório):
 *   node start-emulators-seeded.js
 *
 * O script:
 *   1) inicia `firebase emulators:start --project unirota-f0a63`;
 *   2) aguarda Auth (9099) e Firestore (8080);
 *   3) limpa Auth + collections users/weeklyRoutes/rideRequests;
 *   4) recria o dataset padrão abaixo;
 *   5) mantém os emuladores rodando até Ctrl+C.
 *
 * Também pode ser usado com emuladores já iniciados:
 *   node start-emulators-seeded.js --seed-only
 *
 * IMPORTANTE:
 * - As credenciais abaixo são SOMENTE para o Firebase Emulator.
 * - Os Place IDs `TEST_PLACE_*` são identificadores determinísticos de teste.
 *   Eles não devem ser enviados para Google Places/Routes reais. O dataset foi
 *   preparado para o ambiente local e para um futuro serviço fake de mapas.
 * - O script sempre restaura o baseline: alterações feitas durante um teste
 *   serão apagadas na próxima execução.
 */

const { spawn } = require("node:child_process");
const net = require("node:net");
const path = require("node:path");
const { createRequire } = require("node:module");

const PROJECT_ID = "unirota-f0a63";
const AUTH_HOST = "127.0.0.1";
const AUTH_PORT = 9099;
const FIRESTORE_HOST = "127.0.0.1";
const FIRESTORE_PORT = 8080;
const FUNCTIONS_PORT = 5001;
const EMULATOR_UI_PORT = 4000;
const DEFAULT_PASSWORD = "090909";
const CREATED_AT = new Date("2026-10-14T12:00:00.000Z");

// Defina as variáveis antes de carregar firebase-admin.
process.env.GCLOUD_PROJECT = PROJECT_ID;
process.env.FIREBASE_CONFIG = JSON.stringify({ projectId: PROJECT_ID });
process.env.FIREBASE_AUTH_EMULATOR_HOST = `${AUTH_HOST}:${AUTH_PORT}`;
process.env.FIRESTORE_EMULATOR_HOST = `${FIRESTORE_HOST}:${FIRESTORE_PORT}`;

let initializeApp;
let getApps;
let getAuth;
let getFirestore;
let Timestamp;
try {
  // Resolve os submódulos do firebase-admin a partir do package.json de
  // `functions`, respeitando o mecanismo de exports do pacote no Node 22.
  const functionsRequire = createRequire(
    path.join(process.cwd(), "functions", "package.json"));

  ({ initializeApp, getApps } = functionsRequire("firebase-admin/app"));
  ({ getAuth } = functionsRequire("firebase-admin/auth"));
  ({ getFirestore, Timestamp } = functionsRequire("firebase-admin/firestore"));
} catch (error) {
  console.error("\nNão foi possível carregar firebase-admin.");
  console.error("Confirme que `npm install` foi executado dentro de `functions`.\n");
  console.error(error);
  process.exit(1);
}

const users = [
  {
    uid: "test-driver-alfa",
    name: "Motorista Alfa",
    email: "motorista.alfa@unirota.local",
  },
  {
    uid: "test-driver-bravo",
    name: "Motorista Bravo",
    email: "motorista.bravo@unirota.local",
  },
  {
    uid: "test-driver-charlie",
    name: "Motorista Charlie",
    email: "motorista.charlie@unirota.local",
  },
  {
    uid: "test-driver-delta",
    name: "Motorista Delta",
    email: "motorista.delta@unirota.local",
  },
  {
    uid: "test-passenger-ana",
    name: "Passageira Ana",
    email: "passageira.ana@unirota.local",
  },
  {
    uid: "test-passenger-bruno",
    name: "Passageiro Bruno",
    email: "passageiro.bruno@unirota.local",
  },
  {
    uid: "test-passenger-carla",
    name: "Passageira Carla",
    email: "passageira.carla@unirota.local",
  },
  {
    uid: "test-passenger-diego",
    name: "Passageiro Diego",
    email: "passageiro.diego@unirota.local",
  },
  {
    uid: "test-user-livre",
    name: "Usuário Livre",
    email: "usuario.livre@unirota.local",
  },
];

const routes = [
  // MOTORISTAS ---------------------------------------------------------------
  {
    id: "route-driver-alfa-manha",
    userId: "test-driver-alfa",
    userName: "Motorista Alfa",
    role: "driver",
    origin: "Centro - Sorocaba",
    originPlaceId: "TEST_PLACE_CENTRO",
    destination: "FACENS - Sorocaba",
    destinationPlaceId: "TEST_PLACE_FACENS",
    daysOfWeek: ["Monday", "Tuesday", "Wednesday"],
    departureTimeMinutes: 420, // 07:00
    availableSeats: 3,
    estimatedDistanceKm: 8.2,
    requestRevision: 0,
  },
  {
    id: "route-driver-bravo-manha",
    userId: "test-driver-bravo",
    userName: "Motorista Bravo",
    role: "driver",
    origin: "Campolim - Sorocaba",
    originPlaceId: "TEST_PLACE_CAMPOLIM",
    destination: "FACENS - Sorocaba",
    destinationPlaceId: "TEST_PLACE_FACENS",
    daysOfWeek: ["Tuesday", "Thursday"],
    departureTimeMinutes: 435, // 07:15
    availableSeats: 2,
    estimatedDistanceKm: 11.4,
    requestRevision: 0,
  },
  {
    id: "route-driver-charlie-noite",
    userId: "test-driver-charlie",
    userName: "Motorista Charlie",
    role: "driver",
    origin: "FACENS - Sorocaba",
    originPlaceId: "TEST_PLACE_FACENS",
    destination: "Centro - Sorocaba",
    destinationPlaceId: "TEST_PLACE_CENTRO",
    daysOfWeek: ["Monday", "Wednesday", "Friday"],
    departureTimeMinutes: 1080, // 18:00
    availableSeats: 4,
    estimatedDistanceKm: 8.2,
    requestRevision: 0,
  },
  {
    id: "route-driver-delta-cheia",
    userId: "test-driver-delta",
    userName: "Motorista Delta",
    role: "driver",
    origin: "Zona Norte - Sorocaba",
    originPlaceId: "TEST_PLACE_ZONA_NORTE",
    destination: "FACENS - Sorocaba",
    destinationPlaceId: "TEST_PLACE_FACENS",
    daysOfWeek: ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
    departureTimeMinutes: 425, // 07:05
    availableSeats: 1,
    estimatedDistanceKm: 13.7,
    requestRevision: 0,
  },

  // PASSAGEIROS --------------------------------------------------------------
  {
    id: "route-passenger-ana-manha",
    userId: "test-passenger-ana",
    userName: "Passageira Ana",
    role: "passenger",
    origin: "Centro - Sorocaba",
    originPlaceId: "TEST_PLACE_CENTRO",
    destination: "FACENS - Sorocaba",
    destinationPlaceId: "TEST_PLACE_FACENS",
    daysOfWeek: ["Monday", "Tuesday"],
    departureTimeMinutes: 430, // 07:10
    availableSeats: null,
    estimatedDistanceKm: 0,
    requestRevision: 0,
  },
  {
    id: "route-passenger-bruno-manha",
    userId: "test-passenger-bruno",
    userName: "Passageiro Bruno",
    role: "passenger",
    origin: "Centro - Sorocaba",
    originPlaceId: "TEST_PLACE_CENTRO",
    destination: "FACENS - Sorocaba",
    destinationPlaceId: "TEST_PLACE_FACENS",
    daysOfWeek: ["Wednesday"],
    departureTimeMinutes: 425, // 07:05
    availableSeats: null,
    estimatedDistanceKm: 0,
    requestRevision: 0,
  },
  {
    id: "route-passenger-carla-manha",
    userId: "test-passenger-carla",
    userName: "Passageira Carla",
    role: "passenger",
    origin: "Campolim - Sorocaba",
    originPlaceId: "TEST_PLACE_CAMPOLIM",
    destination: "FACENS - Sorocaba",
    destinationPlaceId: "TEST_PLACE_FACENS",
    daysOfWeek: ["Tuesday", "Thursday"],
    departureTimeMinutes: 440, // 07:20
    availableSeats: null,
    estimatedDistanceKm: 0,
    requestRevision: 0,
  },
  {
    id: "route-passenger-diego-noite",
    userId: "test-passenger-diego",
    userName: "Passageiro Diego",
    role: "passenger",
    origin: "FACENS - Sorocaba",
    originPlaceId: "TEST_PLACE_FACENS",
    destination: "Centro - Sorocaba",
    destinationPlaceId: "TEST_PLACE_CENTRO",
    daysOfWeek: ["Monday", "Wednesday", "Friday"],
    departureTimeMinutes: 1085, // 18:05
    availableSeats: null,
    estimatedDistanceKm: 0,
    requestRevision: 0,
  },
];

function waitForPort(host, port, timeoutMs = 60000) {
  const startedAt = Date.now();

  return new Promise((resolve, reject) => {
    const attempt = () => {
      const socket = net.createConnection({ host, port });

      socket.once("connect", () => {
        socket.end();
        resolve();
      });

      socket.once("error", () => {
        socket.destroy();
        if (Date.now() - startedAt >= timeoutMs) {
          reject(new Error(`Timeout aguardando ${host}:${port}`));
          return;
        }
        setTimeout(attempt, 500);
      });
    };

    attempt();
  });
}

async function clearAuth(auth) {
  let nextPageToken;

  do {
    const result = await auth.listUsers(1000, nextPageToken);
    if (result.users.length > 0) {
      await auth.deleteUsers(result.users.map((user) => user.uid));
    }
    nextPageToken = result.pageToken;
  } while (nextPageToken);
}

async function clearCollection(db, collectionName) {
  const collection = db.collection(collectionName);

  while (true) {
    const snapshot = await collection.limit(400).get();
    if (snapshot.empty) {
      return;
    }

    const batch = db.batch();
    for (const document of snapshot.docs) {
      batch.delete(document.ref);
    }
    await batch.commit();
  }
}

async function seed() {
  if (getApps().length === 0) {
    initializeApp({ projectId: PROJECT_ID });
  }

  const auth = getAuth();
  const db = getFirestore();

  console.log("\n[seed] Restaurando baseline do UniRota...");

  await Promise.all([
    clearCollection(db, "rideRequests"),
    clearCollection(db, "weeklyRoutes"),
    clearCollection(db, "users"),
  ]);
  await clearAuth(auth);

  for (const user of users) {
    await auth.createUser({
      uid: user.uid,
      email: user.email,
      password: DEFAULT_PASSWORD,
      displayName: user.name,
      emailVerified: true,
    });

    await db.collection("users").doc(user.uid).set({
      id: user.uid,
      name: user.name,
      email: user.email,
      createdAtUtc: Timestamp.fromDate(CREATED_AT),
    });
  }

  const routeBatch = db.batch();
  routes.forEach((route, index) => {
    const createdAt = new Date(CREATED_AT.getTime() + (index + 1) * 60000);
    const ref = db.collection("weeklyRoutes").doc(route.id);

    routeBatch.set(ref, {
      userId: route.userId,
      userName: route.userName,
      role: route.role,
      origin: route.origin,
      originPlaceId: route.originPlaceId,
      destination: route.destination,
      destinationPlaceId: route.destinationPlaceId,
      daysOfWeek: route.daysOfWeek,
      departureTimeMinutes: route.departureTimeMinutes,
      availableSeats: route.availableSeats,
      estimatedDistanceKm: route.estimatedDistanceKm,
      requestRevision: route.requestRevision,
      createdAtUtc: Timestamp.fromDate(createdAt),
    });
  });
  await routeBatch.commit();

  console.log(`[seed] ${users.length} usuários criados no Auth.`);
  console.log(`[seed] ${users.length} documentos em users.`);
  console.log(`[seed] ${routes.length} rotas em weeklyRoutes.`);
  console.log("[seed] rideRequests iniciado vazio.");
  console.log("\nCredenciais de teste (mesma senha para todos):");
  console.log(`Senha: ${DEFAULT_PASSWORD}\n`);
  for (const user of users) {
    console.log(`- ${user.name.padEnd(18)} ${user.email}`);
  }
  console.log("\nBaseline pronto.");
  console.log(`Emulator UI: http://localhost:${EMULATOR_UI_PORT}\n`);
}

async function main() {
  const seedOnly = process.argv.includes("--seed-only");
  let firebaseProcess = null;

  if (!seedOnly) {
    console.log("Iniciando Firebase Emulator Suite...");
    firebaseProcess = spawn(
      "firebase",
      ["emulators:start", "--project", PROJECT_ID],
      {
        cwd: process.cwd(),
        stdio: "inherit",
        shell: process.platform === "win32",
      });

    firebaseProcess.once("exit", (code) => {
      if (code && code !== 0) {
        process.exitCode = code;
      }
    });
  }

  try {
    await Promise.all([
      waitForPort(AUTH_HOST, AUTH_PORT),
      waitForPort(FIRESTORE_HOST, FIRESTORE_PORT),
    ]);
    await seed();
  } catch (error) {
    console.error("\nFalha ao preparar o dataset padrão:", error);
    if (firebaseProcess) {
      firebaseProcess.kill();
    }
    process.exit(1);
  }

  if (seedOnly) {
    return;
  }

  const shutdown = () => {
    if (firebaseProcess && !firebaseProcess.killed) {
      firebaseProcess.kill("SIGINT");
    }
  };

  process.on("SIGINT", shutdown);
  process.on("SIGTERM", shutdown);
}

main();
