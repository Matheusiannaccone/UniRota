"use strict";

const COMPUTE_ROUTES_URL =
  "https://routes.googleapis.com/directions/v2:computeRoutes";
const ROUTE_FIELD_MASK = "routes.distanceMeters,routes.duration";
const REQUEST_TIMEOUT_MILLISECONDS = 10000;

async function computeRoute({
  apiKey,
  originPlaceId,
  destinationPlaceId,
  fetchImpl = fetch,
}) {
  let response;

  try {
    response = await fetchImpl(COMPUTE_ROUTES_URL, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Goog-Api-Key": apiKey,
        "X-Goog-FieldMask": ROUTE_FIELD_MASK,
      },
      body: JSON.stringify({
        origin: { placeId: originPlaceId },
        destination: { placeId: destinationPlaceId },
        travelMode: "DRIVE",
        computeAlternativeRoutes: false,
      }),
      signal: AbortSignal.timeout(REQUEST_TIMEOUT_MILLISECONDS),
    });
  } catch (error) {
    const status = error?.name === "AbortError"
      || error?.name === "TimeoutError"
      ? "DEADLINE_EXCEEDED"
      : "UNAVAILABLE";
    throw new GoogleRoutesError(
      status === "DEADLINE_EXCEEDED" ? 504 : 503,
      status);
  }

  const payload = await readGoogleResponse(response);
  const route = Array.isArray(payload.routes) ? payload.routes[0] : undefined;

  if (!route) {
    throw new GoogleRoutesError(404, "NO_ROUTE");
  }

  const durationSeconds = parseDurationSeconds(route.duration);

  if (!Number.isSafeInteger(route.distanceMeters)
      || route.distanceMeters <= 0
      || durationSeconds === null
      || durationSeconds <= 0) {
    throw new GoogleRoutesError(502, "INVALID_RESPONSE");
  }

  return {
    distanceMeters: route.distanceMeters,
    durationSeconds,
  };
}

function parseDurationSeconds(value) {
  if (typeof value !== "string"
      || !/^[0-9]+(?:\.[0-9]{1,9})?s$/.test(value)) {
    return null;
  }

  const seconds = Number(value.slice(0, -1));
  return Number.isFinite(seconds) ? seconds : null;
}

async function readGoogleResponse(response) {
  let payload;

  try {
    payload = await response.json();
  } catch {
    throw new GoogleRoutesError(502, "INVALID_RESPONSE");
  }

  if (!response.ok) {
    const status = payload?.error?.status || "GOOGLE_ROUTES_ERROR";
    throw new GoogleRoutesError(response.status, status);
  }

  return payload;
}

class GoogleRoutesError extends Error {
  constructor(httpStatus, googleStatus) {
    super(googleStatus);
    this.name = "GoogleRoutesError";
    this.httpStatus = httpStatus;
    this.googleStatus = googleStatus;
  }
}

module.exports = {
  GoogleRoutesError,
  computeRoute,
  parseDurationSeconds,
};
