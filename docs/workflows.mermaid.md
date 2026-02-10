# BookingScheduleSystem - System Workflows

## User Registration Flow with OTP Verification

```mermaid
sequenceDiagram
    actor User
    participant WebUI as Web UI<br/>(Blazor Server)
    participant OtpWebSvc as OtpService<br/>(Web)
    participant API as API Server<br/>(FastEndpoints)
    participant OtpApiSvc as OtpService<br/>(API)
    participant NotifSvc as OtpNotificationService
    participant DB as PostgreSQL<br/>(Marten)

    %% Step 1: Personal Info
    Note over User,WebUI: Step 1: Personal Information
    User->>WebUI: Enter name, select Email/Phone
    WebUI->>WebUI: Validate input

    %% Step 2: Send OTP
    Note over User,NotifSvc: Step 2: OTP Request & Send
    User->>WebUI: Click "Send OTP"
    WebUI->>OtpWebSvc: SendOtpAsync(contact, purpose)
    OtpWebSvc->>API: POST /api/auth/send-otp
    API->>OtpApiSvc: GenerateOtp(identifier)
    OtpApiSvc->>OtpApiSvc: Generate 6-digit code<br/>Set 10min expiry<br/>Store in memory
    OtpApiSvc->>NotifSvc: SendEmailOtp/SendSmsOtp
    NotifSvc->>NotifSvc: Log to console<br/>(Production: SendGrid/Twilio)
    NotifSvc-->>User: OTP Code (via Email/SMS)
    NotifSvc-->>API: Success
    API-->>OtpWebSvc: OtpResponse(expiresAt)
    OtpWebSvc-->>WebUI: Success
    WebUI->>WebUI: Start countdown timer
    WebUI-->>User: Show OTP input field

    %% Step 3: Verify OTP
    Note over User,OtpApiSvc: Step 3: OTP Verification
    User->>WebUI: Enter 6-digit OTP code
    WebUI->>OtpWebSvc: VerifyOtpAsync(contact, code)
    OtpWebSvc->>API: POST /api/auth/verify-otp
    API->>OtpApiSvc: VerifyOtp(identifier, code)

    alt Valid OTP
        OtpApiSvc->>OtpApiSvc: Check: not expired<br/>attempts < 5<br/>code matches
        OtpApiSvc->>OtpApiSvc: Mark as verified<br/>Generate token
        OtpApiSvc-->>API: (true, verificationToken)
        API-->>WebUI: Success + token
        WebUI->>WebUI: Move to Terms step
    else Invalid OTP
        OtpApiSvc->>OtpApiSvc: Increment attempt count
        OtpApiSvc-->>API: (false, null)
        API-->>WebUI: Error: Invalid OTP
        WebUI-->>User: Show error
    else Max Attempts Exceeded
        OtpApiSvc-->>API: Error: Max attempts
        API-->>WebUI: Error: Request new OTP
        WebUI-->>User: Show error + Resend
    end

    %% Step 4: Terms & Registration
    Note over User,DB: Step 4: Terms & Final Registration
    User->>WebUI: Accept Terms & Conditions
    User->>WebUI: Click "Complete Registration"
    WebUI->>API: POST /api/auth/register<br/>(name, contact, token)
    API->>OtpApiSvc: Validate verification token

    alt Token Valid
        API->>DB: Check if user exists
        alt User exists
            API-->>WebUI: Error: User exists
        else New user
            API->>DB: Insert User document
            DB-->>API: Success
            API-->>WebUI: RegisterUserResponse(userId)
            WebUI->>WebUI: Navigate to Login/Dashboard
            WebUI-->>User: Registration successful!
        end
    else Token Invalid
        API-->>WebUI: Error: Invalid token
        WebUI-->>User: Show error
    end
```

## System Architecture Overview

```mermaid
graph TB
    subgraph "Client Layer"
        Browser[Web Browser]
    end

    subgraph "Web Application - BookingScheduleSystem.Web"
        BlazorApp[Blazor Server App<br/>Port: 5288]
        WebServices[Services]
        OtpWebService[OtpService]
        WebServices --> OtpWebService
    end

    subgraph "API Layer - BookingScheduleSystem.Api"
        APIServer[API Server<br/>Port: 5059]

        subgraph "FastEndpoints"
            SendOtpEP[/api/auth/send-otp]
            VerifyOtpEP[/api/auth/verify-otp]
            RegisterEP[/api/auth/register]
        end

        subgraph "Infrastructure Services"
            OtpSvc[OtpService<br/>In-Memory Store]
            NotifSvc[OtpNotificationService<br/>Email/SMS]
        end

        APIServer --> SendOtpEP
        APIServer --> VerifyOtpEP
        APIServer --> RegisterEP
        SendOtpEP --> OtpSvc
        VerifyOtpEP --> OtpSvc
        OtpSvc --> NotifSvc
    end

    subgraph "Data Layer"
        PostgreSQL[(PostgreSQL<br/>Database<br/>Port: 5432)]
        Marten[Marten ORM<br/>Document Store]
        PostgreSQL --> Marten
    end

    subgraph "Infrastructure"
        Docker[Docker Container<br/>bookingschedule_postgres]
        Docker --> PostgreSQL
    end

    Browser <-->|HTTP| BlazorApp
    BlazorApp <-->|REST API| APIServer
    APIServer <-->|Npgsql| Marten
    NotifSvc -.->|Future: SendGrid| Email[Email Service]
    NotifSvc -.->|Future: Twilio| SMS[SMS Service]

    style OtpSvc fill:#e1f5ff
    style OtpWebService fill:#e1f5ff
    style NotifSvc fill:#fff4e1
    style PostgreSQL fill:#336791,color:#fff
    style Docker fill:#0db7ed,color:#fff
```

## OTP Service Internal State Management

```mermaid
stateDiagram-v2
    [*] --> OtpGenerated: User requests OTP

    OtpGenerated --> Verifying: User enters code
    OtpGenerated --> Expired: 10 minutes elapsed

    Verifying --> Verified: Valid code entered
    Verifying --> InvalidAttempt: Wrong code

    InvalidAttempt --> Verifying: Retry (attempts < 5)
    InvalidAttempt --> MaxAttemptsExceeded: 5 failed attempts

    Expired --> [*]: User must request new OTP
    MaxAttemptsExceeded --> [*]: User must request new OTP
    Verified --> Registered: Complete registration
    Registered --> [*]

    note right of OtpGenerated
        State stored in ConcurrentDictionary
        - Code: 6-digit random number
        - ExpiresAt: DateTime + 10min
        - Attempts: 0
        - IsVerified: false
    end note

    note right of Verified
        - IsVerified: true
        - VerificationToken: GUID
        Token used in registration
    end note
```

## Data Flow - OTP Storage Structure

```mermaid
graph LR
    subgraph "In-Memory OTP Store (ConcurrentDictionary)"
        Key["Key: Identifier<br/>(email or phone)"]

        subgraph "OtpEntry Value"
            Code[Code: string<br/>6-digit number]
            Purpose[Purpose: string<br/>registration/login]
            ExpiresAt[ExpiresAt: DateTime<br/>Now + 10 minutes]
            Attempts[Attempts: int<br/>0 to 5 max]
            IsVerified[IsVerified: bool<br/>false until verified]
            VerifiedAt[VerifiedAt: DateTime?<br/>null until verified]
            VerificationToken[VerificationToken: string?<br/>GUID when verified]
        end
    end

    Key --> Code
    Key --> Purpose
    Key --> ExpiresAt
    Key --> Attempts
    Key --> IsVerified
    Key --> VerifiedAt
    Key --> VerificationToken

    style Code fill:#e1f5ff
    style IsVerified fill:#c8e6c9
    style VerificationToken fill:#fff9c4
```

## Registration Flow - Decision Tree

```mermaid
graph TD
    Start([User starts registration])
    Start --> SelectContact{Select contact<br/>method}

    SelectContact -->|Email| EnterEmail[Enter email address]
    SelectContact -->|Phone| EnterPhone[Enter phone number]

    EnterEmail --> SendOTP[Send OTP request]
    EnterPhone --> SendOTP

    SendOTP --> CheckGenerate{Can generate<br/>OTP?}

    CheckGenerate -->|Yes| GenerateCode[Generate 6-digit code<br/>Set 10min expiry]
    CheckGenerate -->|No: Rate limit| ErrorTooMany[Error: Too many requests]

    GenerateCode --> SendNotif[Send via Email/SMS]
    SendNotif --> ShowInput[Show OTP input field<br/>Start countdown]

    ShowInput --> UserEnters{User enters<br/>OTP code}

    UserEnters -->|Submit| ValidateOTP{Valid OTP?}
    UserEnters -->|Resend| SendOTP

    ValidateOTP -->|Yes| MarkVerified[Mark as verified<br/>Generate token]
    ValidateOTP -->|No| IncrementAttempt[Increment attempt count]

    IncrementAttempt --> CheckAttempts{Attempts < 5?}
    CheckAttempts -->|Yes| ShowError1[Show error message]
    CheckAttempts -->|No| LockOut[Lock out OTP<br/>Must request new]

    ShowError1 --> ShowInput
    LockOut --> ShowInput

    MarkVerified --> ShowTerms[Show Terms & Conditions]
    ShowTerms --> UserAccepts{User accepts<br/>terms?}

    UserAccepts -->|Yes| SubmitReg[Submit registration]
    UserAccepts -->|No| ShowTerms

    SubmitReg --> ValidateToken{Verification<br/>token valid?}

    ValidateToken -->|No| ErrorInvalid[Error: Invalid token]
    ValidateToken -->|Yes| CheckExists{User already<br/>exists?}

    CheckExists -->|Yes| ErrorExists[Error: User exists]
    CheckExists -->|No| SaveDB[Save to database]

    SaveDB --> Success([Registration complete!])

    ErrorTooMany --> End([End])
    ErrorInvalid --> End
    ErrorExists --> End

    style Start fill:#c8e6c9
    style Success fill:#c8e6c9
    style ErrorTooMany fill:#ffcdd2
    style ErrorInvalid fill:#ffcdd2
    style ErrorExists fill:#ffcdd2
    style LockOut fill:#ffcdd2
    style MarkVerified fill:#bbdefb
    style SaveDB fill:#bbdefb
```

## Technology Stack

```mermaid
graph LR
    subgraph "Frontend"
        Blazor[Blazor Server<br/>.NET 8]
        MudBlazor[MudBlazor<br/>UI Components]
        Blazor --> MudBlazor
    end

    subgraph "Backend API"
        FastEndpoints[FastEndpoints<br/>Minimal API]
        FluentVal[FluentValidation<br/>Request Validation]
        Serilog[Serilog<br/>Logging]
        FastEndpoints --> FluentVal
        FastEndpoints --> Serilog
    end

    subgraph "Data Access"
        Marten[Marten<br/>Document DB ORM]
        Npgsql[Npgsql<br/>PostgreSQL Driver]
        Marten --> Npgsql
    end

    subgraph "Infrastructure"
        Docker[Docker<br/>Containerization]
        PostgreSQL[PostgreSQL 16<br/>Database]
        Docker --> PostgreSQL
    end

    subgraph "Security"
        OTPSystem[OTP System<br/>In-Memory]
        JWT[JWT Tokens<br/>Future Auth]
    end

    Blazor <--> FastEndpoints
    FastEndpoints <--> Marten
    Marten <--> PostgreSQL
    FastEndpoints --> OTPSystem

    style Blazor fill:#512bd4,color:#fff
    style MudBlazor fill:#594ae2,color:#fff
    style FastEndpoints fill:#00aa00,color:#fff
    style Marten fill:#2e7d32,color:#fff
    style PostgreSQL fill:#336791,color:#fff
    style Docker fill:#0db7ed,color:#fff
    style OTPSystem fill:#ff9800,color:#fff
```

## Future Enhancements

```mermaid
mindmap
  root((OTP System<br/>Enhancements))
    Production Ready
      Redis for OTP storage
      Rate limiting per IP
      SendGrid integration
      Twilio SMS integration
      Monitoring & alerts
    Security
      2FA for existing users
      Biometric options
      Device fingerprinting
      Suspicious activity detection
    UX Improvements
      Auto-fill OTP from SMS
      Remember device option
      Multi-language support
      Accessibility enhancements
    Analytics
      Registration funnel tracking
      OTP success rates
      Drop-off analysis
      Performance metrics
```

---

## Notes

### Current Implementation (Development)
- **OTP Storage**: In-memory `ConcurrentDictionary` (not production-ready)
- **Notifications**: Console logging only
- **Security**: Basic validation (10min expiry, 5 max attempts)

### Production Recommendations
1. **OTP Storage**: Migrate to Redis with TTL
2. **Email**: Integrate SendGrid with templates
3. **SMS**: Integrate Twilio or Semaphore SMS
4. **Rate Limiting**: Add per-IP and per-user limits
5. **Monitoring**: Add logging for security events
6. **Backup**: Secondary verification method

### Key Security Features Implemented
✅ Time-limited OTP (10 minutes)
✅ Maximum attempt limit (5 attempts)
✅ Verification token for registration
✅ Purpose-specific OTP codes
✅ Secure random number generation

### Next Steps
1. Test complete registration flow
2. Add email/SMS provider integration
3. Implement user login with OTP
4. Add password-based login option
5. Build dashboard for authenticated users
