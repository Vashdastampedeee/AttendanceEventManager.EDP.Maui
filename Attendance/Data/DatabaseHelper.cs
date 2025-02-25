using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Attendance.Models;
using Attendance.Popups;
using Microsoft.Data.SqlClient;
using Mopups.Services;
using ClosedXML.Excel;
using SQLite;
using System.Diagnostics;

namespace Attendance.Data
{
    public class DatabaseHelper
    {
        private readonly SQLiteAsyncConnection _database;
        private readonly string _sqlServerConnStr = "Server=192.168.4.11;Database=TimeKeeping;User Id=WMS;Password=WMS@dmin;TrustServerCertificate=True;";
        public DatabaseHelper(string dbPath)
        {
            UploadDatabaseIfNoDataBaseExists(dbPath);
            _database = new SQLiteAsyncConnection(dbPath);
            _database.CreateTableAsync<Employee>().Wait();
            _database.CreateTableAsync<AttendanceLog>().Wait();
            _database.CreateTableAsync<Event>().Wait();
        }
        //CONDITION IF THERE ARE EXISTING DATABASE OR NOT
        private void UploadDatabaseIfNoDataBaseExists(string dbPath)
        {
            if (!File.Exists(dbPath))
            {
                using var resource = typeof(DatabaseHelper).Assembly.GetManifestResourceStream("Attendance.Resources.Raw.dbtest.db");
                if (resource == null)
                {
                    return;
                }
                using var fileStream = File.Create(dbPath);
                resource.CopyTo(fileStream);
            }
        }
        //HEX CONVERSION FOR IMAGE
        private bool IsHexString(string input)
        {
            return input.Length % 2 == 0 && input.All(c => "0123456789ABCDEFabcdef".Contains(c));
        }
        private byte[] ConvertHexToBytes(string hex)
        {
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) 
                hex = hex.Substring(2);

            int length = hex.Length / 2;
            byte[] bytes = new byte[length];

            for (int i = 0; i < length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
        //SYNC FROM SQL SERVER TO SQL LITE 
        public async Task SyncEmployeesFromSQLServer()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            var fetchingPopup = new SyncDataModal();
            await MopupService.Instance.PushAsync(fetchingPopup);

            using (var sqlConn = new SqlConnection(_sqlServerConnStr))
            {
                await sqlConn.OpenAsync();
                await fetchingPopup.UpdateProgress("Connected to server...");

                await DeleteAllEmployeesAsync();
                await fetchingPopup.UpdateProgress("Clearing old employee data...");

                int totalEmployees = 0;
                string countQuery = "SELECT COUNT(*) FROM TimeKeeping..Employees WHERE JobStatus = 'ACTIVE' OR JobStatusCurrent = 'ACTIVE'";
                using (var countCmd = new SqlCommand(countQuery, sqlConn))
                {
                    totalEmployees = (int)await countCmd.ExecuteScalarAsync();
                }

                await fetchingPopup.UpdateProgress($"Total employees to fetch: {totalEmployees}...");

                string sqlQuery = "select IDNo, EmpName = concat(LastName, ', ',FirstName" +
                    "                   , case when trim(MiddleName) = '' or MiddleName is null then '' else ' ' end, SUBSTRING(trim(MiddleName), 1, 1), case when trim(MiddleName) = '' or MiddleName is null then '' else '.' end)" +
                                    ", BusinessUnit, Photo from TimeKeeping..Employees where JobStatus = 'ACTIVE' or JobStatusCurrent = 'ACTIVE'";

                using (var sqlCmd = new SqlCommand(sqlQuery, sqlConn))
                using (var reader = await sqlCmd.ExecuteReaderAsync())
                {
                    var employees = new List<Employee>();
                    int count = 0;

                    while (await reader.ReadAsync())
                    {
                        byte[] photoBytes = reader["Photo"] as byte[];

                        if (photoBytes != null && photoBytes.Length > 0)
                        {
                            string hexString = Encoding.UTF8.GetString(photoBytes);

                            if (IsHexString(hexString))
                            {
                                photoBytes = ConvertHexToBytes(hexString); 
                            }
                        }
                        var employee = new Employee
                        {
                            IdNumber = reader["IDNo"].ToString(),
                            Name = reader["EmpName"].ToString(),
                            BusinessUnit = reader["BusinessUnit"].ToString(),
                            IdPhoto = photoBytes 
                        };

                        employees.Add(employee);
                        count++;

                        if (count % 50 == 0 || count == totalEmployees)
                        {
                            await fetchingPopup.UpdateProgress($"Fetching {count}/{totalEmployees} employees...");
                        }
                    }
                    await _database.InsertAllAsync(employees);
                    //await _database.RunInTransactionAsync(tran =>
                    //{
                    //    tran.InsertAll(employees);  
                    //});
                    await fetchingPopup.UpdateProgress("Saving data to local database...");
                }
            }
            await fetchingPopup.UpdateProgress("Sync completed successfully");
            await Task.Delay(500); 
            await MopupService.Instance.PopAsync();
        }
        //DELETE EMPLOYEE DATA FROM EMPLOYEE TABLE
        public async Task DeleteAllEmployeesAsync()
        {
            await _database.DeleteAllAsync<Employee>();
            await _database.ExecuteAsync("DELETE FROM sqlite_sequence WHERE name='employee'");
            await _database.ExecuteAsync("UPDATE sqlite_sequence SET seq = 0 WHERE name='employee'");
        }
        //DELETE LOGS DATA FROM ATTENDANCELOGS TABLE
        public async Task DeleteAllLogsAsync()
        {
            await _database.DeleteAllAsync<AttendanceLog>();
        }
        //DISPLAY EMPLOYEE DATA FROM EMPLOYEE TABLE VIA ID NUMBER
        public async Task<Employee> GetEmployeeAsync(string barcode)
        {
            return await _database.Table<Employee>().Where(e => e.IdNumber == barcode).FirstOrDefaultAsync();
        }
        //ADD LOGS DATA TO ATTENDANCE LOGS TABLE
        public Task<int> SaveLogAsync(AttendanceLog log)
        {
            return _database.InsertAsync(log);
        }
        //CHECK FOR EXISTING LOGS IN CURRENT DATE FROM ATTENDANCE LOG TABLE
        public async Task<AttendanceLog> GetAttendanceLogByDateAsync(string idNumber, string date)
        {
            return await _database.Table<AttendanceLog>().Where(log => log.IdNumber == idNumber && log.Timestamp.StartsWith(date)).FirstOrDefaultAsync();
        }
        //DISPLAY LOGS DATA FROM ATTENDANCE LOG TABLE
        public Task<List<AttendanceLog>> GetLogsAsync()
        {
            return _database.Table<AttendanceLog>().OrderByDescending(log => log.Timestamp) .ToListAsync();
        }
        //SEARCH LOG DATA FOR ATTENDANCE LOG TABLE
        public async Task<List<AttendanceLog>> SearchLogsByEventAsync(string keyword, string eventName, string eventCategory)
        {
            keyword = keyword.ToLower();

            return await _database.QueryAsync<AttendanceLog>(
                "SELECT * FROM attendancelogs WHERE (LOWER(IdNumber) LIKE ? OR LOWER(Name) LIKE ? OR LOWER(BusinessUnit) LIKE ? OR LOWER(Status) LIKE ?) " +
                "AND LOWER(EventName) = LOWER(?) AND LOWER(EventCategory) = LOWER(?)",
                $"%{keyword}%", $"%{keyword}%", $"%{keyword}%", $"%{keyword}%", eventName.ToLower(), eventCategory.ToLower()
            );
        }
        //SEARCH EVENT DATA FOR EMPLOYEE DATA
        public async Task<List<Event>> SearchEventsAsync(string keyword)
        {
            keyword = keyword.ToLower();

            return await _database.QueryAsync<Event>(
                "SELECT * FROM event WHERE LOWER(EventName) LIKE ? OR LOWER(Category) LIKE ?",
                $"%{keyword}%", $"%{keyword}%"
            );
        }
        // ADD EVENT DATA
        public async Task AddEventAsync(Event newEvent)
        {
            await _database.InsertAsync(newEvent);
        }
        // DELETE SPECIFIC DATA FOR EVENT
        public async Task DeleteEventAsync(Event deleteEvent)
        {
            await _database.DeleteAsync(deleteEvent);
        }
        // UPDATE SPECIFIC DATA FOR EVENT 
        public async Task UpdateEventAsync(Event updatedEvent)
        {
            var selectedEvent = await GetSelectedEventAsync();

            if (selectedEvent != null && selectedEvent.Id == updatedEvent.Id)
            {
                updatedEvent.IsSelected = true;
            }

            await _database.UpdateAsync(updatedEvent);
        }
        // FOR SELECTION OF DEFAULT EVENT 
        public async Task SetSelectedEventAsync(int eventId)
        {
            var events = await GetEventsAsync();

            foreach (var ev in events)
            {
                ev.IsSelected = (ev.Id == eventId); 
            }

            await _database.UpdateAllAsync(events);
        }
        // DISPLAY SELECTED EVENT OR IN USE EVENT IN HOMEPAGE
        public async Task<Event> GetSelectedEventAsync()
        {
            var result = await _database.QueryAsync<Event>("SELECT * FROM event WHERE IsSelected = 1 LIMIT 1");
            return result.FirstOrDefault();
        }

        // FOR SPECIFIC ID OF EVENT FOR EDIT EVENT
        public async Task<Event> GetEventByIdAsync(int eventId)
        {
            return await _database.Table<Event>().FirstOrDefaultAsync(e => e.Id == eventId);
        }
        // DISPLAY EVENT DATA TO EVENT TABBED PAGE
        public async Task<List<Event>> GetEventsAsync()
        {
            return await _database.QueryAsync<Event>("SELECT * FROM event");
        }
        // FILTER CATEGORY EVENT
        public async Task<List<Event>> GetEventsByCategoryAsync(string category)
        {
            return await _database.Table<Event>().Where(e => e.Category == category).ToListAsync();
        }
        //FILTER LOGS
        public async Task<List<AttendanceLog>> GetFilteredLogsAsync(string eventName, string eventDate, string eventCategory, string fromTime, string toTime)
        {
            string query = "SELECT * FROM attendancelogs";
            List<object> parameters = new List<object>();

            List<string> conditions = new List<string>();

            if (!string.IsNullOrEmpty(eventName))
            {
                conditions.Add("LOWER(EventName) = LOWER(?)");
                parameters.Add(eventName.ToLower());
            }

            if (!string.IsNullOrEmpty(eventCategory))
            {
                conditions.Add("LOWER(EventCategory) = LOWER(?)");
                parameters.Add(eventCategory.ToLower());
            }

            if (!string.IsNullOrEmpty(eventDate))
            {
                conditions.Add("EventDate = ?");
                parameters.Add(eventDate);
            }

            if (!string.IsNullOrEmpty(fromTime) && !string.IsNullOrEmpty(toTime))
            {
                conditions.Add("(LOWER(FromTime) = LOWER(?) AND LOWER(ToTime) = LOWER(?))");
                parameters.Add(fromTime.ToLower());
                parameters.Add(toTime.ToLower());
            }

            // ✅ Only add WHERE if there are conditions
            if (conditions.Count > 0)
            {
                query += " WHERE " + string.Join(" AND ", conditions);
            }

            query += " ORDER BY Timestamp DESC";

            return await _database.QueryAsync<AttendanceLog>(query, parameters.ToArray());
        }
        //FILTER CATEGORY
        public async Task<List<Event>> GetFilteredCategoryAsync(string category)
        {
            return await _database.Table<Event>()
                .Where(l => l.Category == category)
                .ToListAsync();
        }
        //CONTROL FOR EXISTING DATA OF EVENT NAME
        public async Task<Event> GetEventByNameAsync(string eventName)
        {
            return await _database.Table<Event>()
                                  .Where(e => e.EventName == eventName)
                                  .FirstOrDefaultAsync();
        }
        //EXPORT FILTER DIFFERENT METHODS
        public async Task<List<AttendanceLog>> GetFilteredLogsExportAsync(string eventName, string eventCategory, string eventDate, string fromTime, string toTime)
        {
            return await _database.Table<AttendanceLog>()
                .Where(log => log.EventName == eventName &&
                              log.EventCategory == eventCategory &&
                              log.EventDate == eventDate &&
                              log.FromTime == fromTime &&
                              log.ToTime == toTime)
                .ToListAsync();
        }
        public async Task<List<AttendanceLog>> GetLogsByEventAndCategoryAsync(string eventName, string eventCategory)
        {
            return await _database.QueryAsync<AttendanceLog>(
                "SELECT * FROM attendancelogs WHERE EventName = ? AND EventCategory = ? ORDER BY Timestamp DESC",
                eventName, eventCategory
            );
        }
        public async Task<List<AttendanceLog>> GetLogsByEventCategoryAndDateAsync(string eventName, string eventCategory, string eventDate)
        {
            return await _database.QueryAsync<AttendanceLog>(
                "SELECT * FROM attendancelogs WHERE EventName = ? AND EventCategory = ? AND EventDate = ?",
                eventName, eventCategory, eventDate
            );
        }
        public async Task<int> GetTotalEmployeeCountAsync()
        {
            return await _database.Table<Employee>().CountAsync();
        }

        public async Task<int> GetScannedEmployeeCountForActiveEventAsync()
        {
            var selectedEvent = await GetSelectedEventAsync();
            if (selectedEvent == null)
                return 0; 
            return await _database.Table<AttendanceLog>()
                .Where(log => log.EventName == selectedEvent.EventName &&
                              log.EventCategory == selectedEvent.Category &&
                              log.EventDate == selectedEvent.EventDate &&
                              log.FromTime == selectedEvent.FromTime &&
                              log.ToTime == selectedEvent.ToTime &&
                              log.Status == "SUCCESS")
                .CountAsync();
        }

        // Get total employee count for a specific Business Unit
        public async Task<int> GetTotalEmployeeCountByBUAsync(string businessUnit)
        {
            return await _database.Table<Employee>()
                                  .Where(emp => emp.BusinessUnit == businessUnit)
                                  .CountAsync();
        }

        // Get total employee counts for all business units
        public async Task<Dictionary<string, int>> GetTotalEmployeeCountForAllBUAsync()
        {
            var businessUnits = new List<string> { "RAWLINGS", "JLINE", "HLB", "BAG", "SUPPORT GROUP" };
            var result = new Dictionary<string, int>();

            foreach (var bu in businessUnits)
            {
                int count = await GetTotalEmployeeCountByBUAsync(bu);
                result[bu] = count;
            }

            foreach (var bu in businessUnits)
            {
                if (!result.ContainsKey(bu))
                {
                    result[bu] = 0;
                }
            }

            return result;
        }

        // Get the count of scanned employees for a specific Business Unit in the active event
        public async Task<int> GetScannedEmployeeCountByBUAsync(string businessUnit)
        {
            // Get the currently active event
            var selectedEvent = await GetSelectedEventAsync();
            if (selectedEvent == null)
                return 0; // No active event, return 0 scans

            // Count scanned employees for the active event and specific business unit
            return await _database.Table<AttendanceLog>()
                .Where(log => log.EventName == selectedEvent.EventName &&
                              log.EventCategory == selectedEvent.Category &&
                              log.EventDate == selectedEvent.EventDate &&
                              log.FromTime == selectedEvent.FromTime &&
                              log.ToTime == selectedEvent.ToTime &&
                              log.BusinessUnit == businessUnit)
                .CountAsync();
        }

        // Get scanned employee counts for all business units in the active event
        public async Task<Dictionary<string, int>> GetScannedEmployeeCountForAllBUAsync()
        {
            var businessUnits = new List<string> { "RAWLINGS", "JLINE", "HLB", "BAG", "SUPPORT GROUP" };
            var result = new Dictionary<string, int>();

            var selectedEvent = await GetSelectedEventAsync();
            if (selectedEvent == null)
            {
                // If no active event, initialize all BUs with zero count
                foreach (var bu in businessUnits)
                {
                    result[bu] = 0;
                }
                return result;
            }

            foreach (var bu in businessUnits)
            {
                int count = await GetScannedEmployeeCountByBUAsync(bu);
                result[bu] = count;
            }

            return result;
        }
        // Get Present Employees for Active Event
        public async Task<List<AttendanceLog>> GetPresentEmployeesAsync(string eventName)
        {
            string query = "SELECT * FROM attendancelogs WHERE EventName = ?";
            return await _database.QueryAsync<AttendanceLog>(query, eventName);
        }


        // Get Absent Employees for Active Event
        public async Task<List<Employee>> GetAbsentEmployeesAsync(string eventName)
        {
            string query = @"
        SELECT * FROM employee 
        WHERE IdNumber NOT IN 
        (SELECT IdNumber FROM attendancelogs WHERE EventName = ?)";

            return await _database.QueryAsync<Employee>(query, eventName);
        }

        public async Task<List<AttendanceLog>> GetPresentEmployeesByBUAsync(string eventName, string businessUnit)
        {
            string query = @"SELECT * FROM attendancelogs 
                     WHERE EventName = ? AND BusinessUnit = ?";

            return await _database.QueryAsync<AttendanceLog>(query, eventName, businessUnit);
        }

        public async Task<List<Employee>> GetAbsentEmployeesByBUAsync(string eventName, string businessUnit)
        {
            string query = @"SELECT * FROM employee 
                     WHERE BusinessUnit = ? 
                     AND IdNumber NOT IN (SELECT IdNumber FROM attendancelogs WHERE EventName = ?)";

            return await _database.QueryAsync<Employee>(query, businessUnit, eventName);
        }

        public async Task ExportAttendanceToExcel(string businessUnit = "ALL")
        {
            try
            {
                var selectedEvent = await GetSelectedEventAsync();
                if (selectedEvent == null)
                {
                    Debug.WriteLine("[ERROR] No active event selected.");
                    return;
                }

                Debug.WriteLine($"[INFO] Exporting attendance for Business Unit: {businessUnit}");

                // Get Present and Absent Employees based on Business Unit
                List<AttendanceLog> presentEmployees;
                List<Employee> absentEmployees;

                if (businessUnit == "ALL")
                {
                    presentEmployees = await GetPresentEmployeesAsync(selectedEvent.EventName);
                    absentEmployees = await GetAbsentEmployeesAsync(selectedEvent.EventName);
                }
                else
                {
                    presentEmployees = await GetPresentEmployeesByBUAsync(selectedEvent.EventName, businessUnit);
                    absentEmployees = await GetAbsentEmployeesByBUAsync(selectedEvent.EventName, businessUnit);
                }

                Debug.WriteLine($"[INFO] Total Present Employees: {presentEmployees.Count}");
                Debug.WriteLine($"[INFO] Total Absent Employees: {absentEmployees.Count}");

                // Set Excel File Path
                string fileName = $"Attendance_{businessUnit}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                string filePath = Path.Combine("/storage/emulated/0/Download/", fileName);

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Attendance");

                    // Headers
                    worksheet.Cell(1, 1).Value = "ID Number";
                    worksheet.Cell(1, 2).Value = "Name";
                    worksheet.Cell(1, 3).Value = "Business Unit";
                    worksheet.Cell(1, 4).Value = "Status";

                    // Fill Present Employees
                    int row = 2;
                    foreach (var emp in presentEmployees)
                    {
                        worksheet.Cell(row, 1).Value = emp.IdNumber;
                        worksheet.Cell(row, 2).Value = emp.Name;
                        worksheet.Cell(row, 3).Value = emp.BusinessUnit;
                        worksheet.Cell(row, 4).Value = "Present";
                        row++;
                    }

                    // Fill Absent Employees
                    foreach (var emp in absentEmployees)
                    {
                        worksheet.Cell(row, 1).Value = emp.IdNumber;
                        worksheet.Cell(row, 2).Value = emp.Name;
                        worksheet.Cell(row, 3).Value = emp.BusinessUnit;
                        worksheet.Cell(row, 4).Value = "Absent";
                        row++;
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();

                    // Save Excel file
                    workbook.SaveAs(filePath);
                }

                Debug.WriteLine($"[SUCCESS] Attendance exported successfully: {filePath}");
                await MopupService.Instance.PushAsync(new DownloadModal("Export Successful", $"File saved at:\n{filePath}"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to export attendance: {ex.Message}");
                await MopupService.Instance.PushAsync(new DownloadModal("Export Error", $"Failed to export: {ex.Message}"));
            }
        }




    }
}
