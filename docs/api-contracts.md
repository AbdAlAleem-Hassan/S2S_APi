# API Contracts and AI Server Notes

## .NET Backend Endpoints

### POST /api/v1/Translate/audio-to-text
- Purpose: Speech-to-text only (STT in .NET). Returns text for user review.
- Content-Type: multipart/form-data
- Form fields:
  - audio_file (binary, required)
  - language (string, optional, default: "ar")
- Behavior:
  - .NET performs STT using Groq Whisper (model: whisper-large-v3).
  - Response contains the recognized text only.

### POST /api/v1/Translate/audio-to-sign
- Purpose: Audio to sign translation (STT in .NET -> AI Server text-to-sign).
- Content-Type: multipart/form-data
- Form fields:
  - audio_file (binary, required)
  - avatar (string, optional, default: "default")
  - speed (string, optional, default: "1.0")
  - output_format (string, optional, default: "pose")
- Behavior:
  - .NET performs STT using Groq Whisper (model: whisper-large-v3).
  - Recognized text is sent to AI Server /translate/to-sign.

### POST /api/v1/Translate/text-to-sign
- Purpose: Text to sign translation (AI Server text-to-sign).
- Content-Type: multipart/form-data
- Form fields:
  - text (string, required)
  - avatar (string, optional, default: "default")
  - speed (string, optional, default: "1.0")
  - output_format (string, optional, default: "pose")

### POST /api/v1/Translate/sign-to-text
- Purpose: Sign video to text (AI Server sign-to-text) with optional TTS on .NET.
- Content-Type: multipart/form-data
- Form fields:
  - video_file (binary, required)
  - include_audio (bool, optional, default: false)
  - language (string, optional, default: "ar")
- Behavior:
  - If include_audio=true, .NET generates TTS audio and returns audio_url.
  - Video size limit: 50 MB.
  - Allowed video extensions: .mp4, .mov, .webm, .avi, .mkv, .m4v

### GET /api/v1/media/{type}/{fileName}
- Purpose: Serve stored media (pose/sigml/audio/video/profile) from wwwroot.

### POST /api/v1/Auth/UpdateProfile
- Purpose: Update display name and phone number.
- Authorization: Bearer JWT
- Content-Type: application/json
- Body:
  - displayName (string, required)
  - phoneNumber (string, optional)
- Response:
  - { "displayName": "...", "phoneNumber": "...", "profileImageUrl": "..." }

## AI Server Contract (Updated)

### POST /api/v1/translate/to-sign
- Accepts text only (no audio).
- Content-Type: application/x-www-form-urlencoded
- Fields:
  - text (string, required)
  - output_format (string, optional, default: "pose")
  - speed (string, optional, default: "1.0")
  - avatar (string, optional)

### POST /api/v1/translate/sign-to-text
- Accepts video only (no include_audio, no language).
- Content-Type: multipart/form-data
- Fields:
  - video_file (binary, required)
- Response does NOT include audio_url.

### AI Server Media Outputs
- pose_url: file extension .pose (binary pose landmarks)
- sigml_content: XML text content (no download needed)
- video_url: optional MP4 video file (if output_format requests video)

## Rate Limits

- auth-limit: 5 requests / minute
- otp-request-limit: 3 requests / 10 minutes per IP (ResendOtp uses IP+email)
- otp-verify-limit: 5 requests / 10 minutes per IP
- change-password-limit: 5 requests / 5 minutes
- stt-limit (audio-to-text, audio-to-sign): 10 requests / minute per IP

## STT/TTS Settings

- GROQ_API_KEY: required for STT
- GOOGLE_APPLICATION_CREDENTIALS: required for Google TTS
- SttSettings:TimeoutSeconds (default 30)
- Groq:Endpoint (optional override, default Groq transcription endpoint)
- TtsSettings:LanguageCode (default ar-XA)
- TtsSettings:VoiceName (default ar-XA-Wavenet-D)
