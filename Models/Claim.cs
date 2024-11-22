namespace monthly_claiming_system.Models
{
    public class Claim
    {
        public int Id { get; set; }
        public string? LecturerName { get; set; } = string.Empty;
        public string? AdditionalNotes { get; set; } = string.Empty;
        public string LecturerDepartment { get; set; } = string.Empty;
        public string Month { get; set; } = string.Empty;
        public string? SupportingDocument { get; set; }
        public string OriginalFileName { get; set; }
        public decimal HoursWorked { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal TotalClaim { get; set; }
        public string Status { get; set; } = string.Empty;
        public string RejectionReason { get; set; }

        // Validation logic for claim
        public string ValidateClaim()
        {
            const decimal maxHours = 200;  // Max hours a lecturer can claim
            const decimal maxHourlyRate = 50;  // Max hourly rate allowed
            const decimal minHours = 10; // Min hours a lecturer can claim
            const decimal minHourlyRate = 20; // Min hourly rate allowed

            // Validate the hours worked
            if (HoursWorked > maxHours)
            {
                return "Hours worked exceed the maximum allowed.";
            }
            if (HoursWorked < minHours)
            {
                return "Hours worked are below the minimum allowed.";
            }

            // Validate the hourly rate
            if (HourlyRate > maxHourlyRate)
            {
                return "Hourly rate exceeds the maximum allowed.";
            }
            if (HourlyRate < minHourlyRate)
            {
                return "Hourly rate is below the minimum allowed.";
            }

            // Calculate the total claim (HoursWorked * HourlyRate)
            TotalClaim = HoursWorked * HourlyRate;

            // If everything is valid, return null (valid claim)
            return null;
        }

    }
}
