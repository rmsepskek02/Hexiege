# Research - Email Verification Flow Cleanup

## Context

Lobby profile/ranking cloud bridge work exposed two email flow gaps:

- Email verification view displayed a placeholder/stale email because login panels are hidden with `CanvasGroup`, so `EmailVerifyView.OnEnable()` is not a reliable refresh point.
- Firebase creates the email/password user immediately when `CreateUserWithEmailAndPasswordAsync()` succeeds. Sending the verification email is not a temporary pre-registration state.

## Findings

- `SignUpView` and `EmailLoginView` both moved to `LoginRootView.ShowEmailVerify()` without passing the attempted email or the reason for entering the verification screen.
- `EmailVerifyView.OnEnable()` read `_loginUseCase.Email`, but the panel object remains active while visibility is controlled by alpha/raycast/interactable values.
- New email signup and existing unverified email login need different back behavior:
  - New signup pending verification: user can cancel signup, and the current unverified Firebase user should be deleted.
  - Existing unverified login: user should be signed out and returned to the previous login screen, but the account should not be deleted.

## Decision

Introduce an explicit email verification origin:

- `SignUpPending`
- `ExistingUnverifiedLogin`

`LoginRootView.ShowEmailVerify(email, origin)` prepares the verification view before switching panels.

For signup cancellation, call a guarded use case method that deletes only the current unverified Firebase user.
