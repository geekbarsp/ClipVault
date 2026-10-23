# ClipVault licensing

ClipVault sends only a normalized license key, a SHA-256 device fingerprint, the requested licensing operation, and the app version to the `license` Edge Function. Clipboard history, images, file paths, titles, tags, settings, and copied content stay in `%LOCALAPPDATA%\ClipVault\clipvault.db` and `%LOCALAPPDATA%\ClipVault\Images`.

License keys are stored in Supabase only as peppered HMAC-SHA256 hashes. First activation atomically binds the license to one Windows installation. The returned offline token is signed with an ECDSA P-256 key held in Supabase Vault, then the desktop stores its credential encrypted with Windows DPAPI for the current Windows user. Offline access lasts at most the configured grace period (seven days by default).

## Bootstrap an administrator

1. In Supabase Authentication, create an email/password user and disable public sign-up for production.
2. In the SQL editor, run `insert into private.license_admins(user_id) values ('THE-AUTH-USER-UUID');`.
3. Serve the `admin` folder over HTTPS or localhost and sign in. Never add a service-role or secret key to this folder.

The admin UI creates licenses, suspends/reactivates/revokes them, changes expiration, resets an HWID, and displays minimal audit events. Newly created plaintext keys are returned only once.

## Deployment

Schema changes are in `supabase/migrations`. Both `license` and `admin-licenses` functions perform their own authentication and therefore use `verify_jwt = false`; the admin function independently verifies the Supabase Auth bearer token and checks `private.license_admins` before every operation.
