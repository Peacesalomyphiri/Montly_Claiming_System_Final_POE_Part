using monthly_claiming_system.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Microsoft.AspNetCore.Http;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;
using Document = iTextSharp.text.Document;
using System.Data; // Alias to resolve ambiguity

namespace CMCS.Controllers
{
    public class AdminController : Controller
    {
        private string connectionString = "server=localhost;database=monthly_claiming_system;uid=root;password=;";
        

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AdminRegister()
        {
            return View(new AdminRegisterViewModel());  // Initialize the model here
        }


        [HttpPost]
        [AllowAnonymous]
        public IActionResult AdminRegister(AdminRegisterViewModel admin)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Check if fields are populated
                    if (string.IsNullOrEmpty(admin.Email) || string.IsNullOrEmpty(admin.Password) || string.IsNullOrEmpty(admin.Name))
                    {
                        ModelState.AddModelError("", "All fields are required.");
                        return View(admin);
                    }

                    using (MySqlConnection conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();

                        // Insert into the User table with plain text password
                        var cmdUser = new MySqlCommand("INSERT INTO users (email, password) VALUES (@Email, @Password); SELECT LAST_INSERT_ID();", conn);
                        cmdUser.Parameters.AddWithValue("@Email", admin.Email);
                        cmdUser.Parameters.AddWithValue("@Password", admin.Password); // Plain text password

                        // Get the UserId from the inserted record
                        var userId = Convert.ToInt32(cmdUser.ExecuteScalar());

                        // Insert into the Admin table using the UserId (foreign key) and Role
                        var cmdAdmin = new MySqlCommand("INSERT INTO admin (id, role) VALUES (@UserId, @Role)", conn);
                        cmdAdmin.Parameters.AddWithValue("@UserId", userId);  // Reference to the 'Users' table
                        cmdAdmin.Parameters.AddWithValue("@Role", admin.Role); // Role field from the view model

                        int result = cmdAdmin.ExecuteNonQuery();

                        if (result > 0)
                        {
                            return RedirectToAction("Login");
                        }
                        else
                        {
                            ModelState.AddModelError("", "Failed to register admin.");
                            return View(admin);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception or display it for debugging purposes
                    ModelState.AddModelError("", $"Error occurred: {ex.Message}");
                    return View(admin);
                }
            }

            return View(admin);
        }


        [HttpGet]
        [AllowAnonymous]
        public IActionResult AdminLogin()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> AdminLogin(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT * FROM Users WHERE Email = @Email";
                    using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Email", model.Email);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (reader.HasRows)
                            {
                                while (await reader.ReadAsync())
                                {
                                    var storedPassword = reader["Password"].ToString(); // In production, hash comparison is needed
                                    if (storedPassword == model.Password)
                                    {
                                        return RedirectToAction("ManageClaims", "Admin");
                                    }
                                }
                            }
                        }
                    }
                }
                ModelState.AddModelError("", "Invalid login attempt.");
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ManageClaims()
        {
            var claims = new List<ClaimViewModel>();

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var query = @"SELECT 
                        c.id AS ClaimID,
                        c.hours_worked AS HoursWorked,
                        c.hourly_rate AS HourlyRate,
                        c.total_claim AS TotalClaim,
                        c.status AS ClaimStatus,
                        c.month AS Month, -- Ensure the alias matches the property name
                        c.file_name AS SupportingDocumentName,
                        l.name AS LecturerName,
                        l.department AS LecturerDepartment
                      FROM claims c
                      JOIN lecturer l ON c.lecturer_id = l.id";

                using (var command = new MySqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        claims.Add(new ClaimViewModel
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("ClaimID")),
                            LecturerName = reader.GetString(reader.GetOrdinal("LecturerName")),
                            LecturerDepartment = reader.GetString(reader.GetOrdinal("LecturerDepartment")),
                            Month = reader.GetString(reader.GetOrdinal("Month")), // Fixed column alias
                            HoursWorked = reader.GetDecimal(reader.GetOrdinal("HoursWorked")),
                            HourlyRate = reader.GetDecimal(reader.GetOrdinal("HourlyRate")),
                            TotalClaim = reader.GetDecimal(reader.GetOrdinal("TotalClaim")), // No extra calculations
                            Status = reader.GetString(reader.GetOrdinal("ClaimStatus")),
                            SupportingDocument = reader.IsDBNull(reader.GetOrdinal("SupportingDocumentName"))
                                                 ? null
                                                 : Path.Combine("/uploads", reader.GetString(reader.GetOrdinal("SupportingDocumentName")))
                        });
                    }
                }
            }

            return View(claims);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateClaimStatus(int claimId, string action)
        {
            string newStatus = action == "approve" ? "Approved" : "Rejected";

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var query = "UPDATE claims SET status = @Status WHERE id = @ClaimID";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Status", newStatus);
                    command.Parameters.AddWithValue("@ClaimID", claimId);
                    await command.ExecuteNonQueryAsync();
                }
            }

            return RedirectToAction(nameof(ManageClaims));
        }


        [HttpPost]
        public async Task<IActionResult> ApproveClaim(int claimId)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var query = "UPDATE claims SET status = 'Approved' WHERE id = @claimId";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@claimId", claimId);
                    await command.ExecuteNonQueryAsync();
                }
            }
            TempData["SuccessMessage"] = "Claim approved successfully.";
            return RedirectToAction("ManageClaims");
        }

        [HttpPost]
        public async Task<IActionResult> RejectClaim(int claimId, string rejectionReason)
        {
            // Ensure that rejectionReason is not empty or null
            if (string.IsNullOrEmpty(rejectionReason))
            {
                TempData["ErrorMessage"] = "Rejection reason cannot be empty.";
                return RedirectToAction("ManageClaims");
            }

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var query = "UPDATE claims SET status = 'Rejected', rejection_reason = @RejectionReason WHERE id = @ClaimId";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ClaimId", claimId);
                    command.Parameters.AddWithValue("@RejectionReason", rejectionReason);  // Store rejection reason
                    await command.ExecuteNonQueryAsync();
                }
            }

            TempData["SuccessMessage"] = "Claim rejected successfully.";
            return RedirectToAction("ManageClaims");
        }


        [HttpGet]
        public async Task<IActionResult> GenerateReport()
        {
            var claims = new List<ClaimViewModel>();

            // Fetch approved claims from the database
            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var query = @"SELECT 
                         c.id AS ClaimID,
                         l.name AS LecturerName,
                         l.department AS LecturerDepartment,
                         c.month AS Month,
                         c.hours_worked AS HoursWorked,
                         c.hourly_rate AS HourlyRate,
                         c.total_claim AS TotalClaim,
                         c.status AS ClaimStatus
                       FROM claims c
                       JOIN lecturer l ON c.lecturer_id = l.id
                       WHERE c.status = 'Approved'"; // Only approved claims

                using (var command = new MySqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        claims.Add(new ClaimViewModel
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("ClaimID")),
                            LecturerName = reader.GetString(reader.GetOrdinal("LecturerName")),
                            LecturerDepartment = reader.GetString(reader.GetOrdinal("LecturerDepartment")),
                            Month = reader.GetString(reader.GetOrdinal("Month")),
                            HoursWorked = reader.GetDecimal(reader.GetOrdinal("HoursWorked")),
                            HourlyRate = reader.GetDecimal(reader.GetOrdinal("HourlyRate")),
                            TotalClaim = reader.GetDecimal(reader.GetOrdinal("TotalClaim")),
                            Status = reader.GetString(reader.GetOrdinal("ClaimStatus"))
                        });
                    }
                }
            }

            // Generate PDF
            using (var memoryStream = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 10, 10, 10, 10);
                PdfWriter.GetInstance(document, memoryStream);
                document.Open();

                // Add Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                document.Add(new Paragraph("Approved Claims Report", titleFont));
                document.Add(new Paragraph($"Generated on: {DateTime.Now}\n\n"));

                // Add Table
                var table = new PdfPTable(7) { WidthPercentage = 100 };
                table.AddCell("Claim ID");
                table.AddCell("Lecturer Name");
                table.AddCell("Department");
                table.AddCell("Month");
                table.AddCell("Hours Worked");
                table.AddCell("Hourly Rate");
                table.AddCell("Total Claim");

                foreach (var claim in claims)
                {
                    table.AddCell(claim.Id.ToString());
                    table.AddCell(claim.LecturerName);
                    table.AddCell(claim.LecturerDepartment);
                    table.AddCell(claim.Month);
                    table.AddCell(claim.HoursWorked.ToString());
                    table.AddCell(claim.HourlyRate.ToString("C"));
                    table.AddCell(claim.TotalClaim.ToString("C"));
                }

                document.Add(table);
                document.Close();

                return File(memoryStream.ToArray(), "application/pdf", "ApprovedClaimsReport.pdf");
            }
        }

        // GET: ManageLecturers (View Lecturers List and Load Data for Editing)
        [HttpGet]
        public async Task<IActionResult> ManageLecturers(int? id)
        {
            var model = new ManageLecturersViewModel
            {
                Lecturers = new List<Lecturer>(),
                LecturerToEdit = null
            };

            // Fetch all lecturers
            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var command = new MySqlCommand("SELECT * FROM Lecturer", connection);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        model.Lecturers.Add(new Lecturer
                        {
                            Id = reader.GetInt32("Id"),
                            Name = reader.GetString("Name"),
                            Department = reader.GetString("Department"),
                            Email = reader.GetString("Email"),

                        });
                    }
                }
            }

            // If `id` is provided, fetch lecturer details for editing
            if (id.HasValue)
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    var command = new MySqlCommand("SELECT * FROM Lecturer WHERE Id = @Id", connection);
                    command.Parameters.AddWithValue("@Id", id.Value);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            model.LecturerToEdit = new Lecturer
                            {
                                Id = reader.GetInt32("Id"),
                                Name = reader.GetString("Name"),
                                Department = reader.GetString("Department"),
                                Email = reader.GetString("Email"),
                                Password = reader.GetString("Password")
                            };
                        }
                    }
                }
            }

            return View(model);
        }

        // POST: Add or Update Lecturer
        [HttpPost]
        public async Task<IActionResult> ManageLecturers(Lecturer lecturer)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "There was an issue with your submission. Please check all required fields.";
                return RedirectToAction("ManageLecturers");
            }

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                if (lecturer.Id == 0) // Add new lecturer
                {
                    // Insert lecturer into the 'Lecturer' table
                    var command = new MySqlCommand(
                        "INSERT INTO Lecturer (Name, Department, Email, Password) VALUES (@Name, @Department, @Email, @Password)",
                        connection);
                    command.Parameters.AddWithValue("@Name", lecturer.Name);
                    command.Parameters.AddWithValue("@Department", lecturer.Department);
                    command.Parameters.AddWithValue("@Email", lecturer.Email);
                    command.Parameters.AddWithValue("@Password", lecturer.Password); // In production, hash before saving

                    await command.ExecuteNonQueryAsync();

                    // Insert lecturer into the 'users' table for login purposes
                    var userCommand = new MySqlCommand(
                        "INSERT INTO users (email, password) VALUES (@Email, @Password)", connection);
                    userCommand.Parameters.AddWithValue("@Email", lecturer.Email);
                    userCommand.Parameters.AddWithValue("@Password", lecturer.Password); // Again, avoid plain text in production

                    await userCommand.ExecuteNonQueryAsync();

                    TempData["SuccessMessage"] = "Lecturer added successfully!";
                }
                else // Update existing lecturer
                {
                    var command = new MySqlCommand(
                        "UPDATE Lecturer SET Name = @Name, Department = @Department, Email = @Email, Password = @Password WHERE Id = @Id",
                        connection);
                    command.Parameters.AddWithValue("@Id", lecturer.Id);
                    command.Parameters.AddWithValue("@Name", lecturer.Name);
                    command.Parameters.AddWithValue("@Department", lecturer.Department);
                    command.Parameters.AddWithValue("@Email", lecturer.Email);
                    command.Parameters.AddWithValue("@Password", lecturer.Password); // In production, hash before saving

                    await command.ExecuteNonQueryAsync();
                    TempData["SuccessMessage"] = "Lecturer updated successfully!";
                }
            }

            return RedirectToAction("ManageLecturers");
        }


        // POST: Delete Lecturer
        [HttpPost]
        public async Task<IActionResult> DeleteLecturer(int id)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var command = new MySqlCommand("DELETE FROM Lecturer WHERE Id = @Id", connection);
                command.Parameters.AddWithValue("@Id", id);

                await command.ExecuteNonQueryAsync();
                TempData["SuccessMessage"] = "Lecturer deleted successfully!";
            }

            return RedirectToAction("ManageLecturers");
        }
    }
}

