using System;
using System.Data;
using System.Data.SQLite;
using System.Windows.Forms;

namespace TurnCalGUI
{
    public class MainForm : Form
    {
        private TextBox txtName, txtStartTime, txtEndTime;
        private Button btnSave, btnDelete, btnUpdate;
        private DataGridView dgvShifts, dgvTotalHours; // Ny DataGridView for total timer per ansatt
        private Label lblTotalHours; // Label for samlet timer

        private string dbPath = "turncal.db";
        private string connectionString;
        private int selectedShiftId = -1;

        public MainForm()
        {
            connectionString = $"Data Source={dbPath};Version=3;";
            InitializeDatabase();
            InitializeComponents();
            LoadShifts();
        }

        private void InitializeComponents()
        {
            this.Text = "TurnCal Shift Planner";
            this.Size = new System.Drawing.Size(600, 700);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Inputfelt og knapper
            Label lblName = new Label() { Text = "Name:", Left = 20, Top = 20, Width = 100 };
            txtName = new TextBox() { Left = 120, Top = 20, Width = 200 };

            Label lblStartTime = new Label() { Text = "Start Time:", Left = 20, Top = 60, Width = 100 };
            txtStartTime = new TextBox() { Left = 120, Top = 60, Width = 200 };

            Label lblEndTime = new Label() { Text = "End Time:", Left = 20, Top = 100, Width = 100 };
            txtEndTime = new TextBox() { Left = 120, Top = 100, Width = 200 };

            btnSave = new Button() { Text = "Save Shift", Left = 120, Top = 140, Width = 100 };
            btnSave.Click += BtnSave_Click;

            btnUpdate = new Button() { Text = "Update Shift", Left = 250, Top = 140, Width = 100, Enabled = false };
            btnUpdate.Click += BtnUpdate_Click;

            btnDelete = new Button() { Text = "Delete Shift", Left = 380, Top = 140, Width = 100 };
            btnDelete.Click += BtnDelete_Click;

            // Hoved DataGridView for skift
            dgvShifts = new DataGridView()
            {
                Left = 20,
                Top = 200,
                Width = 540,
                Height = 200,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            dgvShifts.SelectionChanged += DgvShifts_SelectionChanged;

            // DataGridView for total timer per ansatt
            Label lblEmployeeTotal = new Label() { Text = "Total Hours per Employee:", Left = 20, Top = 420, Width = 300 };
            dgvTotalHours = new DataGridView()
            {
                Left = 20,
                Top = 450,
                Width = 540,
                Height = 150,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            this.Controls.Add(lblName);
            this.Controls.Add(txtName);
            this.Controls.Add(lblStartTime);
            this.Controls.Add(txtStartTime);
            this.Controls.Add(lblEndTime);
            this.Controls.Add(txtEndTime);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnUpdate);
            this.Controls.Add(btnDelete);
            this.Controls.Add(dgvShifts);
            this.Controls.Add(lblEmployeeTotal);
            this.Controls.Add(dgvTotalHours);
        }

        private string ValidateAndFormatTime(string input)
        {
            if (input.Length <= 2 && int.TryParse(input, out int hour))
                return $"{hour:D2}:00";
            else if (TimeSpan.TryParse(input, out TimeSpan time))
                return time.ToString(@"hh\:mm");
            else
                throw new FormatException("Invalid time format. Use HH or HH:mm (e.g., 08 or 08:00).");
        }

        private void LoadShifts()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT ID, Name, StartTime, EndTime FROM Shifts";
                using (var adapter = new SQLiteDataAdapter(query, connection))
                {
                    DataTable dataTable = new DataTable();
                    adapter.Fill(dataTable);
                    dgvShifts.DataSource = dataTable;
                }
            }
            UpdateEmployeeTotalHours();
        }

        private void UpdateEmployeeTotalHours()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = @"
                    SELECT Name, 
                           SUM((julianday(EndTime) - julianday(StartTime)) * 24) AS TotalHours
                    FROM Shifts
                    GROUP BY Name";

                using (var adapter = new SQLiteDataAdapter(query, connection))
                {
                    DataTable totalTable = new DataTable();
                    adapter.Fill(totalTable);
                    dgvTotalHours.DataSource = totalTable;
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                string name = txtName.Text.Trim();
                string startTime = ValidateAndFormatTime(txtStartTime.Text.Trim());
                string endTime = ValidateAndFormatTime(txtEndTime.Text.Trim());

                if (TimeSpan.Parse(startTime) >= TimeSpan.Parse(endTime))
                    throw new Exception("Start time must be earlier than end time.");

                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "INSERT INTO Shifts (Name, StartTime, EndTime) VALUES (@name, @start, @end)";
                    using (var cmd = new SQLiteCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@start", startTime);
                        cmd.Parameters.AddWithValue("@end", endTime);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Shift saved successfully!");
                ClearInputs();
                LoadShifts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (dgvShifts.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a shift to delete.");
                return;
            }

            try
            {
                int shiftId = Convert.ToInt32(dgvShifts.SelectedRows[0].Cells["ID"].Value);

                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "DELETE FROM Shifts WHERE ID = @id";
                    using (var cmd = new SQLiteCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", shiftId);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Shift deleted successfully!");
                ClearInputs();
                LoadShifts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            if (selectedShiftId == -1)
            {
                MessageBox.Show("Please select a shift to update.");
                return;
            }

            try
            {
                string name = txtName.Text.Trim();
                string startTime = ValidateAndFormatTime(txtStartTime.Text.Trim());
                string endTime = ValidateAndFormatTime(txtEndTime.Text.Trim());

                if (TimeSpan.Parse(startTime) >= TimeSpan.Parse(endTime))
                    throw new Exception("Start time must be earlier than end time.");

                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = "UPDATE Shifts SET Name = @name, StartTime = @start, EndTime = @end WHERE ID = @id";
                    using (var cmd = new SQLiteCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@start", startTime);
                        cmd.Parameters.AddWithValue("@end", endTime);
                        cmd.Parameters.AddWithValue("@id", selectedShiftId);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Shift updated successfully!");
                ClearInputs();
                LoadShifts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private void DgvShifts_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvShifts.SelectedRows.Count > 0)
            {
                selectedShiftId = Convert.ToInt32(dgvShifts.SelectedRows[0].Cells["ID"].Value);
                txtName.Text = dgvShifts.SelectedRows[0].Cells["Name"].Value.ToString();
                txtStartTime.Text = dgvShifts.SelectedRows[0].Cells["StartTime"].Value.ToString();
                txtEndTime.Text = dgvShifts.SelectedRows[0].Cells["EndTime"].Value.ToString();
                btnUpdate.Enabled = true;
            }
        }

        private void ClearInputs()
        {
            txtName.Clear();
            txtStartTime.Clear();
            txtEndTime.Clear();
            btnUpdate.Enabled = false;
            selectedShiftId = -1;
        }

        private void InitializeDatabase()
        {
            if (!System.IO.File.Exists(dbPath))
                SQLiteConnection.CreateFile(dbPath);

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = @"CREATE TABLE IF NOT EXISTS Shifts (
                                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                                    Name TEXT NOT NULL,
                                    StartTime TEXT NOT NULL,
                                    EndTime TEXT NOT NULL
                                )";
                using (var cmd = new SQLiteCommand(query, connection))
                    cmd.ExecuteNonQuery();
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
