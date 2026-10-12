"use strict";

const { getApps, initializeApp } = require("firebase-admin/app");
const { getFirestore } = require("firebase-admin/firestore");

function getFirestoreDb() {
  if (getApps().length === 0) {
    initializeApp();
  }

  return getFirestore();
}

module.exports = { getFirestoreDb };
