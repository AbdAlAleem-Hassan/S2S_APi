# S2S API Documentation

> Base URL: `https://s2sai.online/api/v1`

---

## Table of Contents

- [Authentication](#authentication)
- [Global Configuration](#global-configuration)
- [Auth Endpoints](#1-auth-endpoints)
- [Translate Endpoints](#2-translate-endpoints)
- [Media Endpoint](#3-media-endpoint)
- [Admin Endpoints](#4-admin-endpoints)
- [Health Check](#5-health-check)
- [Rate Limits](#rate-limits)
- [Error Responses](#error-responses)

---

## Authentication

### How to authenticate

1. **Login/Register** returns a `UserDTO` containing a `Token` (JWT) and sets a `refreshToken` cookie (HttpOnly, Secure, SameSite=Strict).
2. **Attach JWT** to every request as: `Authorization: Bearer <token>`
3. **When token expires**, call `POST /Auth/RefreshToken` to get a new one.
4. **CSRF (Web only):** Protected endpoints require `X-XSRF-TOKEN` header. The server sets an `XSRF-TOKEN` cookie (HttpOnly=false, SameSite=Strict). Read the cookie value and send it back as the header.
   - **Mobile/clients that don't send cookies**: CSRF validation is skipped automatically (no `refreshToken` cookie → no CSRF check).

### JWT Claims

| Claim | Description |
|-------|-------------|
| `sub` | User ID (GUID) |
| `email` | User email |
| `name` | Display name |
| `role` | `"User"` or `"Admin"` |
| `is_unlimited` | `"true"` if user has unlimited translation quota |
| `iat`, `exp`, `iss`, `aud` | Standard JWT fields |

---

## Global Configuration

### Headers applied to all responses

| Header | Value |
|--------|-------|
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` | `DENY` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` |
| `X-Permitted-Cross-Domain-Policies` | `none` |
| `Cross-Origin-Resource-Policy` | `same-origin` |
| `Cross-Origin-Opener-Policy` | `same-origin` |
| `Content-Security-Policy` | `default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'; media-src 'self'; img-src 'self'` |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` (HTTPS only) |
| `Cache-Control` | `no-store` (for `/api/` paths) |

### CORS

- Allowed origins: `https://s2sai.online`, `https://www.s2sai.online`, `http://localhost:3000`, `http://localhost:5173`
- Allowed methods: `GET`, `POST`, `PUT`, `DELETE`, `PATCH`
- Allowed headers: `Content-Type`, `Authorization`, `X-XSRF-TOKEN`, `Accept`
- Credentials: allowed (cookies)
- Preflight cache: 10 minutes

---

## 1. Auth Endpoints

All Auth endpoints are rate-limited at **5 requests per minute** (controller-level), except where noted.

---

### 1.1 POST `/Auth/Login`

Authenticate with email/password.

**Auth:** `[AllowAnonymous]`

**Request:**
```json
{
  "email": "user@example.com",
  "password": "P@ssw0rd123"
}
```

**Response `200 OK`:**
```json
{
  "email": "user@example.com",
  "displayName": "John Doe",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": null,
  "profileImageUrl": "https://s2sai.online/api/v1/media/profile/abc.jpg"
}
```

**Also sets cookies:**
- `refreshToken` — HttpOnly, Secure, SameSite=Strict, expires 7 days
- `XSRF-TOKEN` — not HttpOnly, SameSite=Strict (for CSRF)

**Error `401`:** Invalid credentials

---

### 1.2 POST `/Auth/google-login`

Login with Firebase Google IdToken.

**Auth:** `[AllowAnonymous]`

**Request:**
```json
{
  "idToken": "firebase-id-token-string"
}
```

**Response `200 OK`:** Same as `UserDTO` above.

---

### 1.3 POST `/Auth/Register`

Create a new account.

**Auth:** `[AllowAnonymous]`
**Rate limit:** `otp-request-limit` — **3 requests per 10 minutes** (keyed by IP + email)

**Request:**
```json
{
  "email": "user@example.com",
  "displayName": "John Doe",
  "dateOfBirth": "2000-01-15",
  "userName": "johndoe",
  "password": "P@ssw0rd123",
  "phoneNumber": "+201234567890",
  "userType": 1,
  "usesSignLanguage": true,
  "signLanguage": 1
}
```

**Enums:**
- `UserType`: `1 = Deaf`, `2 = NormalUser`
- `SignLanguage`: `1 = Egyptian`

**Response `200 OK`:**
```json
{
  "success": true,
  "message": "Registration successful. Please check your email for verification code."
}
```

An OTP is sent to the email for verification.

---

### 1.4 POST `/Auth/VerifyEmail`

Verify email with OTP after registration.

**Auth:** `[AllowAnonymous]`
**Rate limit:** `otp-verify-limit` — **5 requests per 10 minutes** (keyed by IP)

**Request:**
```json
{
  "email": "user@example.com",
  "otp": "123456"
}
```

**Response `200 OK`:** Same as `UserDTO` (returns JWT token + sets refresh token cookie).

---

### 1.5 POST `/Auth/RefreshToken`

Get a new JWT token using refresh token.

**Auth:** `[AllowAnonymous]`

**Request (body optional — can also send via `refreshToken` cookie):**
```json
{
  "refreshToken": "stored-refresh-token-string"
}
```

**Response `200 OK`:** Same as `UserDTO` (new JWT + new refresh token cookie).

**Error `401`:** Invalid or expired refresh token.

---

### 1.6 POST `/Auth/Logout`

Invalidate the refresh token.

**Auth:** `[Authorize]`

**Request (body optional):**
```json
{
  "refreshToken": "stored-refresh-token-string"
}
```

**Response `200 OK`:**
```json
{
  "success": true,
  "message": "Logged out successfully."
}
```

Removes the `refreshToken` cookie.

---

### 1.7 POST `/Auth/ResendOtp`

Resend verification OTP email.

**Auth:** `[AllowAnonymous]`
**Rate limit:** `resend-otp-limit` — **5 requests per 15 minutes** (keyed by IP only)

**Query parameter:** `email`

```
POST /api/v1/Auth/ResendOtp?email=user@example.com
```

**Response `200 OK` (always same — prevents email enumeration):**
```json
{
  "success": true,
  "message": "If your email is registered, a verification code has been sent."
}
```

---

### 1.8 POST `/Auth/ForgotPassword`

Request password reset OTP.

**Auth:** `[AllowAnonymous]`
**Rate limit:** `otp-request-limit` — **3 requests per 10 minutes**

**Request:**
```json
{
  "email": "user@example.com"
}
```

**Response `200 OK`:** Always same success message.

---

### 1.9 POST `/Auth/ResetPassword`

Reset password using the token from email.

**Auth:** `[AllowAnonymous]`
**Rate limit:** `otp-verify-limit` — **5 requests per 10 minutes**

**Request:**
```json
{
  "token": "reset-token-from-email",
  "newPassword": "NewP@ssw0rd456",
  "confirmPassword": "NewP@ssw0rd456"
}
```

**Response `200 OK`:** Success message.

---

### 1.10 POST `/Auth/ChangePassword`

Change password for authenticated user.

**Auth:** `[Authorize]`
**Rate limit:** `change-password-limit` — **5 requests per 5 minutes** (keyed by user/IP)

**Request:**
```json
{
  "currentPassword": "OldP@ssw0rd",
  "newPassword": "NewP@ssw0rd456",
  "confirmNewPassword": "NewP@ssw0rd456"
}
```

**Response `200 OK`:**
```json
{
  "success": true,
  "message": "Password changed successfully."
}
```

Also clears the `refreshToken` cookie (forces re-login).

---

### 1.11 GET `/Auth/CurrentUser`

Get current authenticated user's data.

**Auth:** `[Authorize]`

**Response `200 OK`:**
```json
{
  "email": "user@example.com",
  "displayName": "John Doe",
  "token": null,
  "refreshToken": null,
  "profileImageUrl": "https://s2sai.online/api/v1/media/profile/abc.jpg"
}
```

---

### 1.12 POST `/Auth/UpdateFcmToken`

Update Firebase Cloud Messaging token for push notifications.

**Auth:** `[Authorize]`

**Request:**
```json
{
  "fcmToken": "firebase-messaging-token"
}
```

**Response `204 No Content`** on success.

---

### 1.13 POST `/Auth/UpdateProfile`

Update display name and phone number.

**Auth:** `[Authorize]`

**Request:**
```json
{
  "displayName": "New Name",
  "phoneNumber": "+201234567890"
}
```

`phoneNumber` is optional.

**Response `200 OK`:**
```json
{
  "displayName": "New Name",
  "phoneNumber": "+201234567890",
  "profileImageUrl": "https://s2sai.online/api/v1/media/profile/abc.jpg"
}
```

---

### 1.14 POST `/Auth/ChangeEmail`

Request email change (sends OTP to new email).

**Auth:** `[Authorize]`
**Rate limit:** `otp-request-limit` — **3 requests per 10 minutes**

**Request:**
```json
{
  "newEmail": "newemail@example.com",
  "currentPassword": "CurrentP@ssw0rd"
}
```

**Response `200 OK`:** Confirmation message.

---

### 1.15 POST `/Auth/ConfirmEmailChange`

Confirm email change with OTP.

**Auth:** `[Authorize]`
**Rate limit:** `otp-verify-limit` — **5 requests per 10 minutes**

**Request:**
```json
{
  "newEmail": "newemail@example.com",
  "otp": "123456"
}
```

**Response `200 OK`:** Confirmation message.

---

### 1.16 POST `/Auth/UploadProfileImage`

Upload profile image.

**Auth:** `[Authorize]`
**Rate limit:** `profile-image-upload-limit` — **5 requests per minute** (keyed by user/IP)
**Max size:** 5 MB

**Request:** `multipart/form-data`

| Field | Type | Description |
|-------|------|-------------|
| `image` | File | Image file (jpg, png, etc.) |

**Response `200 OK`:**
```json
{
  "profileImageUrl": "https://s2sai.online/api/v1/media/profile/abc.jpg"
}
```

---

## 2. Translate Endpoints

All Translate endpoints require **authentication** and are rate-limited by **translation-quota** — **10 requests per hour per user** (sliding window). Unlimited users (identified by JWT claim `is_unlimited=true`) bypass this limit.

---

### 2.1 POST `/Translate/sign-to-text`

Upload a sign language video and get translated text.

**Auth:** `[Authorize]`
**Rate limit:** `translation-quota` (10/hr)
**Max video size:** 50 MB

**Request:** `multipart/form-data`

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `video_file` | File | — | Sign language video file |
| `language` | string | `"ar"` | Language for translation |
| `include_audio` | bool | `false` | If true, generates audio (TTS) of the translation |
| `SaveToHistory` | bool | `false` | If true, saves to translation history |

**Response `200 OK`:**
```json
{
  "session_id": "uuid-string",
  "status": "completed",
  "translation": {
    "text": "مرحباً",
    "confidence": 0.95
  }
}
```

The `translation` Dictionary keys depend on the AI model response.

If `include_audio` was `true`, the response may also include an `audio_url` in the translation.

---

### 2.2 POST `/Translate/audio-to-text`

Upload audio and get recognized text (Speech-to-Text via Groq API).

**Auth:** `[Authorize]`
**Rate limit:** `translation-quota` (10/hr)
**Max audio size:** 18 MB

**Request:** `multipart/form-data`

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `audio_file` | File | — | Audio file |
| `language` | string | `"ar"` | Language code |

**Response `200 OK`:**
```json
{
  "text": "النص المستخرج من الصوت"
}
```

---

### 2.3 POST `/Translate/text-to-sign`

Convert text to sign language video/animation.

**Auth:** `[Authorize]`
**Rate limit:** `translation-quota` (10/hr)

**Request:** `multipart/form-data`

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `text` | string | — | Input text to translate |
| `avatar` | string | `"default"` | Avatar style |
| `speed` | string | `"1.0"` | Animation speed |
| `output_format` | string | `"pose"` | Output format (pose, sigml, etc.) |
| `SaveToHistory` | bool | `false` | If true, saves to history |

**Response `200 OK`:**
```json
{
  "session_id": "uuid-string",
  "status": "completed",
  "translation": {
    "video_url": "https://s2sai.online/api/v1/media/video/abc.mp4",
    "pose_url": "https://s2sai.online/api/v1/media/pose/abc.pose",
    "sigml_content": "<sigml>...</sigml>",
    "duration": 3.5,
    "original_text": "مرحباً",
    "output_format": "pose",
    "glosses": ["مرحباً", "كيف", "حالك"]
  }
}
```

---

### 2.4 POST `/Translate/audio-to-sign`

Upload audio and get sign language video (STT + Text-to-Sign combined).

**Auth:** `[Authorize]`
**Rate limit:** `translation-quota` (10/hr)
**Max audio size:** 18 MB

**Request:** `multipart/form-data`

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `audio_file` | File | — | Audio file |
| `avatar` | string | `"default"` | Avatar style |
| `speed` | string | `"1.0"` | Animation speed |
| `output_format` | string | `"pose"` | Output format |

**Response `200 OK`:** Same as `ToSignResponseDTO`.

---

### 2.5 GET `/Translate/history`

Get translation history for the authenticated user.

**Auth:** `[Authorize]`

**Query parameters:**

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `pageNumber` | int | `1` | Page number |
| `pageSize` | int | `10` | Items per page |

**Response `200 OK`:**
```json
[
  {
    "id": 1,
    "arabicInputText": "مرحباً",
    "videoUrl": "https://s2sai.online/api/v1/media/video/abc.mp4",
    "poseUrl": "https://s2sai.online/api/v1/media/pose/abc.pose",
    "sigmlContent": "<sigml>...</sigml>",
    "audioUrl": "https://s2sai.online/api/v1/media/audio/abc.mp3",
    "createdAt": "2026-06-24T10:30:00Z"
  }
]
```

Ordered newest to oldest.

---

## 3. Media Endpoint

### GET `/media/{type}/{fileName}`

Serve uploaded media files (video, audio, pose, profile images).

**Auth:** `[Authorize]`
**Rate limit:** `media-limit` — **60 requests per minute** (keyed by IP)

**Route parameters:**

| Parameter | Type | Allowed Values |
|-----------|------|----------------|
| `type` | string | `audio`, `video`, `pose`, `profile` |
| `fileName` | string | File name with extension |

**Cache headers:**
- Profile images: `Cache-Control: public, max-age=604800, immutable` (7 days)
- Other media: `Cache-Control: public, max-age=3600` (1 hour)

**Response `200 OK`:** Physical file with correct MIME type.

**Error `404`:** File not found.

---

## 4. Admin Endpoints

All Admin endpoints require **`[Authorize(Roles = "Admin")]`** — JWT must contain `"role": "Admin"`.
Rate-limited at **5 requests per minute**.

---

### 4.1 GET `/Admin/users`

Get all registered users (excluding the current admin).

**Auth:** `[Authorize(Roles = "Admin")]`

**Response `200 OK`:**
```json
[
  {
    "id": "user-guid",
    "firstName": "John",
    "lastName": "Doe",
    "email": "user@example.com",
    "isLockedOut": false,
    "lockoutEnd": null
  }
]
```

---

### 4.2 PUT `/Admin/users/{id}/toggle-lock`

Lock or unlock a user account.

**Auth:** `[Authorize(Roles = "Admin")]`

**Route:** `id` — User GUID

**Response `200 OK`:**
```json
{
  "isSuccess": true,
  "value": "User locked/unlocked successfully.",
  "error": null
}
```

Cannot lock your own admin account (returns `400`).

---

### 4.3 PUT `/Admin/users/{id}/toggle-unlimited`

Toggle unlimited translation quota for a user. Unlimited users bypass the 10/hour translation quota.

**Auth:** `[Authorize(Roles = "Admin")]`

**Route:** `id` — User GUID

**Response `200 OK`:**
```json
{
  "isSuccess": true,
  "value": "User unlimited status toggled successfully.",
  "error": null
}
```

Takes effect within 15 minutes (JWT token lifetime). User must re-login or refresh token.

---

## 5. Health Check

### GET `/healthz`

Simple health check endpoint.

**Auth:** None (anonymous)

**Response `200 OK`:**
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567",
  "checks": [
    {
      "name": "database",
      "status": "Healthy",
      "duration": "00:00:00.0123456",
      "description": null
    }
  ]
}
```

---

## Rate Limits

| Policy | Limit | Window | Keyed By | Where Used |
|--------|-------|--------|----------|------------|
| `auth-limit` | 5 | 1 minute | Global | Auth + Admin controllers |
| `otp-request-limit` | 3 | 10 minutes | IP + Email | Register, ForgotPassword, ChangeEmail |
| `otp-verify-limit` | 5 | 10 minutes | IP | VerifyEmail, ResetPassword, ConfirmEmailChange |
| `resend-otp-limit` | 5 | 15 minutes | IP | ResendOtp |
| `change-password-limit` | 5 | 5 minutes | User/IP | ChangePassword |
| `media-limit` | 60 | 1 minute | IP | Media serving |
| `profile-image-upload-limit` | 5 | 1 minute | User/IP | UploadProfileImage |
| `translation-quota` | 10 | 1 hour (sliding) | User ID | All 4 translation endpoints |

When exceeded, all return **`429 Too Many Requests`** with no retry header.

---

## Error Responses

### Standard Error Format (from `ApiBaseController.HandleRequest`)

**`400 Bad Request`** — Validation errors:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["The Email field is not a valid e-mail address."]
  }
}
```

**`401 Unauthorized`** — Missing or invalid JWT:
```json
{
  "error": "Unauthorized",
  "message": "Please login to access this resource."
}
```

**`403 Forbidden`** — Authenticated but insufficient permissions:
```json
{
  "error": "Forbidden",
  "message": "You do not have permission to access this resource."
}
```

**`404 Not Found`** — Resource not found

**`429 Too Many Requests`** — Rate limit exceeded

**`500 Internal Server Error`** — Unexpected server error (with `ProblemDetails` format)

---

## Input Validation Rules

| Field | Rules |
|-------|-------|
| Email | Must be valid email format, max 256 chars |
| Password | Min 8 chars, requires digit + lowercase + uppercase + non-alphanumeric |
| OTP | 6 digits |
| File names | Max 512 characters, no path traversal (`../`, `..\\`), no null bytes |
| String fields | No control characters (0x00-0x1F except 0x09 tab), max 2000 chars |
| Profile image | Max 5 MB |
| Video file | Max 50 MB |
| Audio file | Max 18 MB |

---

## Cookie Summary

| Cookie | Type | HttpOnly | Secure | SameSite | Purpose |
|--------|------|----------|--------|----------|---------|
| `refreshToken` | Auth | Yes | Yes (HTTPS) | Strict | Refresh token rotation |
| `XSRF-TOKEN` | CSRF | No | Yes (HTTPS) | Strict | Anti-forgery token |
