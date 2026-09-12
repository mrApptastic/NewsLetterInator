# NewsLetterInator
A small  Application which utilizes your own Google Sheets and Gmail for Newsletter campaigns. 

## Technology

This is a proof-of-concept Blazor WebAssembly PWA that runs 100% client-side and can be deployed as static files to GitHub Pages.

After signing in with Google, the user can:
- list/create relevant Google Sheets
- read/write people rows in a selected sheet
- send email through Gmail API to selected recipients

## Architecture

This app has no backend.

- Client app: Blazor WebAssembly (.NET 10)
- Auth: Google Identity Services in browser via JS interop
- APIs called directly from browser with OAuth access token:
  - Google Sheets API
  - Google Drive API (only for sheet listing)
  - Gmail API
- Deployment target: GitHub Pages (GitHub Actions)

No database, no ASP.NET Core API, no server-side auth, no cloud backend.

## Project layout

- .github/workflows/deploy.yml
- Models/*
- Services/*
- Pages/Home.razor
- Pages/Sheets.razor
- Pages/People.razor
- Pages/Mail.razor
- wwwroot/index.html
- wwwroot/manifest.webmanifest
- wwwroot/service-worker.js
- wwwroot/service-worker.published.js
- wwwroot/js/googleAuth.js

## Google Cloud setup

1. Create a Google Cloud project.
2. Enable these APIs:
   - Google Sheets API
   - Gmail API
   - Google Drive API
3. Configure OAuth consent screen.
4. Create an OAuth Client ID of type Web application.
5. Add authorized JavaScript origins:
   - https://localhost:xxxx
   - https://<username>.github.io
6. For GitHub Pages repo path deployment, ensure app base URL is reachable at:
   - https://<username>.github.io/<repository>/

## Configuration

Set your OAuth Client ID in:
- wwwroot/appsettings.json
- optionally override for local in wwwroot/appsettings.Development.json

Example:

{
  "Google": {
    "ClientId": "YOUR_CLIENT_ID"
  }
}

OAuth Client ID for a web app is not a secret.
Never add any client secret to this repository.

## OAuth scopes

Scopes are centrally defined in Services/GoogleScopes.cs.

POC scopes:
- openid
- https://www.googleapis.com/auth/userinfo.profile
- https://www.googleapis.com/auth/userinfo.email
- https://www.googleapis.com/auth/spreadsheets
- https://www.googleapis.com/auth/gmail.send
- https://www.googleapis.com/auth/drive.file

Why drive.file is included:
- Needed to list relevant sheets through Drive files API in this POC.
- It is narrower than broad Drive scopes and limits file access to app-created/app-opened files.

## Local development

Run:

dotnet run

Then open the local HTTPS URL shown in terminal and ensure that exact origin is registered in Google OAuth authorized origins.

## PWA behavior

- manifest.webmanifest included
- service worker enabled
- installable app shell
- offline caching for static app assets

Google API requests are not cached by service worker:
- service worker cache logic is app-origin static assets only
- cross-origin API calls (googleapis.com) bypass cache

## GitHub Pages deployment

Workflow file:
- .github/workflows/deploy.yml

Pipeline steps:
1. Checkout
2. Setup .NET 10
3. Restore
4. Build
5. Publish
6. Set GitHub Pages base href to /<repository>/
7. Create 404.html SPA fallback
8. Create .nojekyll
9. Upload Pages artifact
10. Deploy

In repository settings, set Pages source to GitHub Actions.

## Known limitations

- This is a POC and not production hardened.
- Access token handling is browser-only and in-memory.
- drive.file scope only lists files this app can access via that scope.
- No retry/backoff policy yet for API quota errors.
- No server-side email auditing/logging.
