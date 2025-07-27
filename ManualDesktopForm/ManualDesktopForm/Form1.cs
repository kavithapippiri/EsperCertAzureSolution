using ManualDesktopForm.Models;
using ManualDesktopForm.Services; // Ensure all your services are in this namespace
using Microsoft.Extensions.Logging; // For ILogger
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing; // Added for Color and Font
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms; // Standard WinForms namespace
using MongoDB.Bson;
using MongoDB.Driver;

namespace ManualDesktopForm
{
    public partial class Form1 : Form
    {
        private bool _certExpirySortAscending = true;
        private readonly ILogger<Form1> _logger;
        private readonly DeviceService _deviceService;
        private readonly QueueService _queueService;
        private readonly EsperDeviceApiService _esperDeviceApiService;
        private readonly CertificateService _certificateService;
        private readonly EsperContentService _esperContentService;
        private readonly ICertificateProcessor _certificateProcessor;
        private readonly EsperDeviceSyncService _esperDeviceSyncService;
        private readonly EsperSettings _esperSettings;
        private readonly AutoModeArgs _autoModeArgs;

        private CheckBox _headerCheckBox;

        public Form1(
            ILogger<Form1> logger,
            EsperDeviceSyncService esperDeviceSyncService,
            DeviceService deviceService,
            QueueService queueService,
            EsperDeviceApiService esperDeviceApiService,
            CertificateService certificateService,
            EsperContentService esperContentService,
            ICertificateProcessor certificateProcessor,
            AutoModeArgs autoModeArgs,
            EsperSettings esperSettings)
        {
            InitializeComponent();

            _logger = logger;
            _deviceService = deviceService;
            _esperDeviceSyncService = esperDeviceSyncService;
            _queueService = queueService;
            _esperDeviceApiService = esperDeviceApiService;
            _certificateService = certificateService;
            _esperContentService = esperContentService;
            _certificateProcessor = certificateProcessor;
            _autoModeArgs = autoModeArgs;
            _esperSettings = esperSettings;

            _headerCheckBox = new CheckBox();
            this.Load += new EventHandler(Form1_Load);
            dgvDevices.AutoGenerateColumns = false;
            dgvDevices.ColumnHeaderMouseClick += dgvDevices_ColumnHeaderMouseClick;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            dgvDevices.Width = 1500;
            dgvDevices.EnableHeadersVisualStyles = false;
            dgvDevices.ColumnHeadersDefaultCellStyle.BackColor = Color.DarkBlue;
            dgvDevices.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvDevices.ColumnHeadersDefaultCellStyle.Font = new Font(dgvDevices.ColumnHeadersDefaultCellStyle.Font, FontStyle.Bold);

            btnLoadDevices.BackColor = Color.DarkBlue;
            btnLoadDevices.ForeColor = Color.White;
            btnLoadDevices.FlatStyle = FlatStyle.Flat;
            btnLoadDevices.FlatAppearance.BorderSize = 0;

            btnUploadEsper.BackColor = Color.DarkBlue;
            btnUploadEsper.ForeColor = Color.White;
            btnUploadEsper.FlatStyle = FlatStyle.Flat;
            btnUploadEsper.FlatAppearance.BorderSize = 0;
        }

        private void AddSelectColumnIfMissing()
        {
            if (!dgvDevices.Columns.Contains("Select"))
            {
                var selectColumn = new DataGridViewCheckBoxColumn
                {
                    Name = "Select",
                    HeaderText = "",
                    Width = 30,
                    ReadOnly = false,
                    TrueValue = true,
                    FalseValue = false
                };
                dgvDevices.Columns.Insert(0, selectColumn);
            }
        }

        private async Task<int> EnrollDevicesFromEsperAndSaveToMongoAsync()
        {
            // These should come from your config/services:
            string esperApiKey = _esperSettings.ApiKey;
            string esperBaseUrl = _esperSettings.BaseUrl; // Make sure it ends with /api/v0/ or whatever is needed
            string enterpriseId = _esperSettings.EnterpriseId;
            string mongoConnStr = "mongodb://localhost:27017"; // or get from your MongoSettings
            string mongoDbName = "certmanager";
            string mongoCollName = "devices";

            using var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(esperBaseUrl);
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", esperApiKey);

            string url = $"enterprise/{enterpriseId}/device/";
            var resp = await httpClient.GetAsync(url);

            if (!resp.IsSuccessStatusCode)
            {
                string errorContent = await resp.Content.ReadAsStringAsync();
                throw new Exception($"Esper API request failed: {errorContent}");
            }

            string json = await resp.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<DeviceListResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (list?.results == null || list.results.Count == 0)
                return 0;

            // Connect to MongoDB
            var mongoClient = new MongoClient(mongoConnStr);
            var db = mongoClient.GetDatabase(mongoDbName);
            var collection = db.GetCollection<BsonDocument>(mongoCollName);

            int devicesProcessed = 0;
            foreach (var d in list.results)
            {
                var filter = Builders<BsonDocument>.Filter.Eq("id", d.id);

                var update = Builders<BsonDocument>.Update
                    .Set("id", d.id)
                    .Set("device_name", d.device_name != null ? (BsonValue)d.device_name : BsonNull.Value)
                    .Set("device_model", d.device_model != null ? (BsonValue)d.device_model : BsonNull.Value)
                    .Set("status", d.status)
                    .Set("serialNumber", d.hardwareInfo?.serialNumber != null ? (BsonValue)d.hardwareInfo.serialNumber : BsonNull.Value)
                    .Set("enrolled_on", d.created_on != default ? (BsonValue)d.created_on : BsonNull.Value)
                    .Set("updated_on", d.updated_on != default ? (BsonValue)d.updated_on : BsonNull.Value)
                    .SetOnInsert("certificate", BsonNull.Value);

                var options = new UpdateOptions { IsUpsert = true };

                try
                {
                    await collection.UpdateOneAsync(filter, update, options);
                    devicesProcessed++;
                }
                catch (MongoWriteException writeEx)
                {
                    // log or handle error
                }
                catch (Exception ex)
                {
                    // log or handle error
                }
            }
            return devicesProcessed;
        }

        private async void btnEnrollDevices_Click(object sender, EventArgs e)
        {
            try
            {
                txtStatus.AppendText("Enrolling devices from Esper API...\r\n");
                int enrolled = await EnrollDevicesFromEsperAndSaveToMongoAsync();
                txtStatus.AppendText($"Enrolled {enrolled} device(s) from Esper API into MongoDB.\r\n");
            }
            catch (Exception ex)
            {
                txtStatus.AppendText($"Error enrolling devices: {ex.Message}\r\n");
            }
        }


        /*    private async void btnLoadDevices_Click(object sender, EventArgs e)
            {
                try
                {
                    ClearSelectAllCheckbox();
                    txtStatus.AppendText("Loading and synchronizing devices...");
                    _logger.LogInformation("Starting device load and sync process.");

                    int syncedCount = await _esperDeviceSyncService.SyncDevicesFromEsperAsync();
                    txtStatus.AppendText($"Synchronized {syncedCount} devices from Esper API into MongoDB.");
                    _logger.LogInformation($"Synchronized {syncedCount} devices from Esper API into MongoDB.");

                    var devicesToDisplay = await _deviceService.GetDevicesAsync();
                    txtStatus.AppendText($"Loaded {devicesToDisplay.Count} device(s) from local database for display.");
                    _logger.LogInformation($"Loaded {devicesToDisplay.Count} devices from local database for display.");

                    dgvDevices.DataSource = null;
                    dgvDevices.Columns.Clear();
                    dgvDevices.DataSource = devicesToDisplay;

                    AddSelectColumnIfMissing();
                    ConfigureDataGridViewColumns();
                    AddSelectAllCheckbox();

                    txtStatus.AppendText("Finished loading and syncing devices.");
                    _logger.LogInformation("Device load and sync process completed.");

                    DisplayDeviceProcessingStatuses(devicesToDisplay);
                }
                catch (Exception ex)
                {
                    txtStatus.AppendText($"Error loading and syncing devices: {ex.Message}");
                    _logger.LogError(ex, "Error loading and syncing devices in UI.");
                    MessageBox.Show($"Error loading devices: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            } */

        private async void btnLoadDevices_Click(object sender, EventArgs e)
        {
            try
            {
                ClearSelectAllCheckbox();

                txtStatus.AppendText("Loading devices from local MongoDB...\r\n");
                _logger.LogInformation("Loading devices from local MongoDB.");

                // ONLY load devices from your MongoDB, do NOT sync from Esper!
                var devicesToDisplay = await _deviceService.GetDevicesAsync();

                txtStatus.AppendText($"Loaded {devicesToDisplay.Count} device(s) from local database for display.\r\n");
                _logger.LogInformation($"Loaded {devicesToDisplay.Count} devices from local database for display.");

                dgvDevices.DataSource = null;
                dgvDevices.Columns.Clear();
                dgvDevices.DataSource = devicesToDisplay;

                AddSelectColumnIfMissing();
                ConfigureDataGridViewColumns();
                AddSelectAllCheckbox();

                txtStatus.AppendText("Finished loading devices from local database.\r\n");
                _logger.LogInformation("Device load process completed.");

                DisplayDeviceProcessingStatuses(devicesToDisplay);
            }
            catch (Exception ex)
            {
                txtStatus.AppendText($"Error loading devices: {ex.Message}\r\n");
                _logger.LogError(ex, "Error loading devices in UI.");
                MessageBox.Show($"Error loading devices: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void dgvDevices_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var column = dgvDevices.Columns[e.ColumnIndex];
            if (column.DataPropertyName == "CertificateExpiryDate")
            {
                if (dgvDevices.DataSource is List<Device> deviceList)
                {
                    List<Device> sorted;
                    if (_certExpirySortAscending)
                    {
                        sorted = deviceList.OrderBy(d => d.CertificateExpiryDate ?? DateTime.MaxValue).ToList();
                    }
                    else
                    {
                        sorted = deviceList.OrderByDescending(d => d.CertificateExpiryDate ?? DateTime.MinValue).ToList();
                    }
                    _certExpirySortAscending = !_certExpirySortAscending;
                    dgvDevices.DataSource = null;
                    dgvDevices.DataSource = sorted;
                    AddSelectColumnIfMissing();
                    ConfigureDataGridViewColumns();
                    AddSelectAllCheckbox();
                }
            }
        }

        /* private async void btnLoadDevices_Click(object sender, EventArgs e)
         {
             try
             {
                 ClearSelectAllCheckbox();

                 txtStatus.AppendText("Loading devices...\r\n");
                 var devices = await _deviceService.GetDevicesAsync();

                 dgvDevices.DataSource = null;
                 dgvDevices.Columns.Clear();
                 dgvDevices.DataSource = devices;

                 // Add checkbox column
                 var checkboxColumn = new DataGridViewCheckBoxColumn();
                 checkboxColumn.HeaderText = "";
                 checkboxColumn.Name = "Select";
                 checkboxColumn.Width = 50;
                 dgvDevices.Columns.Insert(0, checkboxColumn);

                 AddSelectAllCheckbox();

                 txtStatus.AppendText($"Loaded {devices.Count} device(s).\r\n");
             }
             catch (Exception ex)
             {
                 txtStatus.AppendText($"Error: {ex.Message}\r\n");
             }
         } */

        private void ConfigureDataGridViewColumns()
        {
            AddOrUpdateColumn("DeviceId", "Device ID", 300);
            AddOrUpdateColumn("DeviceName", "Device Name");
            AddOrUpdateColumn("Thumbprint", "Thumbprint", 300); // <- Add this column
            // Hide Serial Number column in the grid, but keep it in your business/data
            AddOrUpdateColumn("SerialNumber", "Serial Number", 150, false); // <- HIDDEN            AddOrUpdateColumn("Status", "Status", 50);
            AddOrUpdateColumn("EnrolledOn", "Enrolled On", 150);
            AddOrUpdateColumn("CertificateExpiryDate", "Cert Expiry Date", 150, true, DataGridViewColumnSortMode.Programmatic);
            AddOrUpdateColumn("LastRenewedOn", "Last Renewed On", 150, false);
            AddOrUpdateColumn("ProcessingStatus", "Processing Status", 100);
            AddOrUpdateColumn("LastProcessingMessage", "Last Message", 300);
            AddOrUpdateColumn("LastProcessedOn", "Last Processed On", 150);
            AddOrUpdateColumn("Id", "MongoDB ID", 100, false);
            AddOrUpdateColumn("DeviceModel", "Device Model", 100, false);
            AddOrUpdateColumn("Certificate", "Certificate Content", 100, false);
            AddOrUpdateColumn("UpdatedOn", "Esper Updated On", 100, false);
        }
        private void AddOrUpdateColumn(string dataPropertyName, string headerText, int width = 100, bool visible = true, DataGridViewColumnSortMode sortMode = DataGridViewColumnSortMode.Automatic)
        {
            if (!dgvDevices.Columns.Contains(dataPropertyName))
            {
                DataGridViewColumn column = new DataGridViewTextBoxColumn
                {
                    DataPropertyName = dataPropertyName,
                    Name = dataPropertyName,
                    HeaderText = headerText,
                    Width = width,
                    Visible = visible,
                    SortMode = sortMode
                };
                dgvDevices.Columns.Add(column);
            }
            else
            {
                dgvDevices.Columns[dataPropertyName].HeaderText = headerText;
                dgvDevices.Columns[dataPropertyName].Width = width;
                dgvDevices.Columns[dataPropertyName].Visible = visible;
                dgvDevices.Columns[dataPropertyName].SortMode = sortMode;
            }
        }

       

        private void DisplayDeviceProcessingStatuses(List<Device> devicesToDisplay)
        {
            if (devicesToDisplay != null && devicesToDisplay.Any())
            {
                txtStatus.AppendText("--- Device Processing Statuses (from local DB) ---");
                foreach (var device in devicesToDisplay)
                {
                    if (!string.IsNullOrWhiteSpace(device.ProcessingStatus))
                    {
                        string statusLine = $"Device {device.DeviceId}: Status - {device.ProcessingStatus}";
                        if (!string.IsNullOrWhiteSpace(device.LastProcessingMessage))
                        {
                            statusLine += $" | Message - {device.LastProcessingMessage}";
                        }
                        if (device.LastProcessedOn.HasValue)
                        {
                            statusLine += $" | Last Updated - {device.LastProcessedOn.Value.ToLocalTime()}";
                        }
                        txtStatus.AppendText($"{statusLine}");
                    }
                }
                txtStatus.AppendText("--------------------------------------------------");
            }
        }

        private void ClearSelectAllCheckbox()
        {
            if (_headerCheckBox != null)
            {
                _headerCheckBox.Checked = false;
            }

            if (dgvDevices.Columns.Contains("Select"))
            {
                foreach (DataGridViewRow row in dgvDevices.Rows)
                {
                    if (row.Cells["Select"] is DataGridViewCheckBoxCell checkboxCell)
                    {
                        checkboxCell.Value = false;
                    }
                }
            }
        }

        private void AddSelectAllCheckbox()
        {
            if (_headerCheckBox != null)
            {
                this.dgvDevices.Controls.Remove(_headerCheckBox);
            }

            _headerCheckBox.Size = new Size(15, 15);
            _headerCheckBox.Name = "chkSelectAllHeader";

            if (dgvDevices.Columns.Contains("Select"))
            {
                DataGridViewColumn selectColumn = dgvDevices.Columns["Select"];
                Rectangle headerCellRect = dgvDevices.GetCellDisplayRectangle(selectColumn.Index, -1, true);

                _headerCheckBox.Location = new Point(
                    headerCellRect.X + (headerCellRect.Width / 2) - (_headerCheckBox.Width / 2),
                    headerCellRect.Y + (headerCellRect.Height / 2) - (_headerCheckBox.Height / 2)
                );
            }
            else
            {
                _logger.LogWarning("AddSelectAllCheckbox: 'Select' column not found, placing checkbox at (5,5).");
                _headerCheckBox.Location = new Point(5, 5);
            }

            _headerCheckBox.CheckedChanged += HeaderCheckBox_CheckedChanged;
            dgvDevices.Controls.Add(_headerCheckBox);
        }

        private void HeaderCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            bool isChecked = ((CheckBox)sender).Checked;
            if (dgvDevices.Columns.Contains("Select"))
            {
                foreach (DataGridViewRow row in dgvDevices.Rows)
                {
                    if (row.Cells["Select"] is DataGridViewCheckBoxCell checkboxCell)
                    {
                        checkboxCell.Value = isChecked;
                    }
                }
            }
            dgvDevices.Invalidate();
        }

        private async void btnRequestAndUploadCert_Click(object sender, EventArgs e)
        {
            var selectedDeviceIds = dgvDevices.Rows
                .Cast<DataGridViewRow>()
                .Where(row => Convert.ToBoolean(row.Cells["Select"].Value))
                .Select(row => ((Device)row.DataBoundItem).DeviceId)
                .ToList();

            if (!selectedDeviceIds.Any())
            {
                txtStatus.AppendText("No devices selected for certificate generation/upload.");
                _logger.LogInformation("No devices selected for certificate generation/upload.");
                return;
            }

            var request = new RequestCertRequest
            {
                deviceIds = selectedDeviceIds,
                useSelfSigned = _esperSettings.UseSelfSigned,
            };

            txtStatus.AppendText($"Sending request: [\"deviceIds\":[\"{string.Join("\",\"", selectedDeviceIds)}\"],\"useSelfSigned\":\"{_esperSettings.UseSelfSigned}\"]");
            await _queueService.EnqueueDevicesAsync(request, log => txtStatus.AppendText(log + ""));
            txtStatus.AppendText("Enqueued to queue and loaded successfully");

            btnLoadDevices_Click(sender, e);
        }



        public async Task RunAutoFlowAsync()
        {
            _logger.LogInformation("Starting auto flow...");

            try
            {
                await _esperDeviceSyncService.SyncDevicesFromEsperAsync();
                var devices = await _deviceService.GetDevicesAsync();
                var selectedDeviceIds = devices.Select(device => device.DeviceId).ToList();

                if (!selectedDeviceIds.Any())
                {
                    _logger.LogInformation("No devices found for certificate generation/upload in auto mode.");
                    return;
                }

                var request = new RequestCertRequest
                {
                    deviceIds = selectedDeviceIds,
                    useSelfSigned = _esperSettings.UseSelfSigned,
                };

                _logger.LogInformation($"Submitting {selectedDeviceIds.Count} devices for certificate generation/upload: [{string.Join(", ", selectedDeviceIds)}]");
                await _queueService.EnqueueDevicesAsync(request, null);

                _logger.LogInformation("Automated run completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto flow failed.");
            }
        }

        private void Form1_Load_1(object sender, EventArgs e)
        {

        }
    }
}
