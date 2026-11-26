export type VerificationState =
  | "loading"
  | "success"
  | "already-verified"
  | "invalid"
  | "expired"
  | "revoked"
  | "rate-limited"
  | "error";

export type VerificationTone = "success" | "warning" | "danger";

export interface VerificationView {
  state: VerificationState;
  tone: VerificationTone;
  title: string;
  message: string;
  canResend: boolean;
}

export interface VerificationResultInput {
  ok: boolean;
  status: number;
  code?: string;
  alreadyVerified?: boolean;
}

const VIEWS: Record<VerificationState, VerificationView> = {
  loading: {
    state: "loading",
    tone: "warning",
    title: "Checking your link",
    message: "Verifying your Safeway email address…",
    canResend: false
  },
  success: {
    state: "success",
    tone: "success",
    title: "Email verified",
    message:
      "Your Safeway email address is verified. You can now continue to your account.",
    canResend: false
  },
  "already-verified": {
    state: "already-verified",
    tone: "success",
    title: "Already verified",
    message:
      "This email address has already been verified. No further action is needed.",
    canResend: false
  },
  invalid: {
    state: "invalid",
    tone: "danger",
    title: "This link is not valid",
    message:
      "The verification link may be incomplete or incorrect. You can request a fresh one.",
    canResend: true
  },
  expired: {
    state: "expired",
    tone: "warning",
    title: "This link has expired",
    message:
      "Verification links are valid for 30 minutes. Request a new link and we will email it to you.",
    canResend: true
  },
  revoked: {
    state: "revoked",
    tone: "warning",
    title: "This link has been replaced",
    message:
      "You requested a newer verification email. Use the newest link in your inbox.",
    canResend: true
  },
  "rate-limited": {
    state: "rate-limited",
    tone: "warning",
    title: "Please slow down",
    message:
      "Too many verification requests were made. Wait a moment before asking for another link.",
    canResend: true
  },
  error: {
    state: "error",
    tone: "danger",
    title: "We could not verify right now",
    message:
      "Something went wrong while checking your link. Please try again or request a new one.",
    canResend: true
  }
};

export function verificationView(state: VerificationState): VerificationView {
  return VIEWS[state];
}

export function resolveVerificationResult(
  input: VerificationResultInput
): VerificationView {
  if (input.ok) {
    return verificationView(
      input.alreadyVerified ? "already-verified" : "success"
    );
  }

  if (input.status === 429) {
    return verificationView("rate-limited");
  }

  if (
    input.code === "VERIFICATION_TOKEN_ALREADY_USED" ||
    input.code === "EMAIL_ALREADY_VERIFIED"
  ) {
    return verificationView("already-verified");
  }

  switch (input.code) {
    case "VERIFICATION_TOKEN_EXPIRED":
      return verificationView("expired");
    case "VERIFICATION_TOKEN_REVOKED":
      return verificationView("revoked");
    case "VERIFICATION_TOKEN_INVALID":
    case "VALIDATION_FAILED":
      return verificationView("invalid");
    default:
      return verificationView("error");
  }
}

export type ResendState = "idle" | "loading" | "sent" | "error";

export interface ResendView {
  state: ResendState;
  message: string;
}

export function resendView(
  state: ResendState,
  status?: number
): ResendView {
  if (state === "sent") {
    return {
      state,
      message:
        "If an account exists that still needs verification, a new link has been sent."
    };
  }

  if (state === "error") {
    return {
      state,
      message:
        status === 429
          ? "Too many requests. Please wait a moment before trying again."
          : "We could not send a new link right now. Please try again shortly."
    };
  }

  return {
    state,
    message:
      state === "loading"
        ? "Sending a new verification link…"
        : "Did the email not arrive? Request a fresh link below."
  };
}
