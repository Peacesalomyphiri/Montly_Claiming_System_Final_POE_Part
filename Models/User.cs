namespace monthly_claiming_system.Models
{
    // Models/User.cs
    public class User
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;

        public int id { get; set; }
    }

}
