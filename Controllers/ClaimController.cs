using monthly_claiming_system.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using MySql.Data.MySqlClient;
using System.Security.Cryptography;
using System.Text;

namespace monthly_claiming_system.Controllers
{
    public class ClaimController : Controller
    {
        private const string connectionString = "Server=localhost;Database=monthly_claiming_system;User=root;Password=;";

        // GET: Claim/Index
        public ActionResult Index()
        {
            var claims = LoadClaimsList();
            return View(claims);
        }

        // Register a new user with password hashing
        [HttpPost]
        public ActionResult Register(User user)
        {
            if (ModelState.IsValid)
            {
                // Hash password before saving it
                var hashedPassword = HashPassword(user.Password);

                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO users (fullname, email, password) VALUES (@FullName, @Email, @Password)";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@FullName", user.FullName);
                        cmd.Parameters.AddWithValue("@Email", user.Email);
                        cmd.Parameters.AddWithValue("@Password", hashedPassword);

                        cmd.ExecuteNonQuery();
                    }
                }
                return RedirectToAction("Index");
            }
            return View(user);
        }

        // Login action with password comparison
        [HttpPost]
        public ActionResult Login(string email, string password)
        {
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT * FROM users WHERE email = @Email";
                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var storedHashedPassword = reader.GetString("password");

                            // Check if the hashed password matches
                            if (VerifyPassword(password, storedHashedPassword))
                            {
                                // Authentication successful, redirect to the claim index
                                return RedirectToAction("Index");
                            }
                            else
                            {
                                // Authentication failed
                                ModelState.AddModelError("", "Invalid login attempt.");
                            }
                        }
                    }
                }
            }
            return View();
        }

        // Method to hash password using SHA256 (or another secure method)
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        // Method to verify password
        private bool VerifyPassword(string password, string storedHash)
        {
            var hashedPassword = HashPassword(password);
            return hashedPassword == storedHash;
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



        // Load list of claims from the database
        private List<Claim> LoadClaimsList()
        {
            var claims = new List<Claim>();

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT claims.id, lecturer.name AS LecturerName, lecturer.department AS LecturerDepartment, " +
                               "claims.hours_worked AS HoursWorked, claims.total_claim AS TotalClaim, claims.status AS Status " +
                               "FROM claims " +
                               "JOIN lecturer ON claims.lecturer_id = lecturer.id";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            claims.Add(new Claim
                            {
                                Id = reader.GetInt32("id"),
                                LecturerName = reader.GetString("LecturerName"),
                                LecturerDepartment = reader.GetString("LecturerDepartment"),
                                HoursWorked = reader.GetDecimal("HoursWorked"),
                                TotalClaim = reader.GetDecimal("TotalClaim"),
                                Status = reader.GetString("Status")
                            });
                        }
                    }
                }
            }

            return claims;
        }

        // Method to upload a supporting document
        [HttpPost]
        public ActionResult UploadDocument(IFormFile file)
        {
            if (file != null && file.Length > 0)
            {
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "UploadedFiles");
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                var filePath = Path.Combine(uploadDir, file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                // Optionally, store the file path in the database
                // Example: Store filePath in the claims table for reference

                return RedirectToAction("Index");
            }
            return View();
        }
    }
}
