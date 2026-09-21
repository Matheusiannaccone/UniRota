"use strict";

const { onCall, HttpsError } = require("firebase-functions/v2/https");
const { defineSecret } = require("firebase-functions/params");
const {
  GooglePlacesError,
  autocompletePlaces,
  getPlaceDetails,
} = require("./googlePlaces");

const googlePlacesApiKey = defineSecret("GOOGLE_PLACES_API_KEY");
const callableOptions = {
  region: "southamerica-east1",
  secrets: [googlePlacesApiKey],
  timeoutSeconds: 15,
};

exports.placesAutocomplete = onCall(callableOptions, async (request) => {
  requireAuthenticatedUser(request);
  const input = requireText(request.data?.input, "input", 3, 200);
  const sessionToken = requireSessionToken(request.data?.sessionToken);

  try {
    return await autocompletePlaces({
      apiKey: googlePlacesApiKey.value(),
      input,
      sessionToken,
    });
  } catch (error) {
    throw mapPlacesError(error);
  }
});

exports.placeDetails = onCall(callableOptions, async (request) => {
  requireAuthenticatedUser(request);
  const placeId = requireText(request.data?.placeId, "placeId", 1, 300);
  const sessionToken = requireSessionToken(request.data?.sessionToken);

  try {
    return await getPlaceDetails({
      apiKey: googlePlacesApiKey.value(),
      placeId,
      sessionToken,
    });
  } catch (error) {
    throw mapPlacesError(error);
  }
});

function requireAuthenticatedUser(request) {
  if (!request.auth) {
    throw new HttpsError("unauthenticated", "Authentication is required.");
  }
}

function requireText(value, fieldName, minimumLength, maximumLength) {
  if (typeof value !== "string") {
    throw new HttpsError("invalid-argument", `${fieldName} is required.`);
  }

  const normalized = value.trim();

  if (normalized.length < minimumLength || normalized.length > maximumLength) {
    throw new HttpsError("invalid-argument", `${fieldName} is invalid.`);
  }

  return normalized;
}

function requireSessionToken(value) {
  const token = requireText(value, "sessionToken", 36, 36);
  const uuidV4 =
    /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

  if (!uuidV4.test(token)) {
    throw new HttpsError("invalid-argument", "sessionToken is invalid.");
  }

  return token;
}

function mapPlacesError(error) {
  if (!(error instanceof GooglePlacesError)) {
    return new HttpsError("internal", "Places request failed.");
  }

  if (error.httpStatus === 429 || error.googleStatus === "RESOURCE_EXHAUSTED") {
    return new HttpsError("resource-exhausted", "Places quota exceeded.");
  }

  if (error.httpStatus >= 500) {
    return new HttpsError("unavailable", "Places is temporarily unavailable.");
  }

  if (error.httpStatus === 400) {
    return new HttpsError("invalid-argument", "Places rejected the request.");
  }

  return new HttpsError("internal", "Places request failed.");
}
