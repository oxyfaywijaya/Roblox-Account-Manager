# Robux Dashboard V1 (fork)

This fork adds a read-only Robux dashboard to Roblox Account Manager.

## Open the dashboard

1. Start Roblox Account Manager normally and unlock saved accounts.
2. Right-click an account and select **Robux Dashboard (F9)**, or press **F9**.
3. Balances load one account at a time. Click **Refresh Saldo** to retry.
4. Select an account and click **Lihat Riwayat Transaksi** to open Roblox's own transaction page in RAM's account-specific browser.

The dashboard shows the aggregate **only for accounts with successfully retrieved balances**. A failed lookup is **not** counted as zero. Account-specific transaction records are viewed on the official Roblox website, not scraped or stored by this dashboard.

## Security notes

- This feature does **not** collect, display, export, or log account cookies or passwords.
- It reuses the existing account session and read-only balance query.
- The transaction button uses a dedicated stricter browser mode: no `--disable-web-security` flag, no ignored HTTPS errors, no custom pre/post navigation scripts, no loaded extensions, and no configured proxies. Other upstream browser features retain their existing behavior.
- **Never commit** account data, .ROBLOSECURITY cookies, passwords, browser profiles, or exports with session information to a repository.
- The upstream project is archived and includes older dependencies. Review security and compatibility before using it for valuable accounts.
- The dashboard has not yet been compiled or tested on Windows in this PR.

## Next steps

If Roblox offers a suitable supported read-only transaction data interface, add a separate transaction table with per-account filters, robust pagination, error handling, and minimal retention. Do not expose bearer session material through an API or logs.
