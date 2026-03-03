# Contract Proposal: Chatbot User First Login
## Agent: Backend
## Date: 2026-03-03
## New/Modified Contracts:
- `PasswordSetupRequiredResponse` (NEW) — `RequiresPasswordSetup` (bool, always true), `ContactMethod` ("email" or "phone"), `MaskedContact` (masked contact string)
- `RegisterUserRequest` (MODIFIED) — Added `IsPasswordTemporary` (bool) field
## Reason: Chatbot-registered users get a system-generated password they never see. The login endpoint needs to return a distinct response (HTTP 428) so the frontend can redirect to the OTP + set-password flow. The `IsPasswordTemporary` flag on `RegisterUserRequest` lets the chatbot registration flow mark users accordingly.
## Breaking Changes: None — `IsPasswordTemporary` defaults to `false`, `PasswordSetupRequiredResponse` is a new type.
## Frontend Coordination:
- Frontend must set `IsPasswordTemporary = true` in `ChatbotToolExecutor.cs` when registering users via chatbot
- Login page must handle HTTP 428 response, parse `PasswordSetupRequiredResponse`, and redirect to OTP -> set password flow
