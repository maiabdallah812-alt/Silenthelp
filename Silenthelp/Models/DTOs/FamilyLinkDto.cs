namespace SilentHelp.Models.DTOs
{
    public class JoinFamilyDto
    {
        public string InviteCode { get; set; } = string.Empty;
    }

    public class FamilyMemberDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
