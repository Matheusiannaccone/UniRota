"use strict";

const MAX_BATCH_WRITES = 450;

function validateUserName(value) {
  if (typeof value !== "string" || value.trim().length === 0 || value.length > 100) {
    throw new TypeError("Invalid user name for weekly route synchronization.");
  }

  return value;
}

async function synchronizeWeeklyRouteUserNames({ db, uid, beforeName, afterName }) {
  if (beforeName === afterName) {
    return { updatedRouteCount: 0 };
  }

  const userName = validateUserName(afterName);
  const snapshot = await db.collection("weeklyRoutes").where("userId", "==", uid).get();
  const routesToUpdate = snapshot.docs.filter((route) => route.data().userName !== userName);

  for (let index = 0; index < routesToUpdate.length; index += MAX_BATCH_WRITES) {
    const batch = db.batch();
    for (const route of routesToUpdate.slice(index, index + MAX_BATCH_WRITES)) {
      batch.update(route.ref, { userName });
    }
    await batch.commit();
  }

  return { updatedRouteCount: routesToUpdate.length };
}

module.exports = {
  MAX_BATCH_WRITES,
  synchronizeWeeklyRouteUserNames,
  validateUserName,
};
