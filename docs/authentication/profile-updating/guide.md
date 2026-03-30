# Updating Your Profile

## Overview

The Profile Updating feature lets you change the name and email address associated with your Orchestra account at any time. If you also want to change your password, you can do that in the same save action — no separate workflow required.

Your identity is tied to your JWT session token, so only your own profile can be modified; you cannot update another user's account. All changes take effect immediately and the updated profile is returned to the application as soon as you save.

## Prerequisites

- You must be logged in to Orchestra. If your session has expired, [log in again](../log-in/guide.md) before proceeding.
- The new email address you want to use must not already be registered to another account.
- If you are changing your password, you must know your current password.

## Step-by-Step Guide

### Step 1: Open the Profile modal

From anywhere inside the Orchestra application, open the Profile modal. Your current display name and email address are pre-filled in the form so you can see exactly what is stored today.

<!-- IMAGE_PLACEHOLDER: The Orchestra main application interface with the Profile modal open, showing the name and email fields pre-populated with the user's current values -->

### Step 2: Update your name and/or email

Edit the **Name** field, the **Email** field, or both. Both fields must contain a value — you cannot save an empty name or email.

The email you enter will be stored in lowercase regardless of how you type it.

<!-- IMAGE_PLACEHOLDER: The name and email fields in the Profile modal with edited values and both fields containing valid, non-empty input -->

### Step 3: Change your password (optional)

If you want to change your password, fill in all three password fields:

1. **Current password** — your existing password, to confirm your identity.
2. **New password** — must be 8–128 characters and include at least one uppercase letter, one lowercase letter, one digit, and one special character.
3. **Confirm new password** — must exactly match the new password you entered above.

If you do not want to change your password, leave all three fields blank.

<!-- IMAGE_PLACEHOLDER: The Profile modal with the three password fields (Current password, New password, Confirm new password) filled in -->

### Step 4: Save your changes

Click **Save**. Orchestra validates your input locally before making any server call:

- Name and email are checked to be non-empty.
- If any password field is filled, all three password fields are checked, the new password is verified against the strength rules, and the new and confirmation passwords are confirmed to match.

If local validation passes, Orchestra applies your changes. A profile update is always sent first; a password change is only sent if the profile update succeeds.

<!-- IMAGE_PLACEHOLDER: The Save button highlighted in the Profile modal, ready to be clicked -->

## What to Expect

Once your changes are saved successfully, the Profile modal closes and you are returned to the application with your updated details applied. Your existing login session remains active — a password change does **not** sign you out or invalidate your current token.

<!-- IMAGE_PLACEHOLDER: The Orchestra application after a successful profile save, with the modal closed and the updated name visible in the navigation or header area -->

## Important Notes

- **Both name and email are required.** Clearing either field will prevent the form from submitting.
- **Email uniqueness is enforced.** If the email you entered is already used by a different account, the save will fail.
- **Email is stored in lowercase.** Regardless of how you type your email, it is normalised before being saved.
- **Changing your password does not end your session.** Your current token stays valid after a password change.
- **All three password fields must be filled to change your password.** Filling in only one or two fields will block the save.
- **Password changes require your current password.** The server verifies your existing credential before accepting the new one.
- **New password strength rules match registration.** Your new password must be 8–128 characters and contain at least one uppercase letter, one lowercase letter, one digit, and one special character.
- **Profile update and password change happen in sequence.** If the profile update fails, the password change is not attempted.

## Troubleshooting

| Problem | Cause | Solution |
|---------|-------|----------|
| "Name and email are required" error shown | One or both fields were left empty | Re-enter a value in the name and/or email field before saving |
| "Email is already in use" error shown | The new email address is registered to a different account | Use a different email address |
| "Invalid credentials" error shown after filling in password fields | The current password you entered is incorrect | Re-enter your correct current password |
| "New passwords do not match" error shown | The new password and the confirmation field contain different values | Retype both the new password and the confirmation to make sure they match |
| Password strength error shown | The new password does not meet the strength requirements | Choose a password that is 8–128 characters with at least one uppercase letter, one lowercase letter, one digit, and one special character |
| Modal stays open after clicking Save | One of the two server operations (profile update or password change) returned an error | Read the error message displayed inside the modal and follow its guidance |
| "Unauthorized" error shown | Your session has expired or you are not logged in | Log in again and retry your profile update |
