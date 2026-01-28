# S2S API Authentication Documentation

This document provides a guide for the Frontend team on how to integrate with the Authentication API.

## 🔄 Authentication Flow

The application uses a **Token-Based Authentication** system (JWT) with **Refresh Tokens**.

1.  **Registration**:
    *   User registers with valid data.
    *   Server sends an OTP (6 digits) to the user's email.
    *   User enters the OTP to verify their email.
    *   **Upon successful verification, the user is automatically logged in** (receives Access Token).

2.  **Login**:
    *   Existing users log in with Email and Password.
    *   Server returns an **Access Token** (Short-lived, ~15 mins) and sets a **Refresh Token** in an `HttpOnly` Cookie.

3.  **Token Management**:
    *   Attach the `Access Token` to every authorized request in the Header: `Authorization: Bearer <TOKEN>`.
    *   When the Access Token expires (401 Unauthorized), call the **Refresh Token** endpoint. The browser will automatically send the cookie.

---

## 📚 Enums

### UserType (int)
Used during registration to specify the user category.
| Value | Name | Description |
| :--- | :--- | :--- |
| `1` | **Deaf**  |
| `2` | **NormalUser**  |

### SignLanguage (int)
Required if `UsesSignLanguage` is true.
| Value | Name | Description |
| :--- | :--- | :--- |
| `1` | **Egyptian** | Egyptian Sign Language. |

---

## 🚀 Endpoints

### 1. Register
Create a new account.

*   **URL**: `/api/v1/Auth/Register`
*   **Method**: `POST`
*   **Content-Type**: `application/json`

**Request Body:**
```json
{
  "email": "user@example.com",          // Required, Valid Email
  "displayName": "Ahmed Omar",          // Required, 2-100 chars
  "userName": "ahmed_omar",             // Required, 3-50 chars, no special chars
  "password": "Password123!",           // Required, Min 8 chars, 1 Upper, 1 Lower, 1 Digit, 1 Special
  "phoneNumber": "01012345678",         // Optional, Valid Phone
  "dateOfBirth": "2000-01-01",          // Optional, User must be > 7 years (Format: YYYY-MM-DD)
  "userType": 1,                        // Required (See Enums)
  "usesSignLanguage": true,             // bool
  "signLanguage": 1                     // Required if usesSignLanguage is true
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "Verification code sent to your email"
}
```

---

### 2. Verify Email (Confirm Account)
Verify the OTP sent to email. Completes registration and logs the user in.

*   **URL**: `/api/v1/Auth/VerifyEmail`
*   **Method**: `POST`

**Request Body:**
```json
{
  "email": "user@example.com",
  "otp": "123456"                       // 6 Digits
}
```

**Response (200 OK):**
```json
{
  "email": "user@example.com",
  "displayName": "Ahmed Omar",
  "token": "eyJhbGciOiJIUz...",         // <--- Access Token (Save this!)
  "refreshToken": null                  // (Stored in HttpOnly Cookie)
}
```

---

### 3. Login
Log in for existing users.

*   **URL**: `/api/v1/Auth/Login`
*   **Method**: `POST`

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "Password123!"
}
```

**Response (200 OK):**
Returns the same structure as Verify Email.

---

### 4. Resend OTP
Send a new verification code if the previous one expired.

*   **URL**: `/api/v1/Auth/ResendOtp?email=user@example.com`
*   **Method**: `POST`
*   **Query Params**: `email` (string)

**Response (200 OK):**
```json
{
  "success": true,
  "message": "New verification code sent to your email"
}
```

---

### 5. Refresh Token
Get a new Access Token when the old one expires.

*   **URL**: `/api/v1/Auth/RefreshToken`
*   **Method**: `POST`
*   **Note**: Browser automatically sends the `refreshToken` cookie.

**Request Body (Mobile Only - Optional for Web):**
```json
{
  "refreshToken": "..." // Only send if not using Cookies
}
```

**Response (200 OK):**
```json
{
  "email": "...",
  "displayName": "...",
  "token": "NEW_ACCESS_TOKEN",
  "refreshToken": null
}
```

---

### 6. Logout
 invalidate user session.

*   **URL**: `/api/v1/Auth/Logout`
*   **Method**: `POST`

**Response (200 OK):**
```json
{
  "success": true,
  "message": "Logged out successfully"
}
```
