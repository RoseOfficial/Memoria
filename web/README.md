# AlphaScope Web

Next.js 15 App Router web app. Consumes the AlphaScope v1 API.

## Local development

```bash
pnpm install
cp .env.example .env.local
# Edit .env.local to point at your local server (default http://localhost:5099)
pnpm dev
```

In another terminal, run the .NET server:

```bash
dotnet run --project ../AlphaScopeServer
```

Visit http://localhost:3000.

## Vercel deployment

One-time setup:

1. Create a new Vercel project pointing at this repository.
2. Set **Root Directory** to `web`.
3. Framework is auto-detected as Next.js.
4. Set environment variables:
   - `NEXT_PUBLIC_API_BASE_URL` = `https://api.alphascope.app` (or your server URL)
5. Add production domain `alphascope.app`.

Preview deploys work automatically on PRs; server CORS is configured to allow `https://*-alphascope.vercel.app`.

## Server-side checklist before launch

- `Cors:AllowedOrigins` includes `https://alphascope.app`
- `Cors:AllowedOriginPattern` matches Vercel preview URLs
- `Admin:DiscordUserIds` includes your Discord user ID
- `Discord:RedirectUri` points at `https://api.alphascope.app/v1/auth/discord/callback`
