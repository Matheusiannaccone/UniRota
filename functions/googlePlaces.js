"use strict";

const AUTOCOMPLETE_URL =
  "https://places.googleapis.com/v1/places:autocomplete";
const PLACE_DETAILS_BASE_URL =
  "https://places.googleapis.com/v1/places";

async function autocompletePlaces({ apiKey, input, sessionToken, fetchImpl = fetch }) {
  const response = await fetchImpl(AUTOCOMPLETE_URL, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Goog-Api-Key": apiKey,
      "X-Goog-FieldMask":
        "suggestions.placePrediction.placeId," +
        "suggestions.placePrediction.text.text",
    },
    body: JSON.stringify({
      input,
      sessionToken,
      languageCode: "pt-BR",
      regionCode: "br",
    }),
  });

  const payload = await readGoogleResponse(response);
  const suggestions = Array.isArray(payload.suggestions)
    ? payload.suggestions
    : [];

  return {
    suggestions: suggestions
      .map((suggestion) => suggestion.placePrediction)
      .filter((prediction) =>
        prediction &&
        typeof prediction.placeId === "string" &&
        typeof prediction.text?.text === "string")
      .slice(0, 5)
      .map((prediction) => ({
        placeId: prediction.placeId,
        displayText: prediction.text.text,
      })),
  };
}

async function getPlaceDetails({ apiKey, placeId, sessionToken, fetchImpl = fetch }) {
  const url = new URL(
    `${PLACE_DETAILS_BASE_URL}/${encodeURIComponent(placeId)}`);
  url.searchParams.set("sessionToken", sessionToken);
  url.searchParams.set("languageCode", "pt-BR");
  url.searchParams.set("regionCode", "br");

  const response = await fetchImpl(url, {
    method: "GET",
    headers: {
      "X-Goog-Api-Key": apiKey,
      "X-Goog-FieldMask": "id,formattedAddress",
    },
  });

  const payload = await readGoogleResponse(response);

  if (typeof payload.id !== "string"
      || typeof payload.formattedAddress !== "string") {
    throw new GooglePlacesError(502, "INVALID_RESPONSE");
  }

  return {
    placeId: payload.id,
    address: payload.formattedAddress,
  };
}

async function getPlaceCoordinates({ apiKey, placeId, fetchImpl = fetch }) {
  const url = new URL(
    `${PLACE_DETAILS_BASE_URL}/${encodeURIComponent(placeId)}`);

  const response = await fetchImpl(url, {
    method: "GET",
    headers: {
      "X-Goog-Api-Key": apiKey,
      "X-Goog-FieldMask": "location",
    },
  });
  const payload = await readGoogleResponse(response);
  const latitude = payload.location?.latitude;
  const longitude = payload.location?.longitude;

  if (!Number.isFinite(latitude)
      || !Number.isFinite(longitude)
      || latitude < -90
      || latitude > 90
      || longitude < -180
      || longitude > 180) {
    throw new GooglePlacesError(502, "INVALID_RESPONSE");
  }

  return { placeId, latitude, longitude };
}

async function readGoogleResponse(response) {
  let payload;

  try {
    payload = await response.json();
  } catch {
    throw new GooglePlacesError(502, "INVALID_RESPONSE");
  }

  if (!response.ok) {
    const status = payload?.error?.status || "GOOGLE_PLACES_ERROR";
    throw new GooglePlacesError(response.status, status);
  }

  return payload;
}

class GooglePlacesError extends Error {
  constructor(httpStatus, googleStatus) {
    super(googleStatus);
    this.name = "GooglePlacesError";
    this.httpStatus = httpStatus;
    this.googleStatus = googleStatus;
  }
}

module.exports = {
  GooglePlacesError,
  autocompletePlaces,
  getPlaceCoordinates,
  getPlaceDetails,
};
