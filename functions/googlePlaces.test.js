"use strict";

const assert = require("node:assert/strict");
const test = require("node:test");
const {
  GooglePlacesError,
  autocompletePlaces,
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

function jsonResponse(payload, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => payload,
  };
}
