using FairwayFinder.Data.Entities;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) 
    : IdentityDbContext<ApplicationUser>(options)
{
    public virtual DbSet<Course> Courses { get; set; }
    public virtual DbSet<Hole> Holes { get; set; }
    public virtual DbSet<HoleStat> HoleStats { get; set; }
    public virtual DbSet<MissType> MissTypes { get; set; }
    public virtual DbSet<Round> Rounds { get; set; }
    public virtual DbSet<RoundStat> RoundStats { get; set; }
    public virtual DbSet<Score> Scores { get; set; }
    public virtual DbSet<Teebox> Teeboxes { get; set; }
    public virtual DbSet<UserInvitation> UserInvitations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ApplicationUser customizations
        modelBuilder.Entity<ApplicationUser>().Property(e => e.FirstName).HasMaxLength(250);
        modelBuilder.Entity<ApplicationUser>().Property(e => e.LastName).HasMaxLength(250);
        modelBuilder.Entity<ApplicationUser>()
            .Property(e => e.CreatedOn)
            .HasDefaultValueSql("NOW()");
        modelBuilder.Entity<ApplicationUser>()
            .Property(e => e.UpdatedOn)
            .HasDefaultValueSql("NOW()");

        // Course
        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(e => e.CourseId).HasName("course_pkey");
            entity.ToTable("course");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.CourseName).HasColumnName("course_name");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedOn).HasColumnName("created_on");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.PhoneNumber).HasColumnName("phone_number");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedOn).HasColumnName("updated_on");
        });

        // Hole
        modelBuilder.Entity<Hole>(entity =>
        {
            entity.HasKey(e => e.HoleId).HasName("hole_pkey");
            entity.ToTable("hole");
            entity.Property(e => e.HoleId).HasColumnName("hole_id");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedOn).HasColumnName("created_on");
            entity.Property(e => e.Handicap).HasColumnName("handicap");
            entity.Property(e => e.HoleNumber).HasColumnName("hole_number");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Par).HasColumnName("par");
            entity.Property(e => e.TeeboxId).HasColumnName("teebox_id");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedOn).HasColumnName("updated_on");
            entity.Property(e => e.Yardage).HasColumnName("yardage");
        });

        // HoleStat
        modelBuilder.Entity<HoleStat>(entity =>
        {
            entity.HasKey(e => e.HoleStatsId).HasName("hole_stats_pkey");
            entity.ToTable("hole_stats");
            entity.Property(e => e.HoleStatsId).HasColumnName("hole_stats_id");
            entity.Property(e => e.ApproachShotOb).HasColumnName("approach_shot_ob");
            entity.Property(e => e.ApproachYardage).HasColumnName("approach_yardage");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedOn).HasColumnName("created_on");
            entity.Property(e => e.HitFairway).HasColumnName("hit_fairway");
            entity.Property(e => e.HitGreen).HasColumnName("hit_green");
            entity.Property(e => e.HoleId).HasColumnName("hole_id");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.MissFairwayType).HasColumnName("miss_fairway_type");
            entity.Property(e => e.MissGreenType).HasColumnName("miss_green_type");
            entity.Property(e => e.NumberOfPutts).HasColumnName("number_of_putts");
            entity.Property(e => e.RoundId).HasColumnName("round_id");
            entity.Property(e => e.ScoreId).HasColumnName("score_id");
            entity.Property(e => e.TeeShotOb).HasColumnName("tee_shot_ob");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedOn).HasColumnName("updated_on");
        });

        // MissType
        modelBuilder.Entity<MissType>(entity =>
        {
            entity.HasKey(e => e.MissTypeId).HasName("miss_type_pkey");
            entity.ToTable("miss_type");
            entity.Property(e => e.MissTypeId).HasColumnName("miss_type_id");
            entity.Property(e => e.MissType1).HasColumnName("miss_type");
        });

        // Round
        modelBuilder.Entity<Round>(entity =>
        {
            entity.HasKey(e => e.RoundId).HasName("round_pkey");
            entity.ToTable("round");
            entity.Property(e => e.RoundId).HasColumnName("round_id");
            entity.Property(e => e.BackNine).HasColumnName("back_nine");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedOn).HasColumnName("created_on");
            entity.Property(e => e.DatePlayed).HasColumnName("date_played");
            entity.Property(e => e.ExcludeFromStats).HasColumnName("exclude_from_stats");
            entity.Property(e => e.FrontNine).HasColumnName("front_nine");
            entity.Property(e => e.FullRound).HasDefaultValue(true).HasColumnName("full_round");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Score).HasColumnName("score");
            entity.Property(e => e.ScoreIn).HasColumnName("score_in");
            entity.Property(e => e.ScoreOut).HasColumnName("score_out");
            entity.Property(e => e.TeeboxId).HasColumnName("teebox_id");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedOn).HasColumnName("updated_on");
            entity.Property(e => e.UserId).HasDefaultValueSql("'unknown'::text").HasColumnName("user_id");
            entity.Property(e => e.UsingHoleStats).HasColumnName("using_hole_stats");
        });

        // RoundStat
        modelBuilder.Entity<RoundStat>(entity =>
        {
            entity.HasKey(e => e.RoundStatsId).HasName("round_stats_pkey");
            entity.ToTable("round_stats");
            entity.Property(e => e.RoundStatsId).HasColumnName("round_stats_id");
            entity.Property(e => e.Birdies).HasColumnName("birdies");
            entity.Property(e => e.Bogies).HasColumnName("bogies");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedOn).HasColumnName("created_on");
            entity.Property(e => e.DoubleBogies).HasColumnName("double_bogies");
            entity.Property(e => e.DoubleEagles).HasColumnName("double_eagles");
            entity.Property(e => e.Eagles).HasColumnName("eagles");
            entity.Property(e => e.HoleInOne).HasColumnName("hole_in_one");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.Pars).HasColumnName("pars");
            entity.Property(e => e.RoundId).HasColumnName("round_id");
            entity.Property(e => e.TripleOrWorse).HasColumnName("triple_or_worse");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedOn).HasColumnName("updated_on");
        });

        // Score
        modelBuilder.Entity<Score>(entity =>
        {
            entity.HasKey(e => e.ScoreId).HasName("score_pkey");
            entity.ToTable("score");
            entity.Property(e => e.ScoreId).HasColumnName("score_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedOn).HasColumnName("created_on");
            entity.Property(e => e.HoleId).HasColumnName("hole_id");
            entity.Property(e => e.HoleScore).HasColumnName("hole_score");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.RoundId).HasColumnName("round_id");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedOn).HasColumnName("updated_on");
            entity.Property(e => e.UserId).HasColumnName("user_id");
        });

        // Teebox
        modelBuilder.Entity<Teebox>(entity =>
        {
            entity.HasKey(e => e.TeeboxId).HasName("teebox_pkey");
            entity.ToTable("teebox");
            entity.Property(e => e.TeeboxId).HasColumnName("teebox_id");
            entity.Property(e => e.CourseId).HasColumnName("course_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedOn).HasColumnName("created_on");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.IsNineHole).HasColumnName("is_nine_hole");
            entity.Property(e => e.IsWomens).HasColumnName("is_womens");
            entity.Property(e => e.Par).HasColumnName("par");
            entity.Property(e => e.Rating).HasPrecision(3, 1).HasColumnName("rating");
            entity.Property(e => e.Slope).HasColumnName("slope");
            entity.Property(e => e.TeeboxName).HasColumnName("teebox_name");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedOn).HasColumnName("updated_on");
            entity.Property(e => e.YardageIn).HasColumnName("yardage_in");
            entity.Property(e => e.YardageOut).HasColumnName("yardage_out");
            entity.Property(e => e.YardageTotal).HasColumnName("yardage_total");
        });

        // UserInvitation
        modelBuilder.Entity<UserInvitation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_invitation_pkey");
            entity.ToTable("user_invitation");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClaimedOn).HasColumnName("claimed_on");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedOn).HasColumnName("created_on");
            entity.Property(e => e.ExpiresOn).HasColumnName("expires_on");
            entity.Property(e => e.InvitationIdentifier).HasColumnName("invitation_identifier");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
            entity.Property(e => e.SentByUser).HasColumnName("sent_by_user");
            entity.Property(e => e.SentToEmail).HasColumnName("sent_to_email");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UpdatedOn).HasColumnName("updated_on");
        });
    }
}