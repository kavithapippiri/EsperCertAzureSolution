namespace ManualDesktopForm
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        // UI Controls
        private System.Windows.Forms.DataGridView dgvDevices;
        private System.Windows.Forms.Button btnLoadDevices;
        private System.Windows.Forms.Button btnUploadEsper;
        private System.Windows.Forms.Button btnEnrollDevices;
        private System.Windows.Forms.TextBox txtStatus;
        private System.Windows.Forms.TextBox txtPfxPassword;
        private System.Windows.Forms.TextBox txtEsperDeviceGroup;
        private System.Windows.Forms.TextBox txtEsperCertAlias;
        private System.Windows.Forms.Label lblPfxPassword;
        private System.Windows.Forms.Label lblEsperDeviceGroup;
        private System.Windows.Forms.Label lblEsperCertAlias;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            dgvDevices = new System.Windows.Forms.DataGridView();
            btnEnrollDevices = new System.Windows.Forms.Button();
            btnLoadDevices = new System.Windows.Forms.Button();
            btnUploadEsper = new System.Windows.Forms.Button();
            txtStatus = new System.Windows.Forms.TextBox();

            // Set a standard width and height for all buttons
            int buttonWidth = 250;
            int buttonHeight = 38;
            int buttonSpacing = 16;

            // Device Grid
            dgvDevices.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvDevices.Location = new System.Drawing.Point(12, 12);
            dgvDevices.Name = "dgvDevices";
            dgvDevices.RowHeadersWidth = 51;
            dgvDevices.Size = new System.Drawing.Size(1050, 160);
            dgvDevices.TabIndex = 0;

            // Calculate X/Y for centering three buttons below grid
            int totalWidth = (buttonWidth * 3) + (buttonSpacing * 2);
            int gridCenterX = dgvDevices.Location.X + (dgvDevices.Width / 2);
            int buttonsStartX = gridCenterX - (totalWidth / 2);
            int buttonsY = dgvDevices.Bottom + 18;

            // --- Enroll Devices Button ---
            btnEnrollDevices.Location = new System.Drawing.Point(buttonsStartX, buttonsY);
            btnEnrollDevices.Name = "btnEnrollDevices";
            btnEnrollDevices.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnEnrollDevices.TabIndex = 8;
            btnEnrollDevices.Text = "Enroll Devices from Esper";
            btnEnrollDevices.BackColor = System.Drawing.Color.DarkBlue;
            btnEnrollDevices.ForeColor = System.Drawing.Color.White;
            btnEnrollDevices.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnEnrollDevices.FlatAppearance.BorderSize = 0;
            btnEnrollDevices.UseVisualStyleBackColor = false;
            btnEnrollDevices.Click += btnEnrollDevices_Click;

            // --- Load Devices Button ---
            btnLoadDevices.Location = new System.Drawing.Point(buttonsStartX + buttonWidth + buttonSpacing, buttonsY);
            btnLoadDevices.Name = "btnLoadDevices";
            btnLoadDevices.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnLoadDevices.TabIndex = 7;
            btnLoadDevices.Text = "Load Devices/Refresh Devices";
            btnLoadDevices.BackColor = System.Drawing.Color.DarkBlue;
            btnLoadDevices.ForeColor = System.Drawing.Color.White;
            btnLoadDevices.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnLoadDevices.FlatAppearance.BorderSize = 0;
            btnLoadDevices.UseVisualStyleBackColor = false;
            btnLoadDevices.Click += btnLoadDevices_Click;

            // --- Upload Esper Button ---
            btnUploadEsper.Location = new System.Drawing.Point(buttonsStartX + 2 * (buttonWidth + buttonSpacing), buttonsY);
            btnUploadEsper.Name = "btnUploadEsper";
            btnUploadEsper.Size = new System.Drawing.Size(buttonWidth, buttonHeight);
            btnUploadEsper.TabIndex = 9;
            btnUploadEsper.Text = "Request Certificate and Upload to Esper";
            btnUploadEsper.BackColor = System.Drawing.Color.DarkBlue;
            btnUploadEsper.ForeColor = System.Drawing.Color.White;
            btnUploadEsper.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnUploadEsper.FlatAppearance.BorderSize = 0;
            btnUploadEsper.UseVisualStyleBackColor = false;
            btnUploadEsper.Click += btnRequestAndUploadCert_Click;

            // --- Status TextBox ---
            txtStatus.Location = new System.Drawing.Point(dgvDevices.Location.X, buttonsY + buttonHeight + 18);
            txtStatus.Multiline = true;
            txtStatus.Name = "txtStatus";
            txtStatus.ReadOnly = true;
            txtStatus.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtStatus.Size = new System.Drawing.Size(dgvDevices.Width, 120);
            txtStatus.TabIndex = 11;

            // Form
            this.ClientSize = new System.Drawing.Size(dgvDevices.Location.X + dgvDevices.Width + 20, txtStatus.Location.Y + txtStatus.Height + 30);
            this.Controls.Add(dgvDevices);
            this.Controls.Add(btnEnrollDevices);
            this.Controls.Add(btnLoadDevices);
            this.Controls.Add(btnUploadEsper);
            this.Controls.Add(txtStatus);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Manual Certificate Manager";
            ((System.ComponentModel.ISupportInitialize)dgvDevices).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion
    }
}
