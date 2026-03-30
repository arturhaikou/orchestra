# Sign Up

## Overview

The Sign Up feature lets you create a new Orchestra account in a single step. You provide your name, email address, and a password, and Orchestra registers your account and signs you in automatically — no separate login required. Once you complete registration, you land directly inside the application and can start building workspaces, configuring agents, and managing tickets right away.

## Prerequisites

- You do not yet have an Orchestra account associated with the email address you plan to use.
- You have access to the Orchestra login screen in a supported browser.

## Step-by-Step Guide

### Step 1: Open the registration form

On the Orchestra login screen, click the **Create Account** toggle. The form switches into registration mode and reveals three fields: **Email Address**, **Password**, and **Full Name**.

<!-- IMAGE_PLACEHOLDER: The Orchestra login screen with the "Create Account" toggle highlighted, showing the three registration fields (Email Address, Password, Full Name) visible -->

### Step 2: Enter your full name

Type your full name in the **Full Name** field. This is how you will be identified inside Orchestra.

### Step 3: Enter your email address

Type a valid email address in the **Email Address** field. This address must not already be registered to an existing account. Orchestra stores your email in lowercase, so capitalisation does not matter.

<!-- IMAGE_PLACEHOLDER: The registration form with the Email Address and Full Name fields filled in -->

### Step 4: Choose a password

Type your chosen password in the **Password** field. Your password must meet all of the following requirements:

- At least **8 characters** long
- No longer than **128 characters**
- Contains at least one **uppercase letter**
- Contains at least one **lowercase letter**
- Contains at least one **number**
- Contains at least one **special character** (e.g. `!`, `@`, `#`, `$`)

If your password does not meet a requirement, an inline error message will appear below the field telling you the first issue to fix. Address each message and continue until no error is shown.

<!-- IMAGE_PLACEHOLDER: The Password field showing an inline validation error message beneath it (e.g. "Password must contain at least one uppercase letter") -->

### Step 5: Submit the form

Once all three fields are filled in and no validation errors are shown, click the **Create Account** button to submit the form.

<!-- IMAGE_PLACEHOLDER: The completed registration form with all three fields filled in and the "Create Account" button ready to be clicked -->

### Step 6: Create your first workspace

Because every Orchestra activity happens inside a workspace, the application immediately opens the **Create Workspace** dialog. Fill in a workspace name and any optional AI settings, then confirm to finish setting up your account. You cannot skip this step — a workspace is required before you can use any other part of the platform.

<!-- IMAGE_PLACEHOLDER: The Create Workspace modal open on screen immediately after registration, prompting the user to name their first workspace -->

## What to Expect

After a successful registration, Orchestra creates your account, issues you a session token, and brings you into the application — there is no extra login step. Because your account is brand new, the application automatically opens the **Create Workspace** dialog so you can set up your first workspace before doing anything else. Once you create a workspace, you land on the main application view and can start working.

<!-- IMAGE_PLACEHOLDER: The Create Workspace modal appearing immediately after sign-up, with the rest of the application visible but inactive in the background -->

## Important Notes

- **Each email address is unique.** You cannot register with an email that is already linked to an existing account. If you have already signed up, use the login form instead.
- **All fields are required.** Leaving any field blank prevents the form from being submitted and displays an inline error.
- **Password errors appear one at a time.** The form shows only the first failing rule so you can fix issues one step at a time.
- **Your email is stored in lowercase.** `Alice@Example.com` and `alice@example.com` are treated as the same address.
- **Rate limiting applies.** You can submit the registration form up to 5 times within any 60-second window. If you exceed this limit, wait at least 60 seconds before trying again.
- **Your account is immediately active.** There is no email verification step — you can use Orchestra straight after registering.

## Troubleshooting

| Problem | Cause | Solution |
|---------|-------|----------|
| "Email already exists" error after submitting | The email address you entered is already registered to an account | Use a different email address, or go back to the login form if this account is yours |
| Inline password error keeps appearing | Your password does not satisfy one or more strength rules | Read the error message carefully and update your password to meet the stated requirement, then check for the next message |
| "Too many requests" error | You have submitted the form more than 5 times in the past 60 seconds | Wait at least 60 seconds, then try again |
| A required field error appears even though the field looks filled | The field may only contain spaces | Clear the field and type valid content |
