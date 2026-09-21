"use strict";

const assert = require("node:assert/strict");
const test = require("node:test");
const {
  GoogleRoutesError,
  computeRoute,
  parseDurationSeconds,
} = require("./googleRoutes");
const { createComputeRouteHandler } = require("./googleRoutesCallable");

test("computeRoute callable requires authentication", async () => {
  const handler = createComputeRouteHandler({
    getApiKey: () => "test-key",
    computeRouteImpl: async () => {
      throw new Error("should not be called");
    },
  });

  await assert.rejects(
    handler({
      data: {
        originPlaceId: "origin-place-id",
        destinationPlaceId: "destination-place-id",
      },
    }),
    (error) => error.code === "unauthenticated");
});

test("computeRoute sends Place IDs, driving mode, and minimal field mask", async () => {
  let capturedUrl;
  let capturedOptions;
  const result = await computeRoute({
    apiKey: "test-routes-key",
    originPlaceId: "origin-place-id",
    destinationPlaceId: "destination-place-id",
    fetchImpl: async (url, options) => {
      capturedUrl = url;
      capturedOptions = options;
      return jsonResponse({
        routes: [{ distanceMeters: 11840, duration: "1325.5s" }],
      });
    },
  });

  assert.equal(
    capturedUrl,
    "https://routes.googleapis.com/directions/v2:computeRoutes");
  assert.equal(capturedOptions.headers["X-Goog-Api-Key"], "test-routes-key");
  assert.equal(
    capturedOptions.headers["X-Goog-FieldMask"],
    "routes.distanceMeters,routes.duration");

  const body = JSON.parse(capturedOptions.body);
  assert.deepEqual(body.origin, { placeId: "origin-place-id" });
  assert.deepEqual(body.destination, { placeId: "destination-place-id" });
  assert.equal(body.travelMode, "DRIVE");
  assert.equal(body.computeAlternativeRoutes, false);
  assert.equal("intermediates" in body, false);
  assert.deepEqual(result, {
    distanceMeters: 11840,
    durationSeconds: 1325.5,
  });
});

test("computeRoute sends one intermediate Place ID", async () => {
  let capturedBody;

  await computeRoute({
    apiKey: "test-key",
    originPlaceId: "driver-origin",
    destinationPlaceId: "driver-destination",
    intermediatePlaceIds: ["passenger-origin"],
    fetchImpl: async (url, options) => {
      capturedBody = JSON.parse(options.body);
      return jsonResponse({
        routes: [{ distanceMeters: 12000, duration: "1200s" }],
      });
    },
  });

  assert.deepEqual(capturedBody.intermediates, [
    { placeId: "passenger-origin" },
  ]);
});

test("computeRoute sends two intermediate Place IDs in order", async () => {
  let capturedBody;

  await computeRoute({
    apiKey: "test-key",
    originPlaceId: "driver-origin",
    destinationPlaceId: "driver-destination",
    intermediatePlaceIds: ["passenger-origin", "passenger-destination"],
    fetchImpl: async (url, options) => {
      capturedBody = JSON.parse(options.body);
      return jsonResponse({
        routes: [{ distanceMeters: 14000, duration: "1500s" }],
      });
    },
  });

  assert.deepEqual(capturedBody.intermediates, [
    { placeId: "passenger-origin" },
    { placeId: "passenger-destination" },
  ]);
});

test("computeRoute callable rejects more than two intermediates", async () => {
  let callCount = 0;
  const handler = createComputeRouteHandler({
    getApiKey: () => "test-key",
    computeRouteImpl: async () => {
      callCount++;
    },
  });

  await assert.rejects(
    handler(authenticatedRequest({
      intermediatePlaceIds: ["one", "two", "three"],
    })),
    (error) => error.code === "invalid-argument");
  assert.equal(callCount, 0);
});

test("computeRoute callable rejects an empty intermediate", async () => {
  const handler = createComputeRouteHandler({
    getApiKey: () => "test-key",
    computeRouteImpl: async () => {
      throw new Error("should not be called");
    },
  });

  await assert.rejects(
    handler(authenticatedRequest({ intermediatePlaceIds: [" "] })),
    (error) => error.code === "invalid-argument");
});

test("computeRoute callable rejects consecutive duplicate route points", async () => {
  const handler = createComputeRouteHandler({
    getApiKey: () => "test-key",
    computeRouteImpl: async () => {
      throw new Error("should not be called");
    },
  });

  await assert.rejects(
    handler(authenticatedRequest({
      intermediatePlaceIds: ["origin-place-id"],
    })),
    (error) => error.code === "invalid-argument");
});

test("duration parser accepts protobuf seconds and rejects invalid values", () => {
  assert.equal(parseDurationSeconds("1325s"), 1325);
  assert.equal(parseDurationSeconds("1325.123456789s"), 1325.123456789);
  assert.equal(parseDurationSeconds("1325ms"), null);
  assert.equal(parseDurationSeconds("invalid"), null);
});

test("computeRoute reports an empty routes response", async () => {
  await assert.rejects(
    computeRoute({
      apiKey: "test-key",
      originPlaceId: "origin-place-id",
      destinationPlaceId: "destination-place-id",
      fetchImpl: async () => jsonResponse({ routes: [] }),
    }),
    (error) => error instanceof GoogleRoutesError
      && error.googleStatus === "NO_ROUTE");
});

test("computeRoute retains quota errors from the Google API", async () => {
  await assert.rejects(
    computeRoute({
      apiKey: "test-key",
      originPlaceId: "origin-place-id",
      destinationPlaceId: "destination-place-id",
      fetchImpl: async () => jsonResponse(
        { error: { status: "RESOURCE_EXHAUSTED" } },
        429),
    }),
    (error) => error instanceof GoogleRoutesError
      && error.httpStatus === 429
      && error.googleStatus === "RESOURCE_EXHAUSTED");
});

test("computeRoute callable maps no-route responses safely", async () => {
  const handler = createComputeRouteHandler({
    getApiKey: () => "test-key",
    computeRouteImpl: async () => {
      throw new GoogleRoutesError(404, "NO_ROUTE");
    },
  });

  await assert.rejects(
    handler({
      auth: { uid: "user-1" },
      data: {
        originPlaceId: "origin-place-id",
        destinationPlaceId: "destination-place-id",
      },
    }),
    (error) => error.code === "failed-precondition");
});

function jsonResponse(payload, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => payload,
  };
}

function authenticatedRequest(overrides = {}) {
  return {
    auth: { uid: "user-1" },
    data: {
      originPlaceId: "origin-place-id",
      destinationPlaceId: "destination-place-id",
      ...overrides,
    },
  };
}
