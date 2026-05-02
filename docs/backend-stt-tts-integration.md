# Backend STT/TTS Integration Notes

## 1) Goal and scope
- Move STT (speech-to-text) and TTS (text-to-speech) into the .NET backend.
- Keep the AI server focused on Text <-> Sign only.
- Return text to the client for review before sending it to the AI server.

## 2) What was implemented
- New STT service: Groq Whisper integration.
- New TTS service: Google Cloud Text-to-Speech integration.
- New endpoint: audio-to-text (returns text only for user review).
- Updated endpoints:
  - audio-to-sign now performs STT locally, then calls AI server text-to-sign.
  - sign-to-text calls AI server, then generates audio locally when include_audio=true.
- Updated AI server contract: text-only to-sign, video-only sign-to-text.
- Added rate limits for STT paths and a configurable STT timeout.
- Updated documentation and tests.

## 3) Security and performance review
- File validation
  - Audio max size: 20 MB.
  - Video max size: 50 MB.
  - Audio allowed extensions: .mp3, .wav, .m4a, .ogg, .webm, .mp4, .mpeg.
  - Video allowed extensions: .mp4, .mov, .webm, .avi, .mkv, .m4v.
  - Video content types are checked against a safe allow-list.
  - Magic-bytes validation is enforced for audio/video uploads.
- Media cleanup
  - wwwroot/media files are cleaned hourly.
  - Retention window: 7 days.
- Rate limiting
  - stt-limit: 10 requests/min per IP.
  - auth-limit and change-password-limit unchanged.
- Timeouts
  - STT timeout is bounded (5-120s) via SttSettings:TimeoutSeconds.
- TTS resiliency
  - If TTS fails, the API still returns text, logs a warning.
- Secrets
  - Dev: User Secrets.
  - Prod: Environment variables / secret manager (do not store in appsettings).
- Known security gaps to consider
  - Media files are persisted under wwwroot; ensure the cleanup settings remain enabled in production.

## 4) Backend endpoints (for frontend team)

### POST /api/v1/Translate/audio-to-text
- Purpose: STT only (returns text for review)
- Content-Type: multipart/form-data
- Form fields:
  - audio_file (required)
  - language (optional, default: ar)
- Response:
  - { "text": "..." }

### POST /api/v1/Translate/text-to-sign
- Purpose: Text -> Sign (AI server)
- Content-Type: multipart/form-data
- Form fields:
  - text (required)
  - avatar (optional, default: "default")
  - speed (optional, default: "1.0")
  - output_format (optional, default: "pose")
- Response: AI server sign output (pose_url, sigml_content, or video_url)

### POST /api/v1/Translate/audio-to-sign
- Purpose: Audio -> Text (STT) -> Sign (AI server)
- Content-Type: multipart/form-data
- Form fields:
  - audio_file (required)
  - avatar (optional, default: "default")
  - speed (optional, default: "1.0")
  - output_format (optional, default: "pose")
- Response: AI server sign output

### POST /api/v1/Translate/sign-to-text
- Purpose: Sign video -> Text (AI server) + optional TTS
- Content-Type: multipart/form-data
- Form fields:
  - video_file (required)
  - include_audio (optional, default: false)
  - language (optional, default: "ar")
- Behavior:
  - If include_audio=true, backend generates audio_url with Google TTS.
- Response:
  - text + optional audio_url

### GET /api/v1/media/{type}/{fileName}
- Purpose: Serve stored media (pose/sigml/audio)

## 5) AI server contract (for AI team)

### POST /api/v1/translate/to-sign
- Accepts: text only (no audio)
- Content-Type: application/x-www-form-urlencoded
- Fields:
  - text (required)
  - output_format (optional, default: pose)
  - speed (optional, default: 1.0)
  - avatar (optional)

### POST /api/v1/translate/sign-to-text
- Accepts: video only (no include_audio, no language)
- Content-Type: multipart/form-data
- Fields:
  - video_file (required)
- Response: text only, no audio_url

AI server media outputs:
- pose_url -> .pose file
- sigml_content -> XML string
- video_url -> MP4 video when output_format requests video

## 6) Configuration and secrets
- STT
  - Groq:ApiKey or GROQ_API_KEY
  - SttSettings:TimeoutSeconds (default 30)
  - Groq:Endpoint (optional override)
- TTS
  - Google credentials via User Secrets or environment variables
  - TtsSettings:LanguageCode (default ar-XA)
  - TtsSettings:VoiceName (default ar-XA-Wavenet-D)


