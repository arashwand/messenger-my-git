using System;
using System.Collections.Generic;

namespace Messenger.Models.Models;

public partial class ClassGroup
{
    public long ClassId { get; set; }

    public string LevelName { get; set; } = null!;

    public long TeacherUserId { get; set; } = 0;

    public string ClassTiming { get; set; } = null!;

    public bool IsActive { get; set; }

    public int? LeftSes { get; set; }

    public DateTime? EndDate { get; set; }

    public string? ClassGroupName { get; set; }

    //public virtual ICollection<ClassGroupMessage> ClassGroupMessages { get; set; } = new List<ClassGroupMessage>();

    //public virtual User TeacherUser { get; set; } = null!;

    public virtual ICollection<UserClassGroup> UserClassGroups { get; set; } = new List<UserClassGroup>();

    public virtual ICollection<ViewClassGroup> ViewClassGroups { get; set; } = new List<ViewClassGroup>();
}
