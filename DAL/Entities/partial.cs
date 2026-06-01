namespace DAL.Entities;

public partial class Document
{
    public virtual ICollection<UploadJob> UploadJobs { get; set; } = new List<UploadJob>();
}

public partial class User
{
    public virtual ICollection<UploadJob> UploadJobs { get; set; } = new List<UploadJob>();
    public virtual ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
}
