using FairwayFinder.Features.Helpers;
using FairwayFinder.Identity;

namespace FairwayFinder.Features.Tests.Helpers;

public class DisplayNameHelperTests
{
    [Fact]
    public void Build_prefers_the_full_name()
    {
        Assert.Equal("Dale Jones", DisplayNameHelper.Build("Dale", "Jones", "dale@test.com"));
    }

    [Fact]
    public void Build_trims_when_only_one_half_of_the_name_is_set()
    {
        Assert.Equal("Dale", DisplayNameHelper.Build("Dale", null, "dale@test.com"));
        Assert.Equal("Jones", DisplayNameHelper.Build("", "Jones", "dale@test.com"));
    }

    [Fact]
    public void Build_falls_back_to_the_user_name()
    {
        Assert.Equal("dale@test.com", DisplayNameHelper.Build(null, null, "dale@test.com"));
        Assert.Equal("dale@test.com", DisplayNameHelper.Build("  ", "  ", "dale@test.com"));
    }

    [Fact]
    public void Build_returns_empty_when_nothing_is_set()
    {
        Assert.Equal(string.Empty, DisplayNameHelper.Build(null, null, null));
    }

    [Fact]
    public void Build_returns_null_for_a_null_user()
    {
        Assert.Null(DisplayNameHelper.Build((ApplicationUser?)null));
    }

    [Fact]
    public void Build_reads_a_users_name_off_the_entity()
    {
        var user = new ApplicationUser { FirstName = "Sam", LastName = "Reed", UserName = "sam@test.com" };

        Assert.Equal("Sam Reed", DisplayNameHelper.Build(user));
    }

    [Fact]
    public void BuildForAdmin_falls_back_to_the_email_then_unknown()
    {
        Assert.Equal("Dale Jones", DisplayNameHelper.BuildForAdmin("Dale", "Jones", "dale@test.com"));
        Assert.Equal("dale@test.com", DisplayNameHelper.BuildForAdmin(null, null, "dale@test.com"));
        Assert.Equal("Unknown", DisplayNameHelper.BuildForAdmin(null, null, null));
        Assert.Equal("Unknown", DisplayNameHelper.BuildForAdmin(null, null, ""));
    }

    [Fact]
    public void BuildForAdmin_never_falls_back_to_a_user_name()
    {
        // The admin overload takes an email, not a user name — a blank name must not silently
        // pick up whatever was passed as the third argument unless it really is the email.
        Assert.Equal("sam@test.com", DisplayNameHelper.BuildForAdmin("", "", "sam@test.com"));
    }
}
