# Contract Proposals

## TL;DR

This directory holds **contract proposals** from both the Backend and Frontend agents.  
Before adding or modifying any shared DTO/contract, place a proposal here so the other agent is aware.

## Process

1. Create a `.md` file named after your feature (e.g., `upgrade-subscription.md`)
2. Describe the new/modified contracts, fields, and reasoning
3. Note any breaking changes
4. Then create/modify the actual contract files
5. Delete the proposal file once both agents have consumed the change

## Why

This prevents merge conflicts and ensures both API endpoints and UI clients stay aligned on the same contract shapes.
