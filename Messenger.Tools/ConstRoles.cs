namespace Messenger.Tools
{
    /// <summary>
    /// 1. Manager
    /// 2. Personel
    /// 3. Teacher
    /// 4. Student
    /// 5. Mentor
    /// </summary>
    public class ConstRoles
    {
        
        public const string Manager = "Manager";
        public const string Personel = "Personel";
        public const string Teacher = "Teacher";
        public const string Student = "Student";
        public const string Mentor = "Mentor";
        public const string SystemBot = "SystemBot";

        public static List<string> ListRoles()
        {
            return new List<string>()
            {
                Manager,
                Personel,
                Teacher,
                Student,
                Mentor,
                SystemBot
            };
        }
    }
    public class RoleFaName
    {
        public const string Manager = "مدیر";
        public const string Mentor = "منتور";
        public const string Student = "دانشجو";
        public const string Teacher = "استاد";
        public const string Personel = "پرسنل";
        public const string SystemBot = "سیستم";

        public static string GetRoleName(string role)
        {
            return role switch
            {
                ConstRoles.Manager => Manager,
                ConstRoles.Mentor => Mentor,
                ConstRoles.Student => Student,
                ConstRoles.Teacher => Teacher,
                ConstRoles.Personel => Personel,
                ConstRoles.SystemBot => SystemBot,
                _ => throw new ArgumentException("Invalid role name", nameof(role))
            };
        }
    }
}
