# Backend deployment

This API is published as a Docker image to GitHub Container Registry.

Default production image:

```text
ghcr.io/hedra-nabil/s2sai-backend:latest
```

## Build and push from Windows

Run from this `S2S_APi` directory:

```powershell
docker login ghcr.io -u Hedra-Nabil

$env:GHCR_OWNER = "hedra-nabil"
$env:TAG = "latest"

powershell -ExecutionPolicy Bypass -File .\scripts\build-push-ghcr.ps1
```

Expected output:

```text
BACKEND_IMAGE=ghcr.io/hedra-nabil/s2sai-backend:latest
```

## Update from Portainer

After pushing the image:

```text
1. Open Portainer over Tailscale.
2. Go to Containers.
3. Select the backend container.
4. Recreate the container and enable pulling the latest image.
5. Check Dozzle logs if the container restarts or returns 502 through Nginx.
```

The server compose file must point to the same image name:

```env
BACKEND_IMAGE=ghcr.io/hedra-nabil/s2sai-backend:latest
```

Production secrets are read from the server `.env`, not from appsettings files.
