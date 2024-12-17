using System;
using System.Data;
using System.Data.SQLite;
using System.Windows.Forms;

namespace TurnCalGUI
{
    public partial class Form1 : Form
    {
        private TextBox txtName, txtStartTime, txtEndTime;
        private DateTimePicker dtpDate;
        private ComboBox cmbWageLevel;
        private Button btnSave, btnDelete, btnEdit, btnOpenCalendar;
        private DataGridView dgvShifts;
        private string dbPath = "turncal.db";
        private string connectionString;
        private int selectedShiftId = -1;

        public Form1()
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

            // Input fields
            Label lblName = new Label() { Text = "Name:", Left = 20, Top = 20, Width = 100 };
            txtName = new TextBox() { Left = 120, Top = 20, Width = 200 };

            Label lblDate = new Label() { Text = "Date:", Left = 20, Top = 60, Width = 100 };
            dtpDate = new DateTimePicker() { Left = 120, Top = 60, Width = 200, Format = DateTimePickerFormat.Short };

            Label lblStartTime = new Label() { Text = "Start Time:", Left = 20, Top = 100, Width = 100 };
            txtStartTime = new TextBox() { Left = 120, Top = 100, Width = 200 };

            Label lblEndTime = new Label() { Text = "End Time:", Left = 20, Top = 140, Width = 100 };
            txtEndTime = new TextBox() { Left = 120, Top = 140, Width = 200 };

            Label lblWageLevel = new Label() { Text = "Wage Level:", Left = 20, Top = 180, Width = 100 };
            cmbWageLevel = new ComboBox()
            {
                Left = 120,
                Top = 180,
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbWageLevel.Items.AddRange(new object[] { "1", "2", "3" });
            cmbWageLevel.SelectedIndex = 0;

            // Buttons
            btnSave = new Button() { Text = "Save Shift", Left = 120, Top = 220, Width = 100 };
            btnSave.Click += BtnSave_Click;

            btnEdit = new Button() { Text = "Edit Shift", Left = 250, Top = 220, Width = 100 };
            btnEdit.Click += BtnEdit_Click;

            btnDelete = new Button() { Text = "Delete Shift", Left = 380, Top = 220, Width = 100 };
            btnDelete.Click += BtnDelete_Click;

            btnOpenCalendar = new Button() { Text = "Open Calendar", Left = 120, Top = 260, Width = 150 };
            btnOpenCalendar.Click += (sender, e) =>
            {
                CalendarForm calendarForm = new CalendarForm();
                calendarForm.ShowDialog();
            };

            dgvShifts = new DataGridView()
            {
                Left = 20,
                Top = 300,
                Width = 540,
                Height = 280,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            dgvShifts.SelectionChanged += DgvShifts_SelectionChanged;

            this.Controls.Add(lblName);
            this.Controls.Add(txtName);
            this.Controls.Add(lblDate);
            this.Controls.Add(dtpDate);
            this.Controls.Add(lblStartTime);
            this.Controls.Add(txtStartTime);
            this.Controls.Add(lblEndTime);
            this.Controls.Add(txtEndTime);
            this.Controls.Add(lblWageLevel);
            this.Controls.Add(cmbWageLevel);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnEdit);
            this.Controls.Add(btnDelete);
            this.Controls.Add(btnOpenCalendar);
            this.Controls.Add(dgvShifts);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                string name = txtName.Text.Trim();
                string date = dtpDate.Value.ToString("yyyy-MM-dd");
                string startTime = ValidateAndFormatTime(txtStartTime.Text.Trim());
                string endTime = ValidateAndFormatTime(txtEndTime.Text.Trim());
                int wageLevel = int.Parse(cmbWageLevel.SelectedItem.ToString());

                if (TimeSpan.Parse(startTime) >= TimeSpan.Parse(endTime))
                    throw new Exception("Start time must be earlier than end time.");

                int employeeID = GetOrCreateEmployeeID(name, wageLevel);

                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"INSERT INTO Shifts (EmployeeID, Name, Date, StartTime, EndTime) 
                                     VALUES (@id, @name, @date, @start, @end)";
                    using (var cmd = new SQLiteCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", employeeID);
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@date", date);
                        cmd.Parameters.AddWithValue("@start", startTime);
                        cmd.Parameters.AddWithValue("@end", endTime);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Shift saved successfully!");
                LoadShifts();
                ClearInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            if (selectedShiftId == -1)
            {
                MessageBox.Show("Please select a shift to edit.");
                return;
            }

            try
            {
                string name = txtName.Text.Trim();
                string date = dtpDate.Value.ToString("yyyy-MM-dd");
                string startTime = ValidateAndFormatTime(txtStartTime.Text.Trim());
                string endTime = ValidateAndFormatTime(txtEndTime.Text.Trim());

                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    string query = @"UPDATE Shifts 
                                     SET Name = @name, Date = @date, StartTime = @start, EndTime = @end 
                                     WHERE ID = @id";
                    using (var cmd = new SQLiteCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", selectedShiftId);
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@date", date);
                        cmd.Parameters.AddWithValue("@start", startTime);
                        cmd.Parameters.AddWithValue("@end", endTime);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Shift updated successfully!");
                LoadShifts();
                ClearInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (dgvShifts.SelectedRows.Count == 0) return;

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
            LoadShifts();
        }

        private void DgvShifts_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvShifts.SelectedRows.Count > 0)
            {
                selectedShiftId = Convert.ToInt32(dgvShifts.SelectedRows[0].Cells["ID"].Value);
                txtName.Text = dgvShifts.SelectedRows[0].Cells["Name"].Value.ToString();
                dtpDate.Value = DateTime.Parse(dgvShifts.SelectedRows[0].Cells["Date"].Value.ToString());
                txtStartTime.Text = dgvShifts.SelectedRows[0].Cells["StartTime"].Value.ToString();
                txtEndTime.Text = dgvShifts.SelectedRows[0].Cells["EndTime"].Value.ToString();
            }
        }

        private void LoadShifts()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = @"SELECT s.ID, e.Name, e.WageLevel, s.Date, s.StartTime, s.EndTime 
                                 FROM Shifts s 
                                 JOIN Employees e ON s.EmployeeID = e.ID";
                using (var adapter = new SQLiteDataAdapter(query, connection))
                {
                    DataTable dataTable = new DataTable();
                    adapter.Fill(dataTable);
                    dgvShifts.DataSource = dataTable;
                }
            }
        }

        private int GetOrCreateEmployeeID(string name, int wageLevel)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string selectQuery = "SELECT ID FROM Employees WHERE Name = @name";
                using (var selectCmd = new SQLiteCommand(selectQuery, connection))
                {
                    selectCmd.Parameters.AddWithValue("@name", name);
                    var result = selectCmd.ExecuteScalar();
                    if (result != null)
                        return Convert.ToInt32(result);
                }

                string insertQuery = "INSERT INTO Employees (Name, WageLevel) VALUES (@name, @level); SELECT last_insert_rowid();";
                using (var insertCmd = new SQLiteCommand(insertQuery, connection))
                {
                    insertCmd.Parameters.AddWithValue("@name", name);
                    insertCmd.Parameters.AddWithValue("@level", wageLevel);
                    return Convert.ToInt32(insertCmd.ExecuteScalar());
                }
            }
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

        private void ClearInputs()
        {
            txtName.Clear();
            txtStartTime.Clear();
            txtEndTime.Clear();
            dtpDate.Value = DateTime.Now;
            cmbWageLevel.SelectedIndex = 0;
        }

        private void InitializeDatabase()
        {
            if (!System.IO.File.Exists(dbPath))
                SQLiteConnection.CreateFile(dbPath);

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                string employeesTable = @"CREATE TABLE IF NOT EXISTS Employees (
                                            ID INTEGER PRIMARY KEY AUTOINCREMENT,
                                            Name TEXT UNIQUE NOT NULL,
                                            WageLevel INTEGER NOT NULL DEFAULT 1
                                        )";
                string shiftsTable = @"CREATE TABLE IF NOT EXISTS Shifts (
                                        ID INTEGER PRIMARY KEY AUTOINCREMENT,
                                        EmployeeID INTEGER NOT NULL,
                                        Name TEXT NOT NULL,
                                        Date TEXT NOT NULL,
                                        StartTime TEXT NOT NULL,
                                        EndTime TEXT NOT NULL,
                                        FOREIGN KEY (EmployeeID) REFERENCES Employees(ID)
                                    )";

                using (var cmd = new SQLiteCommand(employeesTable, connection))
                    cmd.ExecuteNonQuery();

                using (var cmd = new SQLiteCommand(shiftsTable, connection))
                    cmd.ExecuteNonQuery();
            }
        }
    }
}
