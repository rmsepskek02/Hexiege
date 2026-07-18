# Plan - Email Verification Flow Cleanup

## Goals

1. Show the actual signup/login email on the email verification screen.
2. Separate fresh signup verification from existing unverified-login verification.
3. Handle back/cancel from verification safely.
4. Document Firebase behavior: email users exist before verification completes.

## Implementation

- Add `EmailVerificationOrigin`.
- Add `EmailVerifyView.PrepareForShow(email, origin)`.
- Route signup success to `ShowEmailVerify(email, SignUpPending)`.
- Route existing unverified email login to `ShowEmailVerify(email, ExistingUnverifiedLogin)`.
- Let `LoginRootView.HandleBack()` delegate email verification back behavior to `EmailVerifyView`.
- Add `FirebaseAuthService.DeleteCurrentUserAsync()`.
- Add `LoginUseCase.DeleteCurrentUnverifiedEmailUserAsync()` with a guard against deleting verified users.

## Behavior

- Fresh signup -> verification screen:
  - Back asks for confirmation.
  - Confirm deletes the unverified Firebase user and returns to the previous panel.
  - Cancel stays on verification.
- Existing unverified login -> verification screen:
  - Back signs out and returns to the previous panel.
  - Account remains in Firebase.

## Non-goals

- Server-side cleanup of old `emailVerified=false` accounts is not implemented here.
- Email verification UI layout changes are not included in this slice.

## Completion Result (2026-07-18)

- `EmailVerificationOrigin` now separates fresh signup verification from existing unverified-login verification.
- The verification screen receives the attempted email explicitly through `ShowEmailVerify(email, origin)`.
- Fresh signup back/cancel confirmation deletes only the current unverified Firebase user when the user confirms signup cancellation.
- Existing unverified-login back signs out and returns to the previous login panel without deleting the Firebase account.
- App quit or force quit from the verification screen is not treated as signup cancellation. Relaunch returns to verification while the account remains unverified.
- Auto login now blocks Lobby entry for unverified email accounts and returns to verification.
- Auto login also blocks Lobby entry for verified email accounts that still have no Cloud Save nickname, returning to nickname setup instead.
- `SplashOverlay` fade behavior was corrected so Tap to Start only skips fade for Lobby scene transition; Login-scene panels such as verification and nickname setup fade the overlay out first.
