import { test } from "node:test";
import assert from "node:assert/strict";
import {
  resolveVerificationResult,
  resendView,
  verificationView
} from "../lib/verification-state.ts";

test("loading state is non-actionable", () => {
  const view = verificationView("loading");
  assert.equal(view.state, "loading");
  assert.equal(view.canResend, false);
  assert.match(view.message, /Verifying/);
});

test("successful verification shows the continue state", () => {
  const view = resolveVerificationResult({ ok: true, status: 200 });
  assert.equal(view.state, "success");
  assert.equal(view.tone, "success");
  assert.equal(view.canResend, false);
});

test("already verified response is distinct from a fresh success", () => {
  const view = resolveVerificationResult({
    ok: true,
    status: 200,
    alreadyVerified: true
  });
  assert.equal(view.state, "already-verified");
  assert.match(view.title, /Already verified/);
  assert.equal(view.canResend, false);
});

test("expired token offers a resend option", () => {
  const view = resolveVerificationResult({
    ok: false,
    status: 400,
    code: "VERIFICATION_TOKEN_EXPIRED"
  });
  assert.equal(view.state, "expired");
  assert.equal(view.canResend, true);
  assert.match(view.message, /30 minutes/);
});

test("invalid and missing tokens map to the invalid state", () => {
  const invalid = resolveVerificationResult({
    ok: false,
    status: 400,
    code: "VERIFICATION_TOKEN_INVALID"
  });
  assert.equal(invalid.state, "invalid");

  const missing = resolveVerificationResult({
    ok: false,
    status: 422,
    code: "VALIDATION_FAILED"
  });
  assert.equal(missing.state, "invalid");
});

test("an already used token resolves to the already-verified state", () => {
  const view = resolveVerificationResult({
    ok: false,
    status: 400,
    code: "VERIFICATION_TOKEN_ALREADY_USED"
  });
  assert.equal(view.state, "already-verified");
  assert.equal(view.tone, "success");
  assert.equal(view.canResend, false);
});

test("revoked token explains that a newer link exists", () => {
  const view = resolveVerificationResult({
    ok: false,
    status: 400,
    code: "VERIFICATION_TOKEN_REVOKED"
  });
  assert.equal(view.state, "revoked");
  assert.match(view.message, /newest link/);
});

test("rate limiting and unexpected failures are handled separately", () => {
  const limited = resolveVerificationResult({ ok: false, status: 429 });
  assert.equal(limited.state, "rate-limited");

  const unexpected = resolveVerificationResult({ ok: false, status: 500 });
  assert.equal(unexpected.state, "error");
  assert.equal(unexpected.canResend, true);
});

test("resend states cover idle, loading, sent and error", () => {
  assert.match(resendView("idle").message, /Request a fresh link/);
  assert.match(resendView("loading").message, /Sending/);
  assert.match(resendView("sent").message, /new link has been sent/);
  assert.match(resendView("error", 500).message, /could not send/);
  assert.match(resendView("error", 429).message, /Too many requests/);
});
