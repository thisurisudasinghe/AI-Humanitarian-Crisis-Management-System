import {
  ApiError,
  apiJson,
  type EmailVerificationResponse
} from "@/lib/api";
import {
  resolveVerificationResult,
  resendView,
  verificationView,
  type ResendView,
  type VerificationView
} from "@/lib/verification-state";

/** Redeems a one-time link. The token is sent in a POST body, never in a log. */
export async function verifyEmailToken(token: string): Promise<VerificationView> {
  if (!token) {
    return verificationView("invalid");
  }

  try {
    const response = await apiJson<EmailVerificationResponse>(
      "/api/v1/auth/email/verify",
      "POST",
      { token }
    );

    return resolveVerificationResult({
      ok: true,
      status: 200,
      alreadyVerified: response.alreadyVerified
    });
  } catch (caught) {
    if (caught instanceof ApiError) {
      return resolveVerificationResult({
        ok: false,
        status: caught.status,
        code: caught.code
      });
    }

    return verificationView("error");
  }
}

export async function resendVerificationEmail(
  email: string
): Promise<ResendView> {
  try {
    await apiJson<EmailVerificationResponse>(
      "/api/v1/auth/email/resend",
      "POST",
      { email }
    );
    return resendView("sent");
  } catch (caught) {
    const status = caught instanceof ApiError ? caught.status : undefined;
    return resendView("error", status);
  }
}
