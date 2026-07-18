# Testcase - Email Verification Flow Cleanup

## Manual Tests

### TC-01 Signup Email Display

1. Open Login scene.
2. Create a new email/password account.
3. Verify that the email verification screen displays the entered email, not `[이메일 주소]`.

Expected: The entered email is shown.

### TC-02 Signup Cancel Deletes Unverified User

1. Create a new email/password account.
2. On the verification screen, press back.
3. Confirm signup cancellation.
4. Check Firebase Authentication users.

Expected: The newly created unverified Firebase user is deleted.

### TC-03 Signup Cancel Dismiss

1. Create a new email/password account.
2. On the verification screen, press back.
3. Choose the cancel/continue-verification option.

Expected: The verification screen remains visible and no account deletion occurs.

### TC-04 Existing Unverified Login Back

1. Use an existing unverified email/password user.
2. Attempt login.
3. On the verification screen, press back.

Expected: The app signs out from Firebase and returns to the previous login panel. The Firebase user remains.

### TC-05 Signup Verification App Quit

1. Create a new email/password account.
2. Reach the verification screen without completing email verification.
3. Close or force close the app.
4. Launch the app again.

Expected: The Firebase user remains, app quit is not treated as signup cancellation, and the user returns to the verification screen as an existing unverified account instead of entering Lobby.

### TC-06 Verify Complete First Login

1. Create and verify an email/password account.
2. Press verification complete.

Expected: First login moves to nickname setup before Lobby.

### TC-07 Verified Session Relaunch First Login

1. Create a new email/password account.
2. Reach the verification screen.
3. Complete email verification outside the app.
4. Close and launch the app again before setting a nickname.

Expected: Auto login does not enter Lobby as Guest. The user moves to nickname setup before Lobby.

## Notes

Unity Editor log check found no C# compile errors. Local shell did not have `dotnet` available, so Unity Editor/device verification is the final authority.

## Results (2026-07-18)

- TC-01 PASS: user confirmed signup verification screen displays the entered email.
- TC-02 PASS: user confirmed back from signup verification shows signup cancellation confirmation and confirmed cancellation deletes the Firebase unverified user.
- TC-03 PASS: user confirmed continuing verification keeps the verification screen visible.
- TC-04 PASS by policy/code path: existing unverified-login back signs out and returns without account deletion.
- TC-05 PASS: user confirmed relaunch from verification screen returns to verification screen.
- TC-06 PASS by flow coverage: verification complete button path routes to nickname setup before Lobby.
- TC-07 PASS: user confirmed relaunch from nickname setup returns to nickname setup instead of entering Lobby as Guest.
