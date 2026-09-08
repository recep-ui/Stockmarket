# Security Secret Rotation & Sanitization Guide

> [!CAUTION]
> **COMPROMISED CREDENTIAL NOTICE**
> Prior commits in this repository contained plain-text development credentials and default signing keys. In accordance with standard security incident response practices, **all previously committed credentials must be considered fully compromised** and must never be used in any public, staging, or production environments.

---

## 1. Compromised Secrets Inventory

The following credentials and keys appeared in historical commits and configuration files:

| Secret Type | Compromised Value | Historical Location | Status |
| :--- | :--- | :--- | :--- |
| **SQL Server SA Password** | `73237Sa.` | `src/BistQuant.API/appsettings.json` | **Compromised — Must be rotated immediately** |
| **SQL Server Fallback Password** | `YourStrong@Passw0rd;` | `src/BistQuant.Infrastructure/DependencyInjection.cs` | **Compromised — Removed from source** |
| **JWT Signing Key** | `BistQuantSuperSecretKeyForJwtTokenGeneration2026!` | `appsettings.json`, `Program.cs`, `JwtService.cs` | **Compromised — Must be rotated immediately** |
| **Demo User Password** | `Demo1234!` | `DatabaseInitializer.cs` | **Development Demo Only — Not for production** |

---

## 2. Immediate Rotation Procedures

### A. Rotating the JWT Signing Key
All currently issued tokens must be invalidated by replacing the JWT signing key:
1. Generate a new cryptographically secure 256-bit (or 512-bit) random key:
   ```bash
   # Using OpenSSL (Linux / macOS):
   openssl rand -base64 48

   # Or using PowerShell:
   [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
   ```
2. Set the generated key in your server environment variables or uncommitted `.env` file:
   ```env
   JWT_KEY="YOUR_NEW_SECURE_RANDOMLY_GENERATED_KEY_HERE"
   ```
3. Restart the API service. All legacy tokens will immediately fail cryptographic signature verification, forcing all sessions to re-authenticate securely.

### B. Rotating the SQL Server SA Password
If an active database was deployed using the old SA password:
1. Generate a strong, random password (minimum 16 characters including uppercase, lowercase, numbers, and symbols):
   ```bash
   openssl rand -base64 24
   ```
2. Connect to the SQL Server instance as administrator and execute:
   ```sql
   ALTER LOGIN sa WITH PASSWORD = 'NEW_STRONG_PASSWORD_HERE';
   GO
   ```
3. Update `.env` or Docker Secret:
   ```env
   MSSQL_SA_PASSWORD="NEW_STRONG_PASSWORD_HERE"
   ```
4. Restart the API and Worker containers with the updated connection string.

### C. Telegram Bot Token Rotation
If any Telegram Bot Token was created:
1. Open Telegram and contact `@BotFather`.
2. Send `/revoke` and select your bot.
3. BotFather will issue a new token.
4. Set the new token in `.env`:
   ```env
   TELEGRAM_BOT_TOKEN="123456789:ABCdefGHIjklMNOpqrSTUvwxYZ"
   ```

---

## 3. Configuration & Future Commit Safety

### Rules for Application Configuration:
1. **`appsettings.json`**: Must contain ONLY safe, non-sensitive defaults (empty strings or placeholders).
2. **`appsettings.Development.json`**: May contain explicitly named mock strings (e.g. `DEV_INSECURE_LOCAL_KEY_ONLY`).
3. **`appsettings.Production.json`**: Must NEVER contain raw passwords or keys; all production secrets must be injected through:
   - Environment variables (e.g. `ConnectionStrings__DefaultConnection`, `Jwt__Key`)
   - Docker secrets
   - Cloud Key Vaults / Secret Managers (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault).
4. **`.env`**: Must remain strictly included in `.gitignore`. Only `.env.example` with blank placeholders is tracked in source control.

### Recommended Pre-Commit Checks:
To prevent future leaks, install `gitleaks` or `git-secrets`:
```bash
# Install gitleaks:
brew install gitleaks # or download binary from GitHub

# Run detect before committing:
gitleaks detect --source . -v
```

---

## 4. Git History Sanitization (Optional Procedure)

> [!WARNING]
> Simply deleting a secret in a new commit does **NOT** remove it from the repository's past Git commit history. If the repository is ever made public or shared outside trusted circles, historical commits can be inspected.
> Rewriting Git history modifies commit hashes and requires all collaborators to re-clone the repository (`git push --force`).

If you decide to purge past commits from the Git history, execute the following procedure:

### Purging with `git-filter-repo` (Recommended):
```bash
# 1. Ensure working directory is clean and backed up:
git clone --bare https://github.com/recep-ui/Stockmarket.git repo-backup.git

# 2. Install git-filter-repo:
pip install git-filter-repo

# 3. Create a replace expressions file 'expressions.txt':
# 73237Sa.==>REDACTED_PASSWORD
# BistQuantSuperSecretKeyForJwtTokenGeneration2026!==>REDACTED_JWT_KEY

# 4. Run git-filter-repo:
git filter-repo --replace-text expressions.txt

# 5. Verify git log no longer contains the sensitive strings:
git log -S "73237Sa."

# 6. Force push rewritten history to remote (Coordinated team action):
# git push origin --force --all
```
