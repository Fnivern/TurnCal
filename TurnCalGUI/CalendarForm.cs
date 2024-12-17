using System;
using System.Data;
using System.Data.SQLite;
using System.Windows.Forms;

namespace TurnCalGUI
{
    public class CalendarForm : Form
    {
        private MonthCalendar calendar;
        private DataGridView dgvShifts;
        private Label lblTotalHours;    // Label for å vise total timer denne måneden
        private Label lblTotalSalary;   // Label for å vise total lønn denne måneden
        private string connectionString = "Data Source=turncal.db;Version=3;";
        private string selectedEmployeeName = string.Empty; // For å lagre navnet på valgt ansatt

        public CalendarForm()
        {
            this.Text = "Shift Calendar";
            this.Size = new System.Drawing.Size(600, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Kalender for å velge dato
            calendar = new MonthCalendar()
            {
                Left = 20,
                Top = 20,
                MaxSelectionCount = 1
            };
            calendar.DateSelected += Calendar_DateSelected; // Hendelse for valg av dato

            // DataGridView for å vise vakter
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
            dgvShifts.SelectionChanged += DgvShifts_SelectionChanged; // Hendelse for valg av ansatt

            // Label for total timer
            lblTotalHours = new Label()
            {
                Text = "Total Hours This Month: 0",
                Left = 20,
                Top = 420,
                Width = 400,
                Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold)
            };

            // Label for total lønn
            lblTotalSalary = new Label()
            {
                Text = "Total Salary This Month: 0",
                Left = 20,
                Top = 450,
                Width = 400,
                Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold)
            };

            this.Controls.Add(calendar);
            this.Controls.Add(dgvShifts);
            this.Controls.Add(lblTotalHours);
            this.Controls.Add(lblTotalSalary);

            LoadShiftsForDate(DateTime.Now);
            UpdateTotalsForMonth(DateTime.Now);
        }

        private void Calendar_DateSelected(object sender, DateRangeEventArgs e)
        {
            // Last inn vakter for valgt dato
            LoadShiftsForDate(e.Start);
        }

        private void DgvShifts_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvShifts.SelectedRows.Count > 0)
            {
                // Hent navnet fra valgt rad
                selectedEmployeeName = dgvShifts.SelectedRows[0].Cells["Name"].Value.ToString();
                UpdateTotalsForMonth(calendar.SelectionStart, selectedEmployeeName);
            }
        }

        private void LoadShiftsForDate(DateTime date)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = @"SELECT e.Name, s.Date, s.StartTime, s.EndTime 
                                 FROM Shifts s 
                                 JOIN Employees e ON s.EmployeeID = e.ID
                                 WHERE s.Date = @date";

                using (var adapter = new SQLiteDataAdapter(query, connection))
                {
                    adapter.SelectCommand.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                    DataTable dataTable = new DataTable();
                    adapter.Fill(dataTable);
                    dgvShifts.DataSource = dataTable;
                }
            }
        }

        private void UpdateTotalsForMonth(DateTime date, string employeeName = null)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                string query = @"
                    SELECT s.StartTime, s.EndTime, e.WageLevel, s.Date
                    FROM Shifts s
                    JOIN Employees e ON s.EmployeeID = e.ID
                    WHERE strftime('%Y-%m', s.Date) = @month";

                if (!string.IsNullOrEmpty(employeeName))
                {
                    query += " AND e.Name = @name";
                }

                using (var cmd = new SQLiteCommand(query, connection))
                {
                    string monthString = date.ToString("yyyy-MM"); // Måned i format YYYY-MM
                    cmd.Parameters.AddWithValue("@month", monthString);

                    if (!string.IsNullOrEmpty(employeeName))
                    {
                        cmd.Parameters.AddWithValue("@name", employeeName);
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        double totalHours = 0;
                        double totalSalary = 0;

                        while (reader.Read())
                        {
                            TimeSpan startTime = TimeSpan.Parse(reader["StartTime"].ToString());
                            TimeSpan endTime = TimeSpan.Parse(reader["EndTime"].ToString());
                            DateTime shiftDate = DateTime.Parse(reader["Date"].ToString());
                            int wageLevel = Convert.ToInt32(reader["WageLevel"]);

                            double hoursWorked = (endTime - startTime).TotalHours;
                            totalHours += hoursWorked;

                            double hourlyRate = GetHourlyRate(wageLevel);
                            double shiftSalary = CalculateShiftSalary(hoursWorked, hourlyRate, startTime, endTime, shiftDate);
                            totalSalary += shiftSalary;
                        }

                        lblTotalHours.Text = $"Total Hours This Month: {totalHours:F2}";
                        lblTotalSalary.Text = $"Total Salary This Month: {totalSalary:C2}";
                    }
                }
            }
        }

        private double GetHourlyRate(int wageLevel)
        {
            return wageLevel switch
            {
                1 => 150.0,
                2 => 175.0,
                3 => 200.0,
                _ => 150.0,
            };
        }

        private double CalculateShiftSalary(double hoursWorked, double hourlyRate, TimeSpan startTime, TimeSpan endTime, DateTime shiftDate)
        {
            double totalPay = 0;
            TimeSpan current = startTime;

            while (current < endTime)
            {
                double multiplier = GetPayMultiplier(current, shiftDate.DayOfWeek);
                totalPay += hourlyRate * multiplier;
                current = current.Add(TimeSpan.FromHours(1));
            }

            return totalPay;
        }

        private double GetPayMultiplier(TimeSpan time, DayOfWeek day)
        {
            if (day == DayOfWeek.Sunday) return 2.0; // 100% tillegg hele søndagen
            if (day == DayOfWeek.Saturday)
            {
                if (time >= new TimeSpan(18, 0, 0)) return 2.0; // 100% tillegg etter 18:00
                if (time >= new TimeSpan(16, 0, 0)) return 1.5; // 50% tillegg 16:00 - 17:59
            }
            else if (day >= DayOfWeek.Monday && day <= DayOfWeek.Friday)
            {
                if (time >= new TimeSpan(20, 0, 0)) return 2.0; // 100% tillegg etter 20:00
                if (time >= new TimeSpan(18, 0, 0)) return 1.5; // 50% tillegg 18:00 - 19:59
                if (time >= new TimeSpan(16, 0, 0)) return 1.25; // 25% tillegg 16:00 - 17:59
            }
            return 1.0; // Normal sats
        }
    }
}
