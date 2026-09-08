# Production Authentication Hardening Roadmap

## 1. Current Architecture & Limitations

The platform currently stores JWT Bearer tokens in the browser's `localStorage`:
- **Current Advantages**: Zero server-side state for authorization, seamless cross-origin communication, simple API integration.
- **Production Limitations & Risks**:
  1. **Cross-Site Scripting (XSS) Exposure**: Any JavaScript executed via a third-party dependency, compromised CDN, or client vulnerability can read tokens directly from `localStorage`.
  2. **No Instant Revocation**: Once issued, standard JWTs cannot be invalidated before expiration without tracking an explicit revocation list.
  3. **Long Token Lifespans**: To prevent frequent re-logins without refresh mechanisms, access tokens have a generous lifespan (24h), widening the window of vulnerability if a token is intercepted.

---

## 2. Target Production Architecture: HttpOnly Cookies + Refresh Tokens

To achieve enterprise-grade security, the following authentication architecture is recommended before production go-live:

```
Browser (Next.js Client)                ASP.NET Core API                 Redis / Database
        │                                      │                                 │
        │─── POST /api/auth/login ────────────>│                                 │
        │                                      │── Verify Credentials ──────────>│
        │                                      │<─ Valid User ───────────────────│
        │                                      │── Issue Short-Lived Access Token│
        │                                      │── Issue Rotated Refresh Token ─>│
        │<── Set-Cookie: access_token (15m) ───│                                 │
        │    HttpOnly, Secure, SameSite=Lax    │                                 │
        │<── Set-Cookie: refresh_token (7d) ───│                                 │
        │    HttpOnly, Secure, SameSite=Strict │                                 │
```

### Key Components:

### A. HttpOnly, Secure, SameSite Cookies
- **`HttpOnly`**: Prevents client-side JavaScript (`document.cookie`) from accessing tokens, effectively neutralizing token theft via XSS.
- **`Secure`**: Enforces that cookies are strictly transmitted over encrypted HTTPS connections.
- **`SameSite=Lax` / `SameSite=Strict`**: Restricts browser from sending cookies in cross-site contexts, mitigating Cross-Site Request Forgery (CSRF).

### B. Short-Lived Access Token + Refresh Token Rotation
- **Access Token**: Valid for 10–15 minutes. Contains user claims and permissions.
- **Refresh Token**: Valid for 7 days. Stored as an opaque hash in the database.
- **Automatic Rotation**: Each time a refresh token is used, it is invalidated and replaced with a new token. If an already-invalidated refresh token is presented, the entire family of refresh tokens is revoked immediately (detecting token theft).

### C. Server-Side Logout & Instant Revocation
- When a user logs out (`POST /api/auth/logout`), the refresh token is marked as revoked in the database.
- For immediate access token invalidation, the token `jti` (JWT ID) is written to Redis with a TTL matching the token's remaining lifespan. The API authentication handler verifies `jti` against Redis.

### D. CSRF Protection for Cookie Authentication
When transitioning from `Bearer` headers to cookies:
- Implement the ASP.NET Core Antiforgery system (`IAntiforgery`).
- Or utilize Double Submit Cookie pattern: the server sets a readable `XSRF-TOKEN` cookie, and the Next.js client attaches an `X-XSRF-TOKEN` header on mutating requests (`POST`, `PUT`, `DELETE`).

---

## 3. Implementation Checklist for Next Sprint

- [ ] Add `RefreshToken` entity linked to `User`.
- [ ] Configure `app.UseCookiePolicy()` and secure cookie options in ASP.NET Core API.
- [ ] Implement `POST /api/auth/refresh` endpoint with rotation and theft detection.
- [ ] Implement Redis-backed token blacklist for `POST /api/auth/logout`.
- [ ] Update Next.js frontend middleware to handle transparent cookie forwarding and automatic refresh on 401.
