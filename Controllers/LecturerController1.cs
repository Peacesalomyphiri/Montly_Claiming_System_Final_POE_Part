using Microsoft.AspNetCore.Mvc;
using monthly_claiming_system.Models;
using MySql.Data.MySqlClient;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace monthly_claiming_system.Controllers
{
    public class LecturerController : Controller
    {
        private string connectionString = "server=localhost;database=monthly_claiming_system;uid=root;password=;";

        // Register Action
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(Lecturer lecturer)
        {
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                // Insert into the User table first (plain text password)
                var cmdUser = new MySqlCommand("INSERT INTO users (email, password) VALUES (@Email, @Password); SELECT LAST_INSERT_ID();", conn);
                cmdUser.Parameters.AddWithValue("@Email", lecturer.Email);
                cmdUser.Parameters.AddWithValue("@Password", lecturer.Password); // No hashing, plain text password

                // Get the UserId from the inserted record
                var userId = Convert.ToInt32(cmdUser.ExecuteScalar());

                // Now, insert into the Lecturer table using the UserId
                var cmdLecturer = new MySqlCommand("INSERT INTO lecturer (name, department, email, user_id) VALUES (@Name, @Department, @Email, @UserId)", conn);
                cmdLecturer.Parameters.AddWithValue("@Name", lecturer.Name);
                cmdLecturer.Parameters.AddWithValue("@Department", lecturer.Department);
                cmdLecturer.Parameters.AddWithValue("@Email", lecturer.Email);
                cmdLecturer.Parameters.AddWithValue("@UserId", userId);

                int result = cmdLecturer.ExecuteNonQuery();

                if (result > 0)
                {
                    return RedirectToAction("Login");
                }
                else
                {
                    ModelState.AddModelError("", "Failed to register lecturer.");
                    return View();
                }
            }
        }

        // Login Action
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new MySqlCommand("SELECT * FROM users WHERE email = @Email AND password = @Password", conn);
                cmd.Parameters.AddWithValue("@Email", email);
                cmd.Parameters.AddWithValue("@Password", password);
                var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    HttpContext.Session.SetString("Email", email);
                    return RedirectToAction("Dashboard");
                }
            }

            ModelState.AddModelError("", "Invalid login credentials.");
            return View();
        }

        // Dashboard Action
        public IActionResult Dashboard()
        {
            // Get the lecturer's email from the session
            string email = HttpContext.Session.GetString("Email");

            // Redirect to Login if the user isn't logged in
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login");
            }

            // Prepare variables to store lecturer and claims information
            string lecturerName = "";
            List<Claim> claims = new List<Claim>();

            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                // Fetch lecturer's name
                var cmd = new MySqlCommand("SELECT name FROM lecturer WHERE email = @Email", conn);
                cmd.Parameters.AddWithValue("@Email", email);
                var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    lecturerName = reader["name"].ToString();
                }
                reader.Close();

                // Fetch claims submitted by the lecturer
                var claimCmd = new MySqlCommand("SELECT * FROM claims WHERE lecturer_id = (SELECT id FROM lecturer WHERE email = @Email)", conn);
                claimCmd.Parameters.AddWithValue("@Email", email);
                var claimReader = claimCmd.ExecuteReader();
                while (claimReader.Read())
                {
                    claims.Add(new Claim
                    {
                        Id = Convert.ToInt32(claimReader["id"]),
                        HoursWorked = Convert.ToDecimal(claimReader["hours_worked"]),
                        HourlyRate = Convert.ToDecimal(claimReader["hourly_rate"]),
                        TotalClaim = Convert.ToDecimal(claimReader["total_claim"]),
                        Status = claimReader["status"].ToString(),
                        Month = claimReader["month"].ToString(),
                        SupportingDocument = claimReader["file_name"].ToString(),
                        RejectionReason = claimReader["rejection_reason"].ToString(),
                        AdditionalNotes = claimReader["additional_notes"].ToString()
                    }); 
                } 
                claimReader.Close();
            }

            // Pass data to the view
            ViewData["LecturerName"] = lecturerName;
            ViewData["Claims"] = claims;

            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitClaim(Claim claim, IFormFile supportingDocument)
        {
            if (claim == null || claim.HoursWorked <= 0 || claim.HourlyRate <= 0 || string.IsNullOrWhiteSpace(claim.Month))
            {
                ModelState.AddModelError("", "Invalid claim data. Please check your inputs.");
                return View(claim);
            }

            string email = HttpContext.Session.GetString("Email");
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login");
            }

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // Fetch Lecturer ID
                    var cmdLecturerId = new MySqlCommand("SELECT id FROM lecturer WHERE email = @Email", conn);
                    cmdLecturerId.Parameters.AddWithValue("@Email", email);
                    var lecturerId = await cmdLecturerId.ExecuteScalarAsync();

                    if (lecturerId == null)
                    {
                        ModelState.AddModelError("", "Lecturer not found.");
                        return View(claim);
                    }

                    // Calculate the total claim amount
                    decimal totalClaim = claim.HoursWorked * claim.HourlyRate;

                    // Handle file upload
                    string filePath = null;
                    string fileName = null;
                    string originalFileName = null;
                    if (supportingDocument != null && supportingDocument.Length > 0)
                    {
                        string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                        Directory.CreateDirectory(uploadsFolder);
                        fileName = Guid.NewGuid().ToString() + Path.GetExtension(supportingDocument.FileName);
                        filePath = Path.Combine(uploadsFolder, fileName);
                        originalFileName = supportingDocument.FileName;

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await supportingDocument.CopyToAsync(stream);
                        }
                    }
                    // Define the minimum and maximum allowed values for hourly rate and hours worked
                    const decimal MIN_HOURLY_RATE = 50m; // Min allowed hourly rate
                    const decimal MAX_HOURLY_RATE = 1000m; // Max allowed hourly rate
                    const decimal MIN_HOURS = 10m; // Min allowed hours worked
                    const decimal MAX_HOURS = 200m; // Max allowed hours worked

                    // Default status is "Pending"
                    string status = "Pending";

                    // Check if the values are within the allowed range
                    if (claim.HoursWorked < MIN_HOURS || claim.HoursWorked > MAX_HOURS || claim.HourlyRate < MIN_HOURLY_RATE || claim.HourlyRate > MAX_HOURLY_RATE)
                    {
                        // If out of bounds, set status to "Pending Approval" for admin review
                        status = "Pending Approval";
                    }
                    else
                    {
                        // If within the range, set status to "Approved"
                        status = "Approved";
                    }

                    // Optionally, you can now assign this status to the claim object or save it to the database
                    claim.Status = status;


                    // Insert claim into the database
                    string sqlQuery = @"
                INSERT INTO claims (lecturer_id, hours_worked, hourly_rate, total_claim, status, month, file_name, file_path, original_file_name, additional_notes) 
                VALUES (@LecturerId, @HoursWorked, @HourlyRate, @TotalClaim, @Status, @Month, @FileName, @FilePath, @OriginalFileName, @AdditionalNotes)";

                    var cmd = new MySqlCommand(sqlQuery, conn);
                    cmd.Parameters.AddWithValue("@LecturerId", lecturerId);
                    cmd.Parameters.AddWithValue("@HoursWorked", claim.HoursWorked);
                    cmd.Parameters.AddWithValue("@HourlyRate", claim.HourlyRate);
                    cmd.Parameters.AddWithValue("@TotalClaim", totalClaim);
                    cmd.Parameters.AddWithValue("@Status", status);
                    cmd.Parameters.AddWithValue("@Month", claim.Month);
                    cmd.Parameters.AddWithValue("@FileName", fileName);
                    cmd.Parameters.AddWithValue("@FilePath", filePath);
                    cmd.Parameters.AddWithValue("@OriginalFileName", originalFileName);
                    cmd.Parameters.AddWithValue("@AdditionalNotes", claim.AdditionalNotes);

                    int result = await cmd.ExecuteNonQueryAsync();

                    if (result > 0)
                    {
                        TempData["SuccessMessage"] = "Claim submitted successfully!";
                        return RedirectToAction("Dashboard");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Failed to submit claim. Please try again.");
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred: " + ex.Message);
            }

            return View(claim);
        }

        public IActionResult Logout()
        {
            // You can perform any logout logic here (like clearing the session or cookies)

            // Redirect the user back to the Index page (or Home page)
            return RedirectToAction("Index", "Home");
        }

    }
}




