# Log In

## Overview

The Log In feature lets you return to Orchestra using your registered email address and password. When you sign in successfully, Orchestra verifies your identity, records your login, and issues a secure access token that keeps you authenticated as you work across workspaces, agents, tools, and tickets.

If you have not created an account yet, see the [Sign Up guide](../sign-up/guide.md) first.

## Prerequisites

- You must have a registered Orchestra account. If you do not have one, [sign up](../sign-up/guide.md) before continuing.
- You need the email address and password you used when you created your account.

## Step-by-Step Guide

### Step 1: Open the Login form

Navigate to the Orchestra application URL in your browser. The landing screen shows a combined Login / Register form. Make sure the **Login** mode is selected.

<!-- IMAGE_PLACEHOLDER: The Orchestra login screen with the Login tab active, showing the email and password fields and the submit button -->

### Step 2: Enter your email address

Type your registered email address into the **Email** field.

<!-- IMAGE_PLACEHOLDER: Close-up of the email field filled in with a placeholder email address -->

### Step 3: Enter your password

Type your password into the **Password** field.

<!-- IMAGE_PLACEHOLDER: Close-up of the password field with obscured input -->

### Step 4: Submit the form

Click the **Log In** button (or press **Enter**). Orchestra will verify your credentials and, if they are correct, sign you in and take you directly into the application.

<!-- IMAGE_PLACEHOLDER: The login button highlighted, ready to be clicked -->

## What to Expect

After a successful login, Orchestra navigates you to the main application view. Your session is active and your access token is stored locally in the browser, so you will stay signed in as you move between modules. Your session expires after 60 minutes of inactivity, at which point you will be asked to log in again.

<!-- IMAGE_PLACEHOLDER: The Orchestra main application view shown immediately after a successful login -->

## Important Notes

- **Both fields are required.** You cannot submit the form if either the email or the password field is empty — the form will show an inline error before making any request to the server.
- **Your session lasts up to 60 minutes.** After that, your access token expires and you will need to sign in again.
- **Repeated failed attempts are rate-limited.** If you (or anyone else) tries to log in from your browser more than 5 times within a 60-second window, further attempts will be blocked temporarily. Wait until the indicated time has passed, then try again.
- **Error messages are intentionally generic.** Whether the email is not recognised or the password is wrong, you will see the same "Invalid credentials" message. This is by design to protect the privacy of registered accounts.

## Troubleshooting

| Problem | Cause | Solution |
|---------|-------|----------|
| "Please fill in all required fields" appears on the form | You submitted the form with the email or password field blank | Fill in both fields and try again |
| "Invalid credentials" error after submitting | The email address is not registered, or the password does not match the account | Double-check your email address for typos; reset your password if you have forgotten it |
| "Too many requests" error and the form is disabled | You have exceeded 5 login attempts within 60 seconds | Wait until the time shown in the error message has elapsed, then try again |
| You are redirected back to the login screen after a short time | Your 60-minute session token has expired | Sign in again to start a new session |
