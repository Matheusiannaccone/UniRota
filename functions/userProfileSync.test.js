"use strict";

const assert = require("node:assert/strict");
const test = require("node:test");
const {
  MAX_BATCH_WRITES,
  synchronizeWeeklyRouteUserNames,
} = require("./userProfileSync");

test("does not query routes when the user name did not change", async () => {
  const db = {
    collection: () => {
      throw new Error("Routes must not be queried.");
    },
  };

  const result = await synchronizeWeeklyRouteUserNames({
    db,
    uid: "user-1",
    beforeName: "Ana",
    afterName: "Ana",
  });

  assert.deepEqual(result, { updatedRouteCount: 0 });
});

test("updates only weekly routes belonging to the user", async () => {
  const fixture = createFirestore([
    { id: "route-1", userId: "user-1", userName: "Ana" },
    { id: "route-2", userId: "user-1", userName: "Ana" },
    { id: "route-3", userId: "user-2", userName: "Ana" },
  ]);

  const result = await synchronizeWeeklyRouteUserNames({
    db: fixture.db,
    uid: "user-1",
    beforeName: "Ana",
    afterName: "Beatriz",
  });

  assert.equal(result.updatedRouteCount, 2);
  assert.deepEqual(fixture.queries, [{ field: "userId", operator: "==", value: "user-1" }]);
  assert.equal(fixture.routes[0].userName, "Beatriz");
  assert.equal(fixture.routes[1].userName, "Beatriz");
  assert.equal(fixture.routes[2].userName, "Ana");
  assert.deepEqual(fixture.updatedFields, ["userName", "userName"]);
});

test("does not write a route that already has the new name", async () => {
  const fixture = createFirestore([
    { id: "route-1", userId: "user-1", userName: "Beatriz" },
  ]);

  const result = await synchronizeWeeklyRouteUserNames({
    db: fixture.db,
    uid: "user-1",
    beforeName: "Ana",
    afterName: "Beatriz",
  });

  assert.equal(result.updatedRouteCount, 0);
  assert.equal(fixture.commits.length, 0);
});

test("finishes without writes when the user has no weekly routes", async () => {
  const fixture = createFirestore([]);

  const result = await synchronizeWeeklyRouteUserNames({
    db: fixture.db,
    uid: "user-1",
    beforeName: "Ana",
    afterName: "Beatriz",
  });

  assert.equal(result.updatedRouteCount, 0);
  assert.equal(fixture.commits.length, 0);
});

test("rejects an invalid new user name before querying routes", async () => {
  const db = {
    collection: () => {
      throw new Error("Routes must not be queried.");
    },
  };

  await assert.rejects(
    synchronizeWeeklyRouteUserNames({
      db,
      uid: "user-1",
      beforeName: "Ana",
      afterName: "   ",
    }),
    /Invalid user name/);
});

test("splits writes above a batch limit without losing routes", async () => {
  const routeCount = MAX_BATCH_WRITES + 51;
  const fixture = createFirestore(Array.from({ length: routeCount }, (_, index) => ({
    id: `route-${index}`,
    userId: "user-1",
    userName: "Ana",
  })));

  const result = await synchronizeWeeklyRouteUserNames({
    db: fixture.db,
    uid: "user-1",
    beforeName: "Ana",
    afterName: "Beatriz",
  });

  assert.equal(result.updatedRouteCount, routeCount);
  assert.deepEqual(fixture.commits.map((writes) => writes.length), [MAX_BATCH_WRITES, 51]);
  assert.ok(fixture.routes.every((route) => route.userName === "Beatriz"));
});

function createFirestore(routes) {
  const queries = [];
  const commits = [];
  const updatedFields = [];

  const db = {
    collection(name) {
      assert.equal(name, "weeklyRoutes");
      return {
        where(field, operator, value) {
          queries.push({ field, operator, value });
          return {
            async get() {
              return {
                docs: routes
                  .filter((route) => route.userId === value)
                  .map((route) => ({
                    data: () => ({ userName: route.userName }),
                    ref: { route },
                  })),
              };
            },
          };
        },
      };
    },
    batch() {
      const writes = [];
      return {
        update(ref, data) {
          writes.push({ ref, data });
        },
        async commit() {
          for (const { ref, data } of writes) {
            updatedFields.push(...Object.keys(data));
            Object.assign(ref.route, data);
          }
          commits.push(writes);
        },
      };
    },
  };

  return { db, routes, queries, commits, updatedFields };
}
