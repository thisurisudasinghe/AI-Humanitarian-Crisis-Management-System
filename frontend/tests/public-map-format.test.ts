import { test } from "node:test";
import assert from "node:assert/strict";
import {
  incidentMarkerType,
  publicMapSeverityLabelKey,
  publicMapStatusLabelKey,
  publicMapTypeLabelKey
} from "../lib/publicMapFormat.ts";

test("severity label key tolerates null, undefined and blank values", () => {
  assert.equal(publicMapSeverityLabelKey(null), null);
  assert.equal(publicMapSeverityLabelKey(undefined), null);
  assert.equal(publicMapSeverityLabelKey(""), null);
  assert.equal(publicMapSeverityLabelKey("   "), null);
});

test("severity label key maps known values and leaves custom values untranslated", () => {
  assert.equal(publicMapSeverityLabelKey(" High "), "publicMap.severityHigh");
  assert.equal(publicMapSeverityLabelKey("medium"), "publicMap.severityMedium");
  assert.equal(publicMapSeverityLabelKey("Severe"), null);
});

test("incident type helpers tolerate missing values", () => {
  assert.equal(incidentMarkerType(null), "other-crisis");
  assert.equal(incidentMarkerType(undefined), "other-crisis");
  assert.equal(incidentMarkerType("  "), "other-crisis");
  assert.equal(publicMapTypeLabelKey(null), "publicMap.typeOtherCrisis");
});

test("status label key tolerates null and blank values", () => {
  assert.equal(publicMapStatusLabelKey(null), null);
  assert.equal(publicMapStatusLabelKey("  "), null);
  assert.equal(publicMapStatusLabelKey("Published"), "publicMap.published");
});
