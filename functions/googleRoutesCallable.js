"use strict";

const { HttpsError } = require("firebase-functions/v2/https");
const { GoogleRoutesError, computeRoute } = require("./googleRoutes");

function createComputeRouteHandler({
  getApiKey,
  computeRouteImpl = computeRoute,
}) {
  return async (request) => {
    requireAuthenticatedUser(request);
    const originPlaceId = requirePlaceId(
      request.data?.originPlaceId,
      "originPlaceId");
    const destinationPlaceId = requirePlaceId(
      request.data?.destinationPlaceId,
      "destinationPlaceId");
    const intermediatePlaceIds = requireIntermediatePlaceIds(
      request.data?.intermediatePlaceIds,
      originPlaceId,
      destinationPlaceId);

    try {
      return await computeRouteImpl({
        apiKey: getApiKey(),
        originPlaceId,
        destinationPlaceId,
        intermediatePlaceIds,
      });
    } catch (error) {
      throw mapRoutesError(error);
    }
  };
}

function requireAuthenticatedUser(request) {
  if (!request.auth) {
    throw new HttpsError("unauthenticated", "Authentication is required.");
  }
}

function requirePlaceId(value, fieldName) {
  if (typeof value !== "string") {
    throw new HttpsError("invalid-argument", `${fieldName} is required.`);
  }

  const normalized = value.trim();

  if (normalized.length < 1 || normalized.length > 300) {
    throw new HttpsError("invalid-argument", `${fieldName} is invalid.`);
  }

  return normalized;
}

function requireIntermediatePlaceIds(
  value,
  originPlaceId,
  destinationPlaceId) {
  if (value === undefined) {
    return [];
  }

  if (!Array.isArray(value) || value.length > 2) {
    throw new HttpsError(
      "invalid-argument",
      "intermediatePlaceIds is invalid.");
  }

  const normalized = value.map((placeId, index) =>
    requirePlaceId(placeId, `intermediatePlaceIds[${index}]`));
  const sequence = [originPlaceId, ...normalized, destinationPlaceId];

  for (let index = 1; index < sequence.length; index++) {
    if (sequence[index - 1] === sequence[index]) {
      throw new HttpsError(
        "invalid-argument",
        "Consecutive route points must be distinct.");
    }
  }

  return normalized;
}

function mapRoutesError(error) {
  if (!(error instanceof GoogleRoutesError)) {
    return new HttpsError("internal", "Routes request failed.");
  }

  if (error.httpStatus === 429 || error.googleStatus === "RESOURCE_EXHAUSTED") {
    return new HttpsError("resource-exhausted", "Routes quota exceeded.");
  }

  if (error.googleStatus === "DEADLINE_EXCEEDED") {
    return new HttpsError("deadline-exceeded", "Routes request timed out.");
  }

  if (error.googleStatus === "NO_ROUTE") {
    return new HttpsError("failed-precondition", "No route was found.");
  }

  if (error.httpStatus >= 500 || error.googleStatus === "UNAVAILABLE") {
    return new HttpsError("unavailable", "Routes is temporarily unavailable.");
  }

  if (error.httpStatus === 400) {
    return new HttpsError("invalid-argument", "Routes rejected the request.");
  }

  return new HttpsError("internal", "Routes request failed.");
}

module.exports = {
  createComputeRouteHandler,
};
