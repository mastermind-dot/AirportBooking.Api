# Deployment

One origin. nginx serves the React build at `/` and proxies `/api` to the .NET
app on loopback. That shape is not incidental — it is what lets the refresh
cookie stay `SameSite=Strict` and makes CORS irrelevant. Splitting the two onto
separate hosts means revisiting both.

```
                    ┌──────────────────────────────┐
  browser ──HTTPS──▶│ nginx  flymaluaviation.com   │
                    │  /        → /var/www/…       │  React build
                    │  /api/*   → 127.0.0.1:5000   │──▶ Kestrel
                    └──────────────────────────────┘        │
                                                             ▼
                                                         PostgreSQL
```

## Files

| File | Goes to |
|---|---|
| `nginx/flymaluaviation.conf` | `/etc/nginx/sites-available/`, symlinked into `sites-enabled/` |
| `systemd/malu-api.service` | `/etc/systemd/system/` |

## Secrets

Never in `appsettings.Production.json` — that file is committed. They come from
`/etc/malu-api.env`, mode `600`, owned by root:

```
ConnectionStrings__Default=Host=localhost;Database=malu;Username=malu;Password=…
Jwt__SigningKey=<64+ random characters>
Stripe__SecretKey=sk_live_…
Stripe__WebhookSecret=whsec_…
```

The double underscore is how .NET maps an environment variable onto a nested
configuration key: `Jwt__SigningKey` sets `Jwt:SigningKey`.

Changing `Jwt__SigningKey` invalidates every issued token, so every user is
signed out. That is the correct response to a suspected leak, and worth knowing
before doing it casually.

## Deploying

```bash
# API
dotnet publish AirportBooking.Api -c Release -o /var/www/malu-api

# Migrations run as a deployment step, not at startup. Two instances starting
# together would otherwise race each other over the same schema.
dotnet ef database update --project AirportBooking.Infrastructure \
                          --startup-project AirportBooking.Api

systemctl restart malu-api

# Site
cd client && npm ci && npm run build
rsync -a --delete dist/ /var/www/flymaluaviation/
```

`client/.env.local` needs `VITE_STRIPE_PUBLISHABLE_KEY` set to the live
publishable key at build time. It is compiled into the bundle, which is correct
— that key is designed to be public. The secret key never goes near the client.

## Stripe webhook

Register `https://flymaluaviation.com/api/payments/webhook` in the Stripe
dashboard and put its signing secret in `Stripe__WebhookSecret`. That value is
**not** the one `stripe listen` prints in development; the CLI issues its own
per session.

Without it every webhook is rejected with a 400 signature failure, and bookings
sit `Pending` until the expiry sweep cancels them half an hour later. Payments
still take money, so this fails in the worst possible direction — check it
first if confirmed bookings stop appearing.

## What the app refuses to start without

Outside Development, startup fails fast rather than coming up degraded:

- `Stripe:SecretKey` and `Stripe:WebhookSecret` — a deployment that cannot take
  money is broken, not partially working
- `Cors:AllowedOrigins` — an empty allow-list silently blocks the real site,
  which looks like a broken deploy rather than a missing setting

It also warns, without refusing, if an allowed origin is `localhost` or plain
`http`.

## Before the first reload

Neither file below has ever been parsed by the software that runs it — both
were written on Windows. Check them on the server before trusting them:

```bash
nginx -t                                    # parses the site config
systemd-analyze verify /etc/systemd/system/malu-api.service
```

Two things in the nginx config are version-sensitive and worth knowing about:

- HTTP/2 is enabled with `listen 443 ssl http2` rather than the newer
  `http2 on;` directive. The newer form needs nginx 1.25.1+ and is a fatal
  error below it; Ubuntu 22.04 LTS still ships 1.18. On nginx 1.25.1+ the
  older form warns as deprecated but works, which is the safer failure.
- OCSP stapling is deliberately absent. It needs a `resolver` directive, and
  without one nginx warns once at startup and then serves every request
  unstapled — on rather than off in appearance only.

## Verifying a deploy

```bash
curl -sI https://flymaluaviation.com/api/airports | head -20
```

Expect `200`, `Strict-Transport-Security`, `X-Content-Type-Options: nosniff`,
and **no** `Server` header. Then check a wrong host is refused:

```bash
curl -s -o /dev/null -w '%{http_code}\n' -H 'Host: example.com' https://flymaluaviation.com/api/airports
```

Expect `400` — `AllowedHosts` rejecting it.
