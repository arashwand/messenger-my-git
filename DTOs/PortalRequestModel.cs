using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.DTOs
{
    public class ClassGroupModel
    {
        public int ClassId { get; set; }
        public string LevelName { get; set; }
        public int TeacherUserId { get; set; }
        public string ClassTiming { get; set; }
        public string TeacherName { get; set; }
        public bool IsActive { get; set; }
        public DateTime EndDate { get; set; }
        public List<MemberModel> Members { get; set; }
    }

    public class MemberModel
    {
        public int UserId { get; set; }
        public string RoleName { get; set; }
        public string RoleFaName { get; set; }
        public string NameFamily { get; set; }
        public string DeptName { get; set; }
    }

}
