"use strict";

const assert = require("node:assert/strict");
const test = require("node:test");
const {
  GooglePlacesError,
  autocompletePlaces,
  getPlaceCoordinates,
  getPlaceDetails,
} = require("./googlePlaces");

test("autocomplete forwards the session token and returns only required data", async () => {
  let capturedOptions;
  const result = await autocompletePlaces({
    apiKey: "test-key",
    input: "Facens",
    sessionToken: "11111111-1111-4111-8111-111111111111",
    fetchImpl: async (url, options) => {
      assert.equal(url, "https://places.googleapis.com/v1/places:autocomplete");
      capturedOptions = options;
      return jsonResponse({
        suggestions: [
          {
            placePrediction: {
              placeId: "place-1",
              text: { text: "Facens, Sorocaba" },
            },
          },
        ],
      });
    },
  });

  const body = JSON.parse(capturedOptions.body);
  assert.equal(body.sessionToken, "11111111-1111-4111-8111-111111111111");
  assert.equal(body.regionCode, "br");
  assert.equal(
    capturedOptions.headers["X-Goog-FieldMask"],
    "suggestions.placePrediction.placeId," +
      "suggestions.placePrediction.text.text");
  assert.deepEqual(result, {
    suggestions: [{ placeId: "place-1", displayText: "Facens, Sorocaba" }],
  });
});

test("place details terminates the session with minimal fields", async () => {
  let capturedUrl;
  let capturedOptions;
  const result = await getPlaceDetails({
    apiKey: "test-key",
    placeId: "place/with spaces",
    sessionToken: "11111111-1111-4111-8111-111111111111",
    fetchImpl: async (url, options) => {
      capturedUrl = url;
      capturedOptions = options;
      return jsonResponse({
        id: "canonical-place-id",
        formattedAddress: "Rodovia Senador José Ermírio de Moraes, Sorocaba",
      });
    },
  });

  assert.match(capturedUrl.toString(), /place%2Fwith%20spaces/);
  assert.equal(
    capturedUrl.searchParams.get("sessionToken"),
    "11111111-1111-4111-8111-111111111111");
  assert.equal(capturedOptions.headers["X-Goog-FieldMask"], "id,formattedAddress");
  assert.equal(result.placeId, "canonical-place-id");
});

test("quota errors retain a safe machine-readable status", async () => {
  await assert.rejects(
    autocompletePlaces({
      apiKey: "test-key",
      input: "Facens",
      sessionToken: "11111111-1111-4111-8111-111111111111",
      fetchImpl: async () => jsonResponse(
        { error: { status: "RESOURCE_EXHAUSTED" } },
        429),
    }),
    (error) => error instanceof GooglePlacesError
      && error.googleStatus === "RESOURCE_EXHAUSTED");
});

test("place coordinates requests only the location field", async () => {
  let capturedOptions;
  const result = await getPlaceCoordinates({
    apiKey: "test-key",
    placeId: "place-id",
    fetchImpl: async (url, options) => {
      capturedOptions = options;
      return jsonResponse({
        location: { latitude: -23.4708, longitude: -47.4287 },
      });
    },
  });

  assert.equal(capturedOptions.headers["X-Goog-FieldMask"], "location");
  assert.deepEqual(result, {
    placeId: "place-id",
    latitude: -23.4708,
    longitude: -47.4287,
  });
});

test("place coordinates rejects an invalid location response", async () => {
  await assert.rejects(
    getPlaceCoordinates({
      apiKey: "test-key",
      placeId: "place-id",
      fetchImpl: async () => jsonResponse({ location: {} }),
    }),
    (error) => error instanceof GooglePlacesError
      && error.googleStatus === "INVALID_RESPONSE");
});

test("place coordinates callable requires authentication", async () => {
  const { placeCoordinates } = require("./index");

  await assert.rejects(
    placeCoordinates.run({ data: { placeIds: ["place-id"] } }),
    (error) => error.code === "unauthenticated");
});

function jsonResponse(payload, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => payload,
  };
}
