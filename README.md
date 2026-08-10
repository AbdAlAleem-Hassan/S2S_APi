# S2S API — Sign Language Translation Backend

A .NET REST API that powers **S2S (Sehety)**, a graduation project enabling real-time, two-way communication between Deaf and hearing users through Arabic Sign Language translation. This service acts as the secure backend layer between the client apps (web & Flutter mobile) and the AI translation model server, handling authentication, business logic, media storage, and orchestration of all translation requests.

> Base URL (production): `https://s2sai.online/api/v1`
> Full endpoint reference: [`API_DOCS.md`](./API_DOCS.md)

---

## Overview

S2S bridges communication between Deaf and hearing individuals through two translation paths:

- **Sign → Text/Audio:** A Deaf user records or uploads a sign language video. The API forwards it to the AI model server and returns the translated Arabic text, optionally with generated speech (TTS).
- **Text/Audio → Sign:** A hearing user types or speaks a message. The API converts speech to text (when needed), sends it to the AI model server, and returns a sign language avatar animation for the Deaf user.

This repository contains **my contribution to the project**: the ASP.NET Core Web API that sits between the front-end clients (Web & Flutter) and the AI Model Server, acting as a secure proxy that handles auth, validation, storage, rate limiting, and response shaping — while the actual sign-language inference happens on a separate AI model server.

---

## Features

- **Authentication & Accounts** — Email/password and Google (Firebase) login, JWT access tokens with refresh-token rotation, email OTP verification, password reset, and profile management.
- **Translation Endpoints** — Sign-to-text, text-to-sign, audio-to-text (via Groq speech-to-text), and audio-to-sign, each proxied to the AI model server and returned in a consistent response shape.
- **Translation History** — Paginated history of a user's past translations (text, video, pose, and audio URLs).
- **Media Delivery** — Dedicated endpoint for serving generated video/audio/pose files and profile images, with type-specific caching.
- **Admin Panel** — User listing, account lock/unlock, and toggling unlimited translation quota for specific users.
- **Rate Limiting** — Per-endpoint-group fixed/sliding-window limiters (auth, OTP, translation quota, media, profile uploads) to prevent abuse.
- **Security Hardening** — JWT + refresh-token cookie flow, CSRF protection (auto-skipped for non-cookie mobile clients), strict security response headers (CSP, HSTS, X-Frame-Options, etc.), and input sanitization on all incoming requests.
- **Observability** — Structured logging with Serilog, shipped to Seq; a `/healthz` endpoint reporting API and database health.
- **API Documentation** — Versioned REST API (v1) with Swagger/OpenAPI.

---

## Tech Stack

| Layer | Technologies |
|---|---|
| **Framework** | .NET 10, ASP.NET Core Web API |
| **Architecture** | Layered/Clean Architecture — separated Domain, Persistence, Services, Presentation, and Web layers |
| **Data** | Entity Framework Core, SQL Server, ASP.NET Core Identity |
| **Auth** | JWT Bearer Authentication, Firebase Admin SDK (Google Sign-In), BCrypt |
| **Mapping & Validation** | AutoMapper, FluentValidation |
| **AI Integrations** | Groq API (speech-to-text), Google Cloud Text-to-Speech, external AI model server for sign-language translation |
| **Logging** | Serilog + Seq |
| **API Docs** | Swashbuckle (Swagger/OpenAPI), API versioning |
| **Infrastructure** | Docker, GitHub Container Registry, Portainer |

---

## Architecture

The API follows a layered structure, separating concerns for maintainability and testability:

```
Sehety.Domain              → Entities, contracts/interfaces
Sehety.Persistence          → EF Core DbContext, configurations, identity data
Sehety.Services              → Business logic (auth, translation, media, admin)
Sehety.ServicesAbstraction    → Service interfaces
Sehety.Presentation          → API controllers (versioned)
Sehety.Shared                → DTOs, validators, mappings, common result wrapper
Sehety.Web                  → Composition root — startup, middleware, health checks
```

Incoming requests pass through authentication, rate limiting, and input sanitization middleware before reaching versioned controllers, which delegate to the service layer. The service layer talks to the AI model server for translation inference and to EF Core/SQL Server for persistence.

---

## Getting Started

### Prerequisites
- .NET 10 SDK
- SQL Server (local or containerized)
- API keys: Groq (speech-to-text), Google Cloud credentials (Text-to-Speech), Firebase Admin credentials (Google login)

### Run locally

```bash
# clone the repo
git clone https://github.com/AbdAlAleem-Hassan/S2S_APi.git
cd S2S_APi

# restore & run
dotnet restore
dotnet run --project Sehety.Web
```

Configure your local `appsettings.json` (or user secrets) with a SQL Server connection string, JWT options, and the third-party API keys listed above before running.

### Run with Docker

The API ships as a Docker image and is deployed via GitHub Container Registry. See [`DEPLOYMENT.md`](./DEPLOYMENT.md) for the full build-and-push workflow.

---

## API Reference

Full endpoint-by-endpoint documentation — request/response payloads, auth requirements, rate limits, and error formats — is available in [`API_DOCS.md`](./API_DOCS.md), covering:

1. Auth endpoints (login, register, refresh token, Google login, password/email management)
2. Translate endpoints (sign-to-text, text-to-sign, audio-to-text, audio-to-sign)
3. Media endpoint
4. Admin endpoints
5. Health check

---

## My Role

This repository represents my part in a multi-person graduation project. I designed and built the **backend REST API**, responsible for:
- Authenticating and authorizing web and mobile clients
- Validating, rate-limiting, and securing all incoming requests
- Proxying translation requests to the AI model server and normalizing responses
- Persisting users, translation history, and media references via EF Core
- Deploying and operating the service via Docker, with centralized logging and health monitoring

The sign-language recognition/generation itself is handled by a separate AI model server maintained by teammates; this API does not perform inference directly.
