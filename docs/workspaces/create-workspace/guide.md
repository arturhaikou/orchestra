# Create a Workspace

## Overview

A workspace is the container for everything you do in Orchestra. Creating one is the first meaningful step after setting up your account — your agents, integrations, tools, and tickets all live inside it.

When you create a workspace you can keep things simple by providing just a name, or you can immediately enable AI-powered features: **AI Summarization** (auto-summarises incoming tickets) and **Customer Satisfaction Analysis** (evaluates sentiment on resolved tickets). Both are optional and can be configured at any point after creation as well.

## Prerequisites

- You must have an Orchestra account and be logged in.
- If you are enabling an AI feature, Orchestra must have an AI provider (Azure OpenAI or Ollama) configured by your platform administrator. Without a reachable AI provider, creation with AI features will not succeed.

## Step-by-Step Guide

### Step 1: Open the Create Workspace modal

**New accounts:** After registering for the first time, Orchestra brings you directly into the application and automatically presents the **Create Workspace** modal. This modal cannot be dismissed — creating a workspace is required before you can use any other part of the platform.

**Existing accounts:** Navigate to the workspace management area and choose the option to create a new workspace.

<!-- IMAGE_PLACEHOLDER: The Create Workspace modal open on a fresh account, showing the workspace name field and two disabled AI feature toggles, with no close/dismiss button visible -->

### Step 2: Enter a workspace name

Type a name for your workspace in the **Name** field. The name must be between 2 and 100 characters. Leading and trailing spaces are trimmed automatically.

<!-- IMAGE_PLACEHOLDER: The Name field filled in with an example workspace name such as "Support Team" -->

### Step 3: (Optional) Enable AI features

Toggle on **AI Summarization**, **Customer Satisfaction Analysis**, or both if you want AI assistance from day one.

The first time you enable either toggle, Orchestra fetches the list of available AI models from the platform's provider and pre-selects the default model for you. If both features are enabled, the same default is applied to each simultaneously. You can change the model selection for either feature independently using the dropdown that appears.

<!-- IMAGE_PLACEHOLDER: The AI Summarization and Customer Satisfaction Analysis toggles both switched on, each showing a model selection dropdown with a model pre-selected -->

> If the AI model list fails to load, an inline error message appears beneath the toggle. The **Create Workspace** button is disabled until you either resolve the error or turn the toggle back off.

<!-- IMAGE_PLACEHOLDER: An AI feature toggle switched on with an inline error message indicating that models could not be loaded, and the Create Workspace button visibly greyed out -->

### Step 4: Submit the form

Click **Create Workspace**. Orchestra validates your name and, if you enabled AI features, verifies the selected model identifiers against the live AI provider catalogue before saving anything.

<!-- IMAGE_PLACEHOLDER: The completed form with a workspace name entered and AI features configured, with the Create Workspace button active and ready to click -->

## What to Expect

Your new workspace is created immediately and you are taken into it. From this point you can start adding agents, configuring integrations, and managing tickets — all scoped to your new workspace.

<!-- IMAGE_PLACEHOLDER: The Orchestra dashboard after successful workspace creation, showing the new workspace name in the navigation and an empty state ready for further configuration -->

## Important Notes

- The workspace name must be between **2 and 100 characters** after trimming whitespace. Blank names and single-character names are not accepted.
- **AI features are entirely optional** at creation time — you can enable them later through workspace settings.
- If you enable an AI feature, you must select a valid model from the loaded list before submitting. The form will not let you submit with an AI feature toggled on but no model loaded.
- **You cannot dismiss the modal on a new account.** Until at least one workspace exists, the modal stays open. Workspace creation is mandatory to access the rest of Orchestra.
- All invalid AI model identifiers are reported together in a single error response, so you can correct them all at once.

## Troubleshooting

| Problem | Cause | Solution |
|---------|-------|----------|
| "Create Workspace" button is disabled even though a name is entered | An AI feature is toggled on and the model list failed to load | Check the inline error beneath the AI toggle. You can turn the toggle off to re-enable the button and create the workspace without that AI feature, or wait until the model list loads successfully. |
| Error after submitting: invalid model identifier | The selected AI model no longer exists in the provider's catalogue | Open the model dropdown and choose a currently available model, then resubmit. |
| Error after submitting: workspace name is invalid | The name is empty, whitespace-only, shorter than 2 characters, or longer than 100 characters | Correct the name and resubmit. |
| Error after submitting: AI provider could not be reached | The platform's AI service is unreachable or misconfigured | Contact your platform administrator to verify the AI provider configuration. As a workaround, turn off all AI feature toggles and create the workspace without AI features for now. |
| Modal won't close | You have no existing workspaces yet | Complete the form and create your first workspace — this is required before you can access the rest of Orchestra. |
