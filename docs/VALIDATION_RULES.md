# S2S Backend — Validation Rules Reference

> **Last Updated:** 2026-05-08
>
> This document lists **every validation rule** enforced by the backend so that the **Frontend** and **AI** teams can replicate them client-side.

---

## Table of Contents

1. [Global Constants](#1-global-constants)
2. [Authentication — Register](#2-register)
3. [Authentication — Login](#3-login)
4. [Authentication — Verify OTP](#4-verify-otp)
5. [Authentication — Forgot Password](#5-forgot-password)
6. [Authentication — Reset Password](#6-reset-password)
7. [Authentication — Change Password](#7-change-password)
8. [Profile — Update Profile](#8-update-profile)
9. [Firebase — Login with Firebase](#9-firebase-login)
10. [Firebase — Update FCM Token](#10-update-fcm-token)
11. [Translation — Sign to Text (Video Upload)](#11-sign-to-text-video-upload)
12. [Translation — Audio to Text (Audio Upload)](#12-audio-to-text-audio-upload)
13. [Translation — Text to Sign](#13-text-to-sign)
14. [Translation — Audio to Sign](#14-audio-to-sign)
15. [Text-to-Speech (TTS)](#15-text-to-speech)
16. [File Signature Validation (Magic Bytes)](#16-file-signature-validation)
17. [Enums Reference](#17-enums-reference)
18. [Password Regex (Copy-Paste Ready)](#18-password-regex)
19. [Change Email (2-Step)](#19-change-email)
20. [CSRF / XSRF Token (Web Clients)](#20-csrf--xsrf-token)
21. [Token Security Architecture](#21-token-security-architecture)
22. [Profile Image Upload](#22-profile-image-upload)
23. [Security Hardening (Audit 2026-05-08)](#23-security-hardening-audit-2026-05-08)
24. [Authentication Redirect & Open Redirect Protection](#24-authentication-redirect--open-redirect-protection)
25. [Email Normalization (Anti-Abuse)](#25-email-normalization-anti-abuse)

---

## 1. Global Constants

| Constant | Value |
|---|---|
| `MaxVideoSizeBytes` | **50 MB** (50 × 1024 × 1024) |
| `MaxAudioSizeBytes` | **20 MB** (20 × 1024 × 1024) |
| `OtpLength` | 6 digits |
| `OtpExpiryMinutes` | 10 min |
| `MaxOtpAttempts` | 3 |
| `ResendOtpCooldownSeconds` | 60 sec |
| `ResetTokenExpiryMinutes` | 30 min |
| `PasswordMinLength` | 8 chars |
| `PasswordMaxLength` | 100 chars |
| `PasswordHistoryLimit` | 5 (cannot reuse last 5 passwords) |
| `AccountLockoutMinutes` | 15 min |
| `MaxFailedAccessAttempts` | 3 |
| `AccessTokenExpiryMinutes` | 15 min |
| `RefreshTokenExpiryDays` | 7 days |
| `MaxEmailLength` | 256 chars |
| `MaxDisplayNameLength` | 50 chars |
| `MinUserNameLength` | 3 chars |
| `MaxUserNameLength` | 30 chars |
| `PhoneRegex` | `^01[0125]\d{8}$` (Egyptian only, 11 digits) |
| `MaxTranslationTextLength` | **200** chars |
| `MaxTtsTextLength` | 2000 chars |

> **Note:** All constants are centralized in `ApiConstants.cs` — change once, applies everywhere.

---

## 2. Register

**Endpoint:** `POST /api/v1/Account/register`  
**Content-Type:** `application/json`

| Field | Type | Required | Rules | Error Message |
|---|---|---|---|---|
| `Email` | `string` | ✅ | Not empty, valid email, max 256 chars, no forbidden chars | `"Email is required."` / `"A valid email is required."` / `"Email cannot exceed 256 characters."` / `"Email contains forbidden characters."` |
| `DisplayName` | `string` | ✅ | Not empty, max 50 chars | `"Display Name is required."` / `"Display Name cannot exceed 50 characters."` |
| `UserName` | `string` | ✅ | Not empty, min 3 chars, max 30 chars, regex `^[a-zA-Z0-9._-]+$` | `"Username is required."` / `"Username must be at least 3 characters long."` / `"Username cannot exceed 30 characters."` / `"Username can only contain letters, numbers, dots, hyphens, and underscores."` |
| `Password` | `string` | ✅ | Not empty, min 8 chars, max 100 chars, ≥1 uppercase, ≥1 lowercase, ≥1 digit, ≥1 special char, no HTML tags | See [Password Regex](#18-password-regex) |
| `PhoneNumber` | `string` | ✅ | Not empty, Egyptian number: `^01[0125]\d{8}$` | `"Phone number is required."` / `"Phone number must be a valid Egyptian number (e.g. 01XXXXXXXXX)."` |
| `DateOfBirth` | `DateOnly?` | ✅ | Not empty, age between **15–80** years | `"Date of birth is required."` / `"Age must be between 15 and 80 years."` |
| `UserType` | `enum` | ✅ | Not empty (see [Enums](#17-enums-reference)) | `"User Type is required."` |
| `UsesSignLanguage` | `bool` | ✅ | — | — |
| `SignLanguage` | `enum?` | Conditional | **Required** when `UsesSignLanguage == true` | `"Sign Language must be specified if 'Uses Sign Language' is true."` |

### Server-Side Business Validations (Register)

| Check | Error Code | Error Message |
|---|---|---|
| Sign language missing when `UsesSignLanguage=true` | `SignLanguage.Required` | `"Sign language is required when UsesSignLanguage is true"` |
| Phone number already exists | `DuplicatePhoneNumber` | `"Phone number is already in use."` |
| Email already exists | `DuplicateEmail` | `"Email is already in use."` |

---

## 3. Login

**Endpoint:** `POST /api/v1/Account/login`  
**Content-Type:** `application/json`

| Field | Type | Required | Rules | Error Message |
|---|---|---|---|---|
| `Email` | `string` | ✅ | Not empty, valid email, max 256 chars | `"Email is required."` / `"A valid email is required."` / `"Email cannot exceed 256 characters."` |
| `Password` | `string` | ✅ | Not empty, max 100 chars | `"Password is required."` / `"Password cannot exceed 100 characters."` |

### Server-Side Business Validations (Login)

| Check | Error Code | Error Message |
|---|---|---|
| User not found / wrong password | `User.InvalidCredentials` | `"Invalid Credentials"` |
| Account locked | `AccountLocked` | `"Account is locked. Try again in {X} minutes."` |
| Email not confirmed | `EmailNotConfirmed` | `"Please verify your email first."` |

---

## 4. Verify OTP

**Endpoint:** `POST /api/v1/Account/verify-otp`  
**Content-Type:** `application/json`

| Field | Type | Required | Rules | Error Message |
|---|---|---|---|---|
| `Email` | `string` | ✅ | Not empty, valid email, max 256 chars, no `<>` chars | `"Email is required"` / `"Invalid email format"` / `"Email cannot exceed 256 characters"` / `"Email contains forbidden characters."` |
| `Otp` | `string` | ✅ | Not empty, exactly 6 chars, regex `^\d{6}$` | `"OTP is required"` / `"OTP must be exactly 6 characters"` / `"OTP must contain only 6 digits"` |

### Server-Side Business Validations (Verify OTP)

| Check | Error Code | Error Message |
|---|---|---|
| User not found | `UserNotFound` | `"User not found."` |
| Already verified | `AlreadyVerified` | `"Email is already verified."` |
| No active OTP | `InvalidOtp` | `"No active verification code found."` |
| Max attempts reached | `MaxAttemptsReached` | `"Maximum attempts reached. Please request a new code."` |
| Wrong OTP code | `WrongOtp` | `"Invalid verification code. Remaining attempts: {N}"` |

---

## 5. Forgot Password

**Endpoint:** `POST /api/v1/Account/forgot-password`  
**Content-Type:** `application/json`

| Field | Type | Required | Rules | Error Message |
|---|---|---|---|---|
| `Email` | `string` | ✅ | Not empty, max 256 chars, valid email, regex `^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$`, no forbidden chars | See messages below |

**Error Messages:**
- `"Email is required."`
- `"Email cannot exceed 256 characters."`
- `"Please enter a valid email address."`
- `"Please enter a valid email format."`
- `"Email contains forbidden characters."` — forbidden chars: `< > & ' " \ / ; backtick`

> **Note:** The backend always returns **200 OK** even if the user is not found (to prevent email enumeration).

---

## 6. Reset Password

**Endpoint:** `POST /api/v1/Account/reset-password`  
**Content-Type:** `application/json`

| Field | Type | Required | Rules | Error Message |
|---|---|---|---|---|
| `Token` | `string` | ✅ | Not empty | `"Security token is required."` |
| `NewPassword` | `string` | ✅ | Not empty, min 8 chars, ≥1 uppercase, ≥1 lowercase, ≥1 digit, ≥1 special char, no `<>` HTML tags | See [Password Regex](#18-password-regex) + `"Password cannot contain HTML tags."` |
| `ConfirmPassword` | `string` | ✅ | Not empty, must equal `NewPassword` | `"Confirm password is required."` / `"Passwords do not match."` |

### Server-Side Business Validations (Reset Password)

| Check | Error Code | Error Message |
|---|---|---|
| Invalid/expired token | `InvalidToken` | `"Invalid or expired reset token."` |
| Account locked | `AccountLocked` | `"Account is locked. Try again in {X} minutes."` |

---

## 7. Change Password

**Endpoint:** `PUT /api/v1/Profile/change-password`  
**Content-Type:** `application/json`  
**Auth:** Bearer Token required

| Field | Type | Required | Rules | Error Message |
|---|---|---|---|---|
| `CurrentPassword` | `string` | ✅ | Not empty | `"Current password is required."` |
| `NewPassword` | `string` | ✅ | Not empty, min 8, max 100 chars, ≥1 upper, ≥1 lower, ≥1 digit, ≥1 special, no `<>`, must differ from `CurrentPassword` | See messages below |
| `ConfirmNewPassword` | `string` | ✅ | Not empty, must equal `NewPassword` | `"Confirm password is required."` / `"Passwords do not match."` |

**NewPassword Error Messages:**
- `"New password is required."`
- `"Password must be at least 8 characters."`
- `"Password cannot exceed 100 characters."`
- `"Password must contain at least one uppercase letter."`
- `"Password must contain at least one lowercase letter."`
- `"Password must contain at least one number."`
- `"Password must contain at least one special character."`
- `"Password cannot contain HTML tags."`
- `"New password must be different from the current password."`

### Server-Side Business Validations (Change Password)

| Check | Error Code | Error Message |
|---|---|---|
| Account locked | `AccountLocked` | `"Account is locked. Try again in {X} minutes."` |
| Wrong current password | `InvalidCurrentPassword` | `"Current password is incorrect."` |
| Reusing last 5 passwords | `PasswordPreviouslyUsed` | `"You cannot reuse any of your last 5 passwords."` |

---

## 8. Update Profile

**Endpoint:** `PUT /api/v1/Profile/update`  
**Content-Type:** `application/json`  
**Auth:** Bearer Token required

| Field | Type | Required | Rules | Error Message |
|---|---|---|---|---|
| `DisplayName` | `string` | ✅ | Not empty, max 50 chars | `"Display Name cannot be empty."` / `"Display Name cannot exceed 50 characters."` |
| `PhoneNumber` | `string?` | ❌ | If provided: Egyptian number `^01[0125]\d{8}$` | `"Phone number must be a valid Egyptian number (e.g. 01XXXXXXXXX)."` |

### Server-Side Business Validations (Update Profile)

| Check | Error Code | Error Message |
|---|---|---|
| Display name empty after trim | `DisplayName.Required` | `"Display name is required."` |
| Phone number already in use | `DuplicatePhoneNumber` | `"Phone number is already in use."` |

---

## 9. Firebase Login

**Endpoint:** `POST /api/v1/Account/firebase-login`  
**Content-Type:** `application/json`

| Field | Type | Required | Rules | Error Message |
|---|---|---|---|---|
| `IdToken` | `string` | ✅ | Valid Firebase ID token | Server validates via Firebase Admin SDK |

---

## 10. Update FCM Token

**Endpoint:** `PUT /api/v1/Profile/fcm-token`  
**Content-Type:** `application/json`  
**Auth:** Bearer Token required

| Field | Type | Required | Rules | Error Message |
|---|---|---|---|---|
| `FcmToken` | `string` | ✅ | Not empty | — |

---

## 11. Sign to Text (Video Upload)

**Endpoint:** `POST /api/v1/Translate/sign-to-text`  
**Content-Type:** `multipart/form-data`  
**Auth:** Bearer Token required

| Field | Form Name | Type | Required | Default | Rules |
|---|---|---|---|---|---|
| `VideoFile` | `video_file` | `File` | ✅ | — | See video validation below |
| `Language` | `language` | `string` | ❌ | `"ar"` | — |
| `IncludeAudio` | `include_audio` | `bool` | ❌ | `false` | — |

### Video File Validation

| Check | Error Code | Error Message |
|---|---|---|
| File is null or empty | `Video.Empty` | `"Video file is required."` |
| File size > **50 MB** | `Video.TooLarge` | `"Video file exceeds 50 MB."` |
| Invalid extension | `Video.InvalidFormat` | `"Unsupported video format."` |
| Invalid content type | `Video.InvalidContentType` | `"Unsupported video content type."` |
| Magic bytes mismatch | `Video.InvalidFormat` | `"Video signature does not match file type."` |

**Allowed Video Extensions:** `.mp4`, `.mov`, `.webm`, `.avi`, `.mkv`, `.m4v`

**Allowed Video Content Types:** `video/mp4`, `video/quicktime`, `video/webm`, `video/x-msvideo`, `video/x-matroska`, `video/x-m4v`

---

## 12. Audio to Text (Audio Upload)

**Endpoint:** `POST /api/v1/Translate/audio-to-text`  
**Content-Type:** `multipart/form-data`  
**Auth:** Bearer Token required

| Field | Form Name | Type | Required | Default | Rules |
|---|---|---|---|---|---|
| `AudioFile` | `audio_file` | `File` | ✅ | — | See audio validation below |
| `Language` | `language` | `string` | ❌ | `"ar"` | — |

### Audio File Validation

| Check | Error Code | Error Message |
|---|---|---|
| File is null or empty | `Audio.Empty` | `"Audio file is required."` |
| File size > **20 MB** | `Audio.TooLarge` | `"Audio file exceeds 20 MB."` |
| Invalid extension | `Audio.InvalidFormat` | `"Unsupported audio format."` |
| Invalid content type | `Audio.InvalidContentType` | `"Unsupported audio content type."` |
| Magic bytes mismatch | `Audio.InvalidFormat` | `"Unsupported audio format."` |

**Allowed Audio Extensions:** `.mp3`, `.wav`, `.m4a`, `.ogg`, `.webm`, `.mp4`, `.mpeg`

**Allowed Audio Content Types:** `audio/mpeg`, `audio/wav`, `audio/x-wav`, `audio/mp4`, `audio/m4a`, `audio/x-m4a`, `audio/ogg`, `audio/webm`, `video/mp4`, `video/webm`

---

## 13. Text to Sign

**Endpoint:** `POST /api/v1/Translate/text-to-sign`  
**Content-Type:** `multipart/form-data`  
**Auth:** Bearer Token required

| Field | Form Name | Type | Required | Default | Rules |
|---|---|---|---|---|---|
| `Text` | `text` | `string` | ✅ | — | Not empty, max **200** chars |
| `Avatar` | `avatar` | `string` | ❌ | `"default"` | Falls back to `"default"` if empty |
| `Speed` | `speed` | `string` | ❌ | `"1.0"` | Falls back to `"1.0"` if empty |
| `OutputFormat` | `output_format` | `string` | ❌ | `"pose"` | Falls back to `"pose"` if empty |

| Check | Error Code | Error Message |
|---|---|---|
| Text is empty/null | `Text.Empty` | `"Text is required for translation."` |
| Text > **200** chars | `Text.TooLong` | `"Text cannot exceed 200 characters."` |

---

## 14. Audio to Sign

**Endpoint:** `POST /api/v1/Translate/audio-to-sign`  
**Content-Type:** `multipart/form-data`  
**Auth:** Bearer Token required

| Field | Form Name | Type | Required | Default | Rules |
|---|---|---|---|---|---|
| `AudioFile` | `audio_file` | `File` | ✅ | — | Same as [Audio Validation](#audio-file-validation) |
| `Avatar` | `avatar` | `string` | ❌ | `"default"` | Falls back to `"default"` if empty |
| `Speed` | `speed` | `string` | ❌ | `"1.0"` | Falls back to `"1.0"` if empty |
| `OutputFormat` | `output_format` | `string` | ❌ | `"pose"` | Falls back to `"pose"` if empty |

> **Important:** Audio language is hardcoded to `"ar"` for this endpoint.

---

## 15. Text-to-Speech

> Used internally when `include_audio = true` in Sign-to-Text.

| Check | Error Code | Error Message |
|---|---|---|
| Text is empty | `Tts.EmptyText` | `"Text is required for speech synthesis."` |
| Text length > **2000** chars | `Tts.TextTooLong` | `"Text exceeds the maximum length for speech synthesis."` |

---

## 16. File Signature Validation

The backend validates files by reading the first **16 bytes** (magic bytes / file signature) and matching them against known patterns. **Extension alone is NOT enough.**

### Video Signatures

| Extension | Expected Signature |
|---|---|
| `.mp4`, `.m4v`, `.mov` | `ftyp` at bytes 4–7 (`0x66 0x74 0x79 0x70`) |
| `.webm`, `.mkv` | EBML header `0x1A 0x45 0xDF 0xA3` |
| `.avi` | RIFF header (`0x52 0x49 0x46 0x46`) + `AVI ` at bytes 8–11 |

### Audio Signatures

| Extension | Expected Signature |
|---|---|
| `.mp3` | ID3 tag `0x49 0x44 0x33` **OR** MPEG sync `0xFF 0xE0+` |
| `.wav` | RIFF header + `WAVE` at bytes 8–11 |
| `.m4a`, `.mp4` | `ftyp` at bytes 4–7 |
| `.ogg` | OGG header `0x4F 0x67 0x67 0x53` |
| `.webm` | EBML header `0x1A 0x45 0xDF 0xA3` |
| `.mpeg` | MPEG PS `0x00 0x00 0x01 0xBA` or MPEG Seq `0x00 0x00 0x01 0xB3` or MP3 sync |

### Image Signatures

| Extension | Expected Signature |
|---|---|
| `.png` | `0x89 0x50 0x4E 0x47 0x0D 0x0A 0x1A 0x0A` |
| `.jpg`, `.jpeg` | `0xFF 0xD8 0xFF` |

---

## 17. Enums Reference

### UserType

| Value | Name |
|---|---|
| `1` | `Deaf` |
| `2` | `NormalUser` |

### SignLanguage

| Value | Name |
|---|---|
| `1` | `Egyptian` |

---

## 18. Password Regex

All password fields use the same core rules:

```
Min length    : 8
Max length    : 100  (only on Change Password)
Uppercase     : [A-Z]          — at least one
Lowercase     : [a-z]          — at least one
Digit         : [0-9]          — at least one
Special char  : [\!\?\*\.#@\$%\^&\(\)_\+\-=\[\]\{\};:'"<>,./\\]
No HTML tags  : ^[^<>]*$       (Reset & Change Password only)
```

### JavaScript-Ready Regex

```javascript
const passwordRules = {
  minLength:    8,
  maxLength:    100,
  hasUppercase: /[A-Z]/,
  hasLowercase: /[a-z]/,
  hasDigit:     /[0-9]/,
  hasSpecial:   /[!?\*.#@$%^&()_+\-=\[\]{};:'"<>,./\\]/,
  noHtmlTags:   /^[^<>]*$/,
};
```

### Phone Number Regex (Egyptian Only)

```javascript
const phoneRegex = /^01[0125]\d{8}$/;
// Valid: 01012345678, 01112345678, 01212345678, 01512345678
// Invalid: 01312345678, 0101234567, +201012345678
```

### OTP Regex

```javascript
const otpRegex = /^\d{6}$/;
```

### Email Regex (Forgot Password — strict)

```javascript
const emailRegex = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
const noForbiddenChars = /^[^<>&'"\\/;`]*$/;
```

---

## OTP / Resend Rules Summary

| Rule | Value |
|---|---|
| OTP is 6 digits | `^\d{6}$` |
| OTP expires after | 10 minutes |
| Max wrong attempts | 3 (then OTP is invalidated) |
| Resend cooldown | 60 seconds |
| Already verified → error | `"Email is already verified."` |

---

> **Note:** All validation errors return **HTTP 400** with a `ProblemDetails` body containing field-level error messages in the `errors` object.

---

## 19. Change Email

Changing email is a **2-step process** for security.

### Step 1: Request Email Change

**Endpoint:** `POST /api/v1/Auth/ChangeEmail`  
**Content-Type:** `application/json`  
**Auth:** Bearer Token required  
**Rate Limit:** `otp-request-limit`

| Field | Type | Required | Rules | Error Message |
|---|---|---|---|---|
| `NewEmail` | `string` | ✅ | Not empty, valid email, max 256 chars, no forbidden chars | `"New email is required."` / `"A valid email is required."` |
| `CurrentPassword` | `string` | ✅ | Not empty, max 100 chars | `"Current password is required."` |

#### Server-Side Validations (Step 1)

| Check | Error Code | Error Message |
|---|---|---|
| Wrong password | `InvalidCurrentPassword` | `"Current password is incorrect."` |
| Same email | `Email.SameAsCurrent` | `"New email must be different from current email."` |
| Email taken | `DuplicateEmail` | `"Email is already in use."` |
| OTP cooldown (60s) | `PleaseWait` | `"Please wait a minute before requesting a new code."` |

✅ On success: OTP is sent to the **new email** (not the current one).

### Step 2: Confirm Email Change

**Endpoint:** `POST /api/v1/Auth/ConfirmEmailChange`  
**Content-Type:** `application/json`  
**Auth:** Bearer Token required  
**Rate Limit:** `otp-verify-limit`

| Field | Type | Required | Rules | Error Message |
|---|---|---|---|---|
| `NewEmail` | `string` | ✅ | Not empty, valid email, max 256 chars | `"New email is required."` |
| `Otp` | `string` | ✅ | Exactly 6 digits | `"OTP is required."` / `"OTP must be exactly 6 characters."` |

#### Server-Side Validations (Step 2)

| Check | Error Code | Error Message |
|---|---|---|
| No active OTP | `InvalidOtp` | `"No active verification code found. Please request a new one."` |
| Max attempts (3) reached | `MaxAttemptsReached` | `"Maximum attempts reached. Please request a new code."` |
| Wrong OTP | `WrongOtp` | `"Invalid verification code. Remaining attempts: {N}"` |
| Email taken (race condition) | `DuplicateEmail` | `"Email is already in use."` |

✅ On success: Email is updated, all sessions are invalidated, user must log in again.

### Security Features

| Feature | Description |
|---|---|
| **Password required** | Prevents email change if JWT is stolen |
| **OTP to new email** | Proves ownership of the new email |
| **OTP hashed (SHA-256)** | Never stored in plaintext |
| **Max 3 attempts** | OTP is invalidated after 3 wrong guesses |
| **60s cooldown** | Prevents OTP spam |
| **Rate limiting** | `otp-request-limit` + `otp-verify-limit` |
| **Race condition check** | Email availability re-checked at confirmation |
| **Session invalidation** | Refresh token + security stamp reset |

---

## 20. CSRF / XSRF Token

CSRF protection is **required for Web clients only** (cookie-based auth). Mobile clients are exempt because they don't use browser-attached cookies.

### How It Works

```
1. User logs in → Server sets TWO cookies:
   ├── refreshToken    (HttpOnly, Secure, SameSite=Strict) → auth cookie
   └── XSRF-TOKEN      (non-HttpOnly, JS-readable)         → CSRF token

2. On protected requests → Web client MUST send:
   └── Header: X-XSRF-TOKEN = <value from XSRF-TOKEN cookie>
```

### Cookies Set After Login/Register

| Cookie | HttpOnly | Secure | SameSite | Expires | Purpose |
|---|---|---|---|---|---|
| `refreshToken` | ✅ Yes | ✅ Yes | `Strict` | 7 days | Authentication |
| `XSRF-TOKEN` | ❌ No (JS can read it) | ✅ Yes | — | Session | CSRF protection |

### Endpoints That Require XSRF Token

| Endpoint | Method | Requires `X-XSRF-TOKEN` Header? |
|---|---|---|
| `/api/v1/Auth/RefreshToken` | POST | ✅ Yes (Web only) |
| `/api/v1/Auth/Logout` | POST | ✅ Yes (Web only) |
| All other endpoints | — | ❌ No |

### Web Client Integration (JavaScript)

```javascript
// 1. Read the XSRF-TOKEN cookie
function getCookie(name) {
  const match = document.cookie.match(new RegExp('(^| )' + name + '=([^;]+)'));
  return match ? decodeURIComponent(match[2]) : null;
}

// 2. Send it in the header on protected requests
fetch('/api/v1/Auth/RefreshToken', {
  method: 'POST',
  credentials: 'include',  // IMPORTANT: sends cookies
  headers: {
    'Content-Type': 'application/json',
    'X-XSRF-TOKEN': getCookie('XSRF-TOKEN'),  // CSRF header
  },
});
```

### Mobile Client Integration

Mobile clients (Flutter, React Native, etc.) **do NOT need CSRF tokens**.

Instead, they send the refresh token in the **request body**:

```json
POST /api/v1/Auth/RefreshToken
{
  "refreshToken": "your-refresh-token-here"
}
```

The server detects the auth method automatically:
- If `refreshToken` **cookie** exists → validates XSRF token (Web flow)
- If `refreshToken` is in **body** only → skips XSRF validation (Mobile flow)

### Error Response (CSRF Failure)

```json
HTTP 400
{
  "error": "Anti-forgery validation failed."
}
```

### Production Setup (Reverse Proxy)

When running behind a TLS-terminating reverse proxy (Nginx), the app receives HTTP internally.
The following Nginx headers are **required** for CSRF cookies to work:

```nginx
proxy_set_header Host $host;
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
proxy_set_header X-Forwarded-Proto $scheme;
```

The app uses `UseForwardedHeaders()` to read `X-Forwarded-Proto` and correctly set
the cookie `Secure` flag via `CookieSecurePolicy.SameAsRequest`.

> **Note:** CSRF validation is **skipped in Development** mode to allow Swagger UI testing.

---

## 21. Token Security Architecture

### Access Token (JWT)

| Property | Value |
|---|---|
| **Algorithm** | HMAC-SHA256 (`HS256`) |
| **Expiry** | 15 minutes (configurable via `JWTOptions:AccessTokenExpiryInMinutes`) |
| **Issuer/Audience** | Validated on every request |

#### JWT Claims

| Claim | Type | Description |
|---|---|---|
| `sub` | `JwtRegisteredClaimNames.Sub` | User ID |
| `email` | `JwtRegisteredClaimNames.Email` | User email |
| `name` | `JwtRegisteredClaimNames.Name` | Username |
| `jti` | `JwtRegisteredClaimNames.Jti` | Unique token ID (replay protection) |
| `nameid` | `ClaimTypes.NameIdentifier` | User ID (ASP.NET standard) |
| `email` | `ClaimTypes.Email` | User email (ASP.NET standard) |
| `role` | `ClaimTypes.Role` | User roles (one per role) |

> **Note:** Both `sub` and `nameid` contain the User ID. `sub` is the JWT standard; `nameid` is the ASP.NET standard used by `User.FindFirstValue(ClaimTypes.NameIdentifier)`.

### Refresh Token

| Property | Value |
|---|---|
| **Format** | 64 random bytes → Base64 string |
| **Expiry** | 7 days (configurable via `JWTOptions:RefreshTokenExpiryInDays`) |
| **Storage (DB)** | **SHA-256 hash only** — plaintext is never persisted |
| **Storage (Client)** | Web: `HttpOnly` + `Secure` + `SameSite=Strict` cookie; Mobile: response body |
| **Rotation** | New token issued on every refresh (old hash replaced) |

#### Refresh Token Flow

```
Client sends plaintext refresh token
        ↓
Server hashes with SHA-256
        ↓
Lookup by hash in DB (never by plaintext)
        ↓
If valid & not expired:
  1. Generate new plaintext token
  2. Store SHA-256(new token) in DB
  3. Return new plaintext to client
  4. Old hash is overwritten (single-use)
```

#### Security Features

| Feature | Description |
|---|---|
| **SHA-256 Hashing** | DB compromise doesn't leak usable tokens |
| **Token Rotation** | Each refresh invalidates the previous token |
| **Session Invalidation** | `ChangePassword` / `ChangeEmail` sets `RefreshToken = null` + resets `SecurityStamp` |
| **Cookie Security** | `HttpOnly` (no JS access), `Secure` (HTTPS only), `SameSite=Strict` (no CSRF) |
| **Dual-client support** | Web clients use cookies; Mobile clients use response body |

#### Important for Frontend/Mobile Teams

| Client | How to store | How to send |
|---|---|---|
| **Web** | Browser handles cookie automatically | Cookie sent automatically; no manual header needed |
| **Mobile** | Store in secure storage (e.g. Keychain/Keystore) | Send in request body: `{ "refreshToken": "..." }` |

---

## 22. Profile Image Upload

**Endpoint:** `POST /api/v1/Auth/UploadProfileImage`  
**Auth:** JWT Required  
**Content-Type:** `multipart/form-data`  
**Max Request Size:** 5 MB (enforced at Kestrel + service level)  
**Rate Limit:** 5 uploads per minute per authenticated user

### Request

| Field | Type | Required | Rules |
|---|---|---|---|
| `image` | `IFormFile` | ✅ | JPEG, PNG, or WebP only; max 5 MB |

### Validation Layers (Defense-in-Depth)

| Layer | What it checks |
|---|---|
| **1. Presence** | File is not null and has content |
| **2. Size** | `≤ 5 MB` (`MaxProfileImageSizeBytes`) |
| **3. Extension** | `.jpg`, `.jpeg`, `.png`, `.webp` only |
| **4. MIME Type** | `image/jpeg`, `image/png`, `image/webp` only |
| **5. Magic Bytes** | `FileSignatureValidator.IsAllowedImage()` — checks actual file header bytes |
| **6. ImageSharp decode** | File is re-decoded as an image; corrupt/polyglot files rejected |

### Image Processing Pipeline (ImageSharp)

| Step | Description |
|---|---|
| **1. Decode** | Parse uploaded bytes as image — rejects non-image payloads |
| **2. Strip metadata** | Removes all EXIF, IPTC, XMP (GPS, camera info, etc.) |
| **3. Resize** | Max 512×512 px, preserves aspect ratio, never upscales |
| **4. Re-encode** | Saves clean image in original format (JPEG @85%, PNG, WebP @85%) |

> **Why re-encode?** Raw uploaded bytes may contain embedded malicious payloads (polyglot files). Re-encoding through ImageSharp produces a clean file containing only valid pixel data.

### Security Features

| Feature | Description |
|---|---|
| **GUID filename** | Original filename is **never used**; stored as `{Guid}.{ext}` |
| **External storage** | Files stored **outside wwwroot** in configurable `UploadStorage:BasePath` |
| **Path traversal protection** | Both upload and serving validate `Path.GetFullPath()` stays within base directory |
| **Extension + MIME + Magic bytes** | Triple validation before ImageSharp processing |
| **Re-encoding** | Strips polyglot payloads; only clean pixel data is saved |
| **Metadata stripping** | No EXIF/GPS/camera data leaks |
| **Old image cleanup** | Previous profile image deleted on successful update |
| **Rollback on DB failure** | If DB update fails, newly saved file is removed |
| **Rate limiting** | 5 uploads/min per user (prevents abuse) |

### Storage Architecture

```
/var/www/uploads/            ← Configurable via UploadStorage:BasePath
  profile/
    {guid}.jpg
    {guid}.png
    {guid}.webp
```

> Storage is **outside wwwroot** — files are NOT publicly accessible via static file middleware. They are served exclusively through the hardened `GetMedia` endpoint.

**Configuration** (`appsettings.json`):
```json
{
  "UploadStorage": {
    "BasePath": "/app/wwwroot/media"
  }
}
```

**Public URL:** `{baseUrl}/api/v1/media/profile/{filename}`  
(served via the hardened `GetMedia` endpoint with path traversal guards)

### GetMedia Endpoint Hardening

| Protection | Description |
|---|---|
| **Filename sanitization** | `Path.GetFileName()` strips path components; rejects `..` and invalid chars |
| **Type whitelist** | Only `audio`, `video`, `pose`, `profile` accepted |
| **Path traversal guard** | `Path.GetFullPath()` must resolve within expected base directory |
| **External routing** | Profile images served from `UploadStorage:BasePath`; other media from `wwwroot` |

### Success Response

```json
HTTP 200
{
  "profileImageUrl": "https://api.s2sai.online/api/v1/media/profile/a1b2c3d4-e5f6-7890-abcd-ef1234567890.jpg"
}
```

### Error Responses

| Error Code | HTTP | Description |
|---|---|---|
| `Image.Required` | 400 | File is missing or empty |
| `Image.TooLarge` | 400 | File exceeds 5 MB |
| `Image.InvalidFormat` | 400 | Unsupported file extension |
| `Image.InvalidContentType` | 400 | MIME type doesn't match allowed types |
| `Image.InvalidSignature` | 400 | File bytes don't match declared extension |
| `Image.Corrupt` | 400 | ImageSharp cannot decode file (not a real image) |
| `UserNotFound` | 404 | JWT user ID not found in DB |
| `UpdateFailed` | 500 | DB update failed |
| `StorageFailure` | 500 | Disk I/O error |

### Profile Image URL in API Responses

`ProfileImageUrl` is included in the `UserDTO` response for **all authentication endpoints**:

| Endpoint | Returns `profileImageUrl` |
|---|---|
| `POST /Login` | ✅ |
| `POST /google-login` | ✅ |
| `POST /VerifyEmail` | ✅ |
| `POST /RefreshToken` | ✅ |
| `GET /CurrentUser` | ✅ |
| `POST /UpdateProfile` | ✅ |
| `POST /UploadProfileImage` | ✅ |

**How it works:**
- DB stores only the filename: `a1b2c3d4-...-.jpg`
- Controller rewrites it to full URL via `WithProfileUrl()` helper
- Response contains: `https://api.s2sai.online/api/v1/media/profile/a1b2c3d4-...-.jpg`
- If user has no profile image, field is `null`

### Inline Browser Display

Profile images are served with `Content-Disposition: inline`:
- Opening the URL in a browser **displays the image directly** (no download prompt)
- Other media types (audio, video, pose) still force download

### Access Model

Profile image URLs use **GUID-based security** (unguessable URLs):

| Property | Detail |
|---|---|
| **Public access** | Anyone with the full URL can view the image |
| **URL is unguessable** | Filename is a random GUID (e.g., `a1b2c3d4-e5f6-7890-abcd-ef1234567890.jpg`) |
| **URL only shared in auth responses** | The URL is only returned in JWT-authenticated API responses |
| **No directory listing** | The storage endpoint only serves individual files — no browsing |

> This is the same model used by Google Drive ("anyone with the link"), WhatsApp profile photos, and most modern platforms. It balances security with usability for features like chat avatars, user lists, and admin dashboards.

---

## 23. Security Hardening (Audit 2026-05-08)

### Security Headers

All API responses include the following security headers:

| Header | Value | Purpose |
|---|---|---|
| `X-Content-Type-Options` | `nosniff` | Prevents MIME-type sniffing |
| `X-Frame-Options` | `DENY` | Prevents clickjacking |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Limits referrer leakage |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` | Disables browser features |
| `X-Permitted-Cross-Domain-Policies` | `none` | Blocks Flash/PDF cross-domain |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` | Forces HTTPS (when detected) |

### CORS Configuration

| Property | Value |
|---|---|
| **Allowed Origins** | `https://s2sai.online`, `https://www.s2sai.online`, `http://localhost:3000`, `http://localhost:5173` |
| **AllowAnyHeader** | ✅ |
| **AllowAnyMethod** | ✅ |
| **AllowCredentials** | ✅ (required for cookie-based auth) |

> `AllowCredentials` is required so the browser sends the `refreshToken` and `XSRF-TOKEN` cookies cross-origin from the frontend.

### Error Response Security

All API error responses follow these rules:

| Rule | Implementation |
|---|---|
| **No exception details** | `ex.Message` is never returned to clients |
| **No stack traces** | `GlobalExceptionMiddleware` logs traces but returns generic ProblemDetails |
| **No user enumeration** | `ForgotPassword` returns success even if email doesn't exist |
| **No email echo** | Error messages don't include the user's email address |
| **TraceId for debugging** | Each 500 response includes a unique `traceId` for log correlation |

### Rate Limiting Summary

| Endpoint | Policy | Limit |
|---|---|---|
| All Auth endpoints | `auth-limit` | 5/min per IP |
| Register, ForgotPassword, ResendOtp, ChangeEmail | `otp-request-limit` | 3/10min per IP+email |
| VerifyEmail, ResetPassword, ConfirmEmailChange | `otp-verify-limit` | 5/10min per IP |
| ChangePassword | `change-password-limit` | 5/5min per user |
| sign-to-text, audio-to-text, text-to-sign, audio-to-sign | `stt-limit` | 10/min per IP |
| GetMedia | `media-limit` | 60/min per IP |
| UploadProfileImage | `profile-image-upload-limit` | 5/min per user |

### Admin Endpoint Protection

| Protection | Detail |
|---|---|
| **Role-based access** | `[Authorize(Roles = "Admin")]` on all admin endpoints |
| **Self-lock prevention** | Admin cannot lock their own account via `toggle-lock` |
| **Rate limited** | `auth-limit` policy applied |

### Swagger

| Environment | Swagger Enabled |
|---|---|
| Development | ✅ Always |
| Production | ❌ Off by default (set `EnableSwagger: true` to override) |

> Endpoints with `[ValidateAntiForgeryForWeb]` automatically show `X-XSRF-TOKEN` header in Swagger UI via `CsrfTokenOperationFilter`.

### JWT Token Hardening

#### Token Claims (Minimal, No Duplicates)

| Claim | Name in Token | Source |
|---|---|---|
| User ID | `sub` | `user.Id` |
| Email | `email` | `user.Email` |
| Username | `name` | `user.UserName` |
| Token ID | `jti` | `Guid.NewGuid()` |
| Role(s) | `role` | `UserManager.GetRolesAsync` |

> **No schema URLs.** We use short JWT claim names (`sub`, `email`, `role`) instead of `http://schemas.xmlsoap.org/...` URIs. This reduces token size and prevents .NET framework leakage.

#### Validation Configuration

| Setting | Value | Why |
|---|---|---|
| `ValidateIssuer` | `true` | Ensures token was issued by our server |
| `ValidateAudience` | `true` | Ensures token is intended for our API |
| `ValidateLifetime` | `true` | Rejects expired tokens |
| `ValidateIssuerSigningKey` | `true` | Verifies the signing key |
| `ClockSkew` | `30 seconds` | Default was 5 min — expired tokens valid only 30s extra |
| `MapInboundClaims` | `false` | Prevents ASP.NET from remapping `sub` → schema URL |
| `RoleClaimType` | `"role"` | Maps `[Authorize(Roles)]` to short `role` claim |
| `NameClaimType` | `JwtRegisteredClaimNames.Name` | Maps `User.Identity.Name` to `name` claim |

#### Claim Reading (Controllers)

```csharp
// ✅ CORRECT — using JWT standard names
var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
var email = User.FindFirstValue(JwtRegisteredClaimNames.Email);

// ❌ WRONG — would return null with MapInboundClaims=false
var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
```

### Reverse Proxy (Nginx) Requirements

The app runs behind Nginx with TLS termination. Required Nginx headers:

```nginx
server {
    listen 443 ssl;
    server_name api.s2sai.online;

    location / {
        proxy_pass http://localhost:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

> Without `X-Forwarded-Proto`, HSTS headers and `Secure` cookies won't be set correctly.

---

## 24. Authentication Redirect & Open Redirect Protection

### How It Works

When an unauthenticated user hits any protected endpoint, the API returns a **structured 401 JSON response** (not an empty 401 or HTML redirect):

```json
HTTP 401
{
  "status": 401,
  "title": "Authentication required",
  "detail": "You must be logged in to access this resource.",
  "loginUrl": "https://s2sai.online/login",
  "returnUrl": "/api/v1/Auth/CurrentUser"
}
```

For insufficient permissions (e.g., non-admin accessing admin endpoints):

```json
HTTP 403
{
  "status": 403,
  "title": "Access denied",
  "detail": "You do not have permission to access this resource."
}
```

### Frontend Integration

```javascript
// Axios interceptor example
axios.interceptors.response.use(
  response => response,
  error => {
    if (error.response?.status === 401) {
      const { loginUrl, returnUrl } = error.response.data;
      // Redirect to login with returnUrl
      window.location.href = returnUrl
        ? `${loginUrl}?returnUrl=${encodeURIComponent(returnUrl)}`
        : loginUrl;
    }
    return Promise.reject(error);
  }
);
```

### Open Redirect Protection (Whitelist)

The `returnUrl` is validated server-side against a whitelist before being included in the response. This prevents attackers from crafting URLs that redirect users to malicious sites.

#### Whitelisted Origins

| Origin | Environment |
|---|---|
| `https://s2sai.online` | Production |
| `https://www.s2sai.online` | Production (www) |
| `http://localhost:3000` | Development (React) |
| `http://localhost:5173` | Development (Vite) |

#### Validation Rules (11 Defense Layers)

| Input | Result | Reason |
|---|---|---|
| `/api/v1/Auth/CurrentUser` | ✅ Allowed | Relative `/api/` path |
| `/api/v1/Translate/sign-to-text` | ✅ Allowed | Relative `/api/` path |
| `https://s2sai.online/home` | ✅ Allowed | Whitelisted origin |
| `/dashboard` | ❌ Rejected → `null` | Not an `/api/` path |
| `/profile` | ❌ Rejected → `null` | Not an `/api/` path |
| `https://evil.com/phish` | ❌ Rejected → `null` | Not in whitelist |
| `//evil.com` | ❌ Rejected → `null` | Protocol-relative URL |
| `javascript:alert(1)` | ❌ Rejected → `null` | Dangerous scheme |
| `data:text/html,...` | ❌ Rejected → `null` | Dangerous scheme |
| `file:///etc/passwd` | ❌ Rejected → `null` | Dangerous scheme |
| `\\/evil.com` | ❌ Rejected → `null` | Backslash bypass |
| `%2F%2Fevil.com` | ❌ Rejected → `null` | URL-encoded bypass |
| `\0/api/v1/hack` | ❌ Rejected → `null` | Control character |
| (3000+ char URL) | ❌ Rejected → `null` | Length limit exceeded |

#### Implementation

**File:** `Sehety.Shared/Security/RedirectUrlValidator.cs`

```csharp
// Usage anywhere in the codebase:
var safeUrl = RedirectUrlValidator.Validate(userProvidedUrl);
// Returns the URL if safe, null if rejected

bool isSafe = RedirectUrlValidator.IsSafe(userProvidedUrl);
```

### Protected vs Unprotected Endpoints

| Endpoint | Auth Required | Returns 401 if missing |
|---|---|---|
| `POST /Auth/Login` | ❌ | — |
| `POST /Auth/Register` | ❌ | — |
| `POST /Auth/VerifyEmail` | ❌ | — |
| `POST /Auth/ForgotPassword` | ❌ | — |
| `POST /Auth/ResetPassword` | ❌ | — |
| `POST /Auth/ResendOtp` | ❌ | — |
| `POST /Auth/google-login` | ❌ | — |
| `POST /Auth/RefreshToken` | ❌ | — |
| `GET /media/{type}/{file}` | ❌ | — |
| `GET /healthz` | ❌ | — |
| `GET /Auth/CurrentUser` | ✅ `[Authorize]` | ✅ 401 + loginUrl |
| `POST /Auth/Logout` | ✅ `[Authorize]` | ✅ 401 + loginUrl |
| `POST /Auth/ChangePassword` | ✅ `[Authorize]` | ✅ 401 + loginUrl |
| `POST /Auth/UpdateProfile` | ✅ `[Authorize]` | ✅ 401 + loginUrl |
| `POST /Auth/UploadProfileImage` | ✅ `[Authorize]` | ✅ 401 + loginUrl |
| `POST /Auth/ChangeEmail` | ✅ `[Authorize]` | ✅ 401 + loginUrl |
| `POST /Auth/ConfirmEmailChange` | ✅ `[Authorize]` | ✅ 401 + loginUrl |
| `POST /Auth/UpdateFcmToken` | ✅ `[Authorize]` | ✅ 401 + loginUrl |
| All `/Translate/*` endpoints | ✅ `[Authorize]` | ✅ 401 + loginUrl |
| All `/Admin/*` endpoints | ✅ `[Authorize(Roles="Admin")]` | ✅ 401 or 403 |

---

## 25. Email Normalization (Anti-Abuse)

### Problem

Gmail (and Googlemail) ignore dots and `+tags` in email addresses. An attacker can create unlimited accounts with the same mailbox:

```
user@gmail.com
u.ser@gmail.com        ← same mailbox
us.e.r@gmail.com       ← same mailbox
user+spam@gmail.com    ← same mailbox
```

### Solution

| Rule | Action | Applies To |
|---|---|---|
| **`+` (plus-addressing)** | ❌ **Blocked outright** | All providers |
| **`.` (dot trick)** | Normalized for duplicate check | Gmail/Googlemail only |

> Plus-addressing (`user+tag@any.com`) is **always rejected** at registration, email change, and Firebase login.

### How It Works

```
Registration attempt: "u.s.e.r+promo@gmail.com"
          ↓
Step 1: Block '+' → ❌ REJECTED ("Email addresses with '+' are not allowed.")
```

```
Registration attempt: "u.s.e.r@gmail.com"
          ↓
Step 1: Block '+' → ✅ no plus
Step 2: Exact match check → ❌ not found
Step 3: Normalize → "user@gmail.com"
Step 4: Check normalized against ALL existing emails:
        - "user@gmail.com" → normalize → "user@gmail.com" → ⚠️ MATCH!
          ↓
Result: "Email is already in use."
```

### Normalization Rules

| Input Email | Normalized Form | Notes |
|---|---|---|
| `user@gmail.com` | `user@gmail.com` | No change |
| `u.ser@gmail.com` | `user@gmail.com` | Dots removed (Gmail) |
| `u.s.e.r@gmail.com` | `user@gmail.com` | All dots removed (Gmail) |
| `user+tag@gmail.com` | ❌ Blocked | Plus not allowed |
| `user+tag@outlook.com` | ❌ Blocked | Plus not allowed |
| `user.name@outlook.com` | `user.name@outlook.com` | Dots **kept** (non-Gmail) |
| `User@Gmail.COM` | `user@gmail.com` | Case-insensitive |

### Enforced On

| Endpoint | Plus Block | Dot Check |
|---|---|---|
| `POST /Auth/Register` | ✅ | ✅ |
| `POST /Auth/ChangeEmail` | ✅ | ✅ |
| `POST /Auth/google-login` (Firebase) | ✅ | ✅ |

### Error Responses

| Error Code | HTTP | Description |
|---|---|---|
| `Email.PlusNotAllowed` | 400 | Email contains `+` in local part |
| `DuplicateEmail` | 400 | Normalized email matches existing account |

### Implementation

**File:** `Sehety.Shared/Security/EmailNormalizer.cs`

```csharp
// Block '+' addressing (all providers)
EmailNormalizer.ContainsPlus("user+tag@gmail.com");   // true → reject
EmailNormalizer.ContainsPlus("user+tag@outlook.com"); // true → reject
EmailNormalizer.ContainsPlus("user@gmail.com");       // false → ok

// Normalize for duplicate check (dots: Gmail only)
EmailNormalizer.NormalizeForDuplicateCheck("u.ser@gmail.com");   // "user@gmail.com"
EmailNormalizer.NormalizeForDuplicateCheck("u.ser@outlook.com"); // "u.ser@outlook.com" (dots kept)
```

> **Important:** The original email (with dots) is stored as-is in the database. Normalization is only used for **duplicate detection** — email delivery always uses the original address.

