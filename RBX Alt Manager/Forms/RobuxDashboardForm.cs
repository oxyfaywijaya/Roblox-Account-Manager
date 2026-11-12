using Newtonsoft.Json.Linq;
using RBX_Alt_Manager.Classes;
using RBX_Alt_Manager.Forms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RBX_Alt_Manager.Forms
{
    // Read-only dashboard: never displays or exports authentication cookies or passwords.
    public sealed class RobuxDashboardForm : Form
    {
        private readonly List<Account> accounts;
        private readonly DataGridView grid;
        private readonly Label totalLabel;
        private readonly Label infoLabel;
        private readonly Button refreshButton;
        private readonly Button historyButton;
        private readonly CancellationTokenSource closing = new CancellationTokenSource();
        private bool refreshing;

        public RobuxDashboardForm(IEnumerable<Account> currentAccounts)
        {
            accounts = (currentAccounts ?? Enumerable.Empty<Account>())
                .Where(account => account != null)
                .GroupBy(account => account.UserID)
                .Select(group => group.First())
                .ToList();

            Text = "Robux Dashboard | Roblox Account Manager";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(850, 520);
            MinimumSize = new Size(650, 360);
            BackColor = ThemeEditor.FormsBackground;
            ForeColor = ThemeEditor.FormsForeground;

            var header = new Panel { Dock = DockStyle.Top, Height = 72 };
            totalLabel = new Label
            {
                Text = "Total Robux: -",
                Dock = DockStyle.Top,
                Height = 38,
                Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
                Padding = new Padding(12, 8, 0, 0),
                ForeColor = ForeColor
            };
            infoLabel = new Label
            {
                Text = "Saldo hanya ditampilkan untuk sesi akun yang dapat diakses.",
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 2, 0, 0),
                ForeColor = ForeColor
            };
            header.Controls.Add(infoLabel);
            header.Controls.Add(totalLabel);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = BackColor
            };
            grid.Columns.Add("Username", "Username");
            grid.Columns.Add("UserId", "User ID");
            grid.Columns.Add("Robux", "Saldo Robux");
            grid.Columns.Add("Status", "Status");
            grid.Columns.Add("Updated", "Diperbarui");
            grid.Columns["Robux"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns["Status"].FillWeight = 145;
            grid.Columns["Updated"].FillWeight = 85;

            foreach (Account account in accounts)
            {
                int rowIndex = grid.Rows.Add(account.Username ?? "(unknown)", account.UserID, "-", "Belum diperiksa", "-");
                grid.Rows[rowIndex].Tag = account;
            }

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 56,
                Padding = new Padding(12, 10, 12, 7),
                WrapContents = false
            };
            var closeButton = new Button { Text = "Tutup", Width = 95, Height = 30 };
            closeButton.Click += (sender, args) => Close();

            historyButton = new Button
            {
                Text = "Lihat Riwayat Transaksi",
                Width = 190,
                Height = 30,
                Enabled = accounts.Count > 0
            };
            historyButton.Click += OpenTransactionHistory;

            refreshButton = new Button { Text = "Refresh Saldo", Width = 120, Height = 30 };
            refreshButton.Click += async (sender, args) => await RefreshBalancesAsync();

            footer.Controls.Add(closeButton);
            footer.Controls.Add(historyButton);
            footer.Controls.Add(refreshButton);

            Controls.Add(grid);
            Controls.Add(footer);
            Controls.Add(header);

            grid.SelectionChanged += (sender, args) =>
                historyButton.Enabled = !refreshing && grid.SelectedRows.Count > 0;

            Shown += async (sender, args) => await RefreshBalancesAsync();
            FormClosing += (sender, args) => closing.Cancel();
            FormClosed += (sender, args) => closing.Dispose();
        }

        private void OpenTransactionHistory(object sender, EventArgs e)
        {
            Account account = grid.SelectedRows.Count > 0
                ? grid.SelectedRows[0].Tag as Account
                : null;

            if (account == null)
            {
                MessageBox.Show("Pilih satu akun terlebih dahulu.", "Riwayat Robux",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Use RAM's existing account-specific browser. No token is copied to
            // clipboard, URL, logs, or another application.
            new AccountBrowser(account, "https://www.roblox.com/transactions");
        }

        private async Task RefreshBalancesAsync()
        {
            if (refreshing || closing.IsCancellationRequested)
                return;

            refreshing = true;
            refreshButton.Enabled = false;
            historyButton.Enabled = false;

            long total = 0;
            int success = 0;

            try
            {
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (closing.IsCancellationRequested || IsDisposed)
                        return;

                    var account = (Account)row.Tag;
                    row.Cells["Status"].Value = "Memuat...";
                    row.Cells["Robux"].Value = "-";

                    try
                    {
                        // Reuse existing authenticated, read-only account method.
                        JToken info = await account.GetMobileInfo();
                        JToken balance = info?["RobuxBalance"];

                        if (closing.IsCancellationRequested || IsDisposed)
                            return;

                        if (balance == null || !long.TryParse(balance.ToString(), out long amount) || amount < 0)
                        {
                            row.Cells["Status"].Value = "Tidak tersedia / login ulang";
                        }
                        else
                        {
                            row.Cells["Robux"].Value = amount.ToString("N0");
                            row.Cells["Status"].Value = "Berhasil";
                            total += amount;
                            success++;
                        }
                    }
                    catch (Exception)
                    {
                        if (closing.IsCancellationRequested || IsDisposed)
                            return;
                        row.Cells["Status"].Value = "Gagal mengambil saldo";
                    }

                    row.Cells["Updated"].Value = DateTime.Now.ToString("HH:mm:ss");
                    totalLabel.Text = $"Total Robux: {total:N0} R$";
                    infoLabel.Text = $"Saldo terbaca: {success}/{accounts.Count} akun. Tidak termasuk akun yang gagal diperiksa.";

                    // Avoid simultaneous requests across every saved account.
                    await Task.Delay(250, closing.Token);
                }

                if (accounts.Count == 0)
                    infoLabel.Text = "Belum ada akun yang disimpan. Tambahkan akun di jendela utama.";
            }
            catch (OperationCanceledException)
            {
                // The window was closed while a refresh was running.
            }
            finally
            {
                refreshing = false;
                if (!IsDisposed && !closing.IsCancellationRequested)
                {
                    refreshButton.Enabled = true;
                    historyButton.Enabled = grid.SelectedRows.Count > 0;
                }
            }
        }
    }
}
