# Memoria Web

Next.js 15 App Router web app. Consumes the Memoria v1 API.

## Local development

```bash
pnpm install
cp .env.example .env.local
# Edit .env.local to point at your local server (default http://localhost:5099)
pnpm dev
```

In another terminal, run the .NET server:

```bash
dotnet run --project ../MemoriaServer
```

Visit http://localhost:3000.

## Netlify deployment

One-time setup:

1. Create a new Netlify site pointing at this repository.
2. **Base directory:** `web`
3. **Build command:** `pnpm build`
4. **Publish directory:** `web/.next`
5. Framework is auto-detected as Next.js. Netlify injects `@netlify/plugin-nextjs` at build time (also listed in `web/netlify.toml`).
6. Site name: pick `memoriagg` (so the Netlify subdomain is `memoriagg.netlify.app` and preview URLs follow `<preview>--memoriagg.netlify.app`, matching the CORS regex).
7. Environment variables (Site configuration → Environment variables):
   - `NEXT_PUBLIC_API_BASE_URL` = `https://api.memoria.gg` (or your server URL)
8. Add custom domain `memoria.gg` (Site configuration → Domain management).

Deploy previews for PRs and branch deploys both use `https://<slug>--memoriagg.netlify.app`; server CORS is configured to allow that pattern.

## Server-side checklist before launch

- `Cors:AllowedOrigins` includes `https://memoria.gg`
- `Cors:AllowedOriginPattern` matches Netlify preview URLs (default: `^https://[a-z0-9-]+--memoriagg\.netlify\.app$`)
- `Admin:DiscordUserIds` includes your Discord user ID
- `Discord:RedirectUri` points at `https://api.memoria.gg/v1/auth/discord/callback`
