using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Ticky.Base.Converters;

namespace Ticky.Internal.Data
{
    public class DataContext
        : IdentityDbContext<User, IdentityRole<int>, int>,
            IDataProtectionKeyContext
    {
        public DbSet<Code> Codes { get; set; } = default!;
        public DbSet<Project> Projects { get; set; } = default!;
        public DbSet<Board> Boards { get; set; } = default!;
        public DbSet<Column> Columns { get; set; } = default!;
        public DbSet<Card> Cards { get; set; } = default!;
        public DbSet<Comment> Comments { get; set; } = default!;
        public DbSet<ProjectMembership> ProjectMemberships { get; set; } = default!;
        public DbSet<BoardMembership> BoardMemberships { get; set; } = default!;
        public DbSet<Activity> Activities { get; set; } = default!;
        public DbSet<Attachment> Attachments { get; set; } = default!;
        public DbSet<Subtask> Subtasks { get; set; } = default!;
        public DbSet<Reminder> Reminders { get; set; } = default!;
        public DbSet<Label> Labels { get; set; } = default!;
        public DbSet<TimeRecord> TimeRecords { get; set; } = default!;
        public DbSet<LastVisit> LastVisits { get; set; } = default!;
        public DbSet<CardLink> CardLinks { get; set; } = default!;
        public DbSet<Favorite> Favorites { get; set; } = default!;
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = default!;

        public DataContext(DbContextOptions<DataContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder
                .Entity<User>()
                .HasOne(x => x.EmailVerificationCode)
                .WithOne(x => x.User)
                .HasForeignKey<Code>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Project>()
                .HasMany(x => x.Boards)
                .WithOne(x => x.Project)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Board>()
                .HasMany(x => x.Columns)
                .WithOne(x => x.Board)
                .HasForeignKey(x => x.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Column>()
                .HasMany(x => x.Cards)
                .WithOne(x => x.Column)
                .HasForeignKey(x => x.ColumnId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<User>()
                .HasMany(x => x.ProjectMemberships)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Project>()
                .HasMany(x => x.Memberships)
                .WithOne(x => x.Project)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<User>()
                .HasMany(x => x.BoardMemberships)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Board>()
                .HasMany(x => x.Memberships)
                .WithOne(x => x.Board)
                .HasForeignKey(x => x.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Card>()
                .HasOne(x => x.CreatedBy)
                .WithMany(x => x.CreatedCards)
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<User>()
                .HasMany(x => x.CreatedComments)
                .WithOne(x => x.CreatedBy)
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Card>()
                .HasMany(x => x.Comments)
                .WithOne(x => x.Card)
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Card>().HasMany(x => x.Assignees).WithMany(x => x.AssignedTo);

            modelBuilder
                .Entity<Card>()
                .HasMany(x => x.Attachments)
                .WithOne(x => x.Card)
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Card>()
                .HasMany(x => x.Activities)
                .WithOne(x => x.Card)
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<User>()
                .HasMany(x => x.Activities)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Card>()
                .HasMany(x => x.Subtasks)
                .WithOne(x => x.Card)
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Card>()
                .HasMany(x => x.Reminders)
                .WithOne(x => x.Card)
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Board>()
                .HasMany(x => x.Labels)
                .WithOne(x => x.Board)
                .HasForeignKey(x => x.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Card>().HasMany(x => x.Labels).WithMany(x => x.OnCards);

            modelBuilder
                .Entity<User>()
                .HasMany(x => x.TimeRecords)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Card>()
                .HasMany(x => x.TimeRecords)
                .WithOne(x => x.Card)
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<User>()
                .HasMany(x => x.LastVisits)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Board>()
                .HasMany(x => x.LastVisits)
                .WithOne(x => x.Board)
                .HasForeignKey(x => x.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Card>()
                .HasMany(x => x.LinkedIssuesOne)
                .WithOne(x => x.CardOne)
                .HasForeignKey(x => x.CardOneId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Card>()
                .HasMany(x => x.LinkedIssuesTwo)
                .WithOne(x => x.CardTwo)
                .HasForeignKey(x => x.CardTwoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Label>()
                .Property(x => x.BackgroundColor)
                .HasConversion<ColorToInt32Converter>();

            modelBuilder
                .Entity<Label>()
                .Property(x => x.TextColor)
                .HasConversion<ColorToInt32Converter>();

            modelBuilder
                .Entity<Board>()
                .HasMany(x => x.Favorites)
                .WithOne(x => x.Board)
                .HasForeignKey(x => x.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<User>()
                .HasMany(x => x.Favorites)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
