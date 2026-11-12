using FairwayFinder.Admin.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Admin;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Identity;
using FairwayFinder.IntegrationTests.Common.Fakes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.Admin.IntegrationTests.Services;

public class UserAdminServiceTests(AdminFactory factory) : AdminTestBase(factory)
{
    [Fact]
    public async Task Lists_users_with_their_roles()
    {
        var admin = await Data.CreateAdminAsync();
        var golfer = await Data.CreateUserAsync(roles: ApplicationRoles.User);

        var users = await Service<UserAdminService>().GetAllUsersAsync();

        Assert.True(users.Single(u => u.Id == admin.Id).IsAdmin);
        Assert.False(users.Single(u => u.Id == golfer.Id).IsAdmin);
    }

    [Fact]
    public async Task Toggling_the_admin_role_grants_then_revokes_it()
    {
        var golfer = await Data.CreateUserAsync(roles: ApplicationRoles.User);
        var service = Service<UserAdminService>();

        Assert.True((await service.ToggleAdminRoleAsync(golfer.Id)).Succeeded);
        Assert.True((await service.GetUserByIdAsync(golfer.Id))!.IsAdmin);

        Assert.True((await service.ToggleAdminRoleAsync(golfer.Id)).Succeeded);
        Assert.False((await service.GetUserByIdAsync(golfer.Id))!.IsAdmin);
    }

    [Fact]
    public async Task Locking_a_user_blocks_sign_in_until_unlocked()
    {
        var golfer = await Data.CreateUserAsync();
        var service = Service<UserAdminService>();

        Assert.True((await service.SetLockoutAsync(golfer.Id, true)).Succeeded);
        Assert.True((await service.GetUserByIdAsync(golfer.Id))!.IsLockedOut);

        Assert.True((await service.SetLockoutAsync(golfer.Id, false)).Succeeded);
        Assert.False((await service.GetUserByIdAsync(golfer.Id))!.IsLockedOut);
    }

    [Fact]
    public async Task Hiding_a_user_removes_them_from_friend_search()
    {
        var searcher = await Data.CreateUserAsync(firstName: "Searcher");
        var hidden = await Data.CreateUserAsync(firstName: "Hideable", lastName: "Golfer");

        await Service<UserAdminService>().SetSearchHiddenAsync(hidden.Id, true);

        var results = await Service<IFriendService>().SearchUsersAsync(searcher.Id, "Hideable");
        Assert.Empty(results);
    }

    [Fact]
    public async Task Publishing_a_golfers_stats_page_acts_on_them_and_stamps_the_admin()
    {
        var admin = await Data.CreateAdminAsync();
        var golfer = await Data.CreateUserAsync();
        var service = Service<UserAdminService>();

        var result = await service.SetPublicProfileAsync(golfer.Id, new PublicProfileSettingsRequest
        {
            IsPublic = true,
            Slug = "Admin-Made",
            ShowCourseNames = false
        }, admin.Id);

        Assert.Equal(PublicProfileSettingsStatus.Ok, result.Status);

        var listed = (await service.GetUserByIdAsync(golfer.Id))!.PublicProfile;
        Assert.True(listed.IsPublic);
        Assert.Equal("admin-made", listed.Slug);
        Assert.False(listed.ShowCourseNames);
        Assert.True(listed.ShowStrokesGained);

        await using var db = Data.CreateDbContext();
        var golferProfile = db.UserProfiles.Single(p => p.UserId == golfer.Id);
        Assert.Equal(admin.Id, golferProfile.UpdatedBy);
        Assert.False(db.UserProfiles.Single(p => p.UserId == admin.Id).IsPublic);
    }

    [Fact]
    public async Task Publishing_with_a_slug_another_golfer_holds_is_refused()
    {
        var admin = await Data.CreateAdminAsync();
        var holder = await Data.CreateUserAsync();
        var golfer = await Data.CreateUserAsync();
        await Data.MakeProfilePublicAsync(holder.Id, "taken-slug");

        var result = await Service<UserAdminService>().SetPublicProfileAsync(golfer.Id,
            new PublicProfileSettingsRequest { IsPublic = true, Slug = "taken-slug" }, admin.Id);

        Assert.Equal(PublicProfileSettingsStatus.Taken, result.Status);
        Assert.False((await Service<UserAdminService>().GetUserByIdAsync(golfer.Id))!.PublicProfile.IsPublic);
    }

    [Fact]
    public async Task Renaming_and_deleting_a_user()
    {
        var golfer = await Data.CreateUserAsync();
        var service = Service<UserAdminService>();

        Assert.True((await service.UpdateUserAsync(golfer.Id, new UpdateUserDto { FirstName = "New", LastName = "Name" })).Succeeded);
        Assert.Equal("New Name", (await service.GetUserByIdAsync(golfer.Id))!.FullName);

        var deleted = await service.DeleteUserAsync(golfer.Id);
        Assert.True(deleted.Succeeded, string.Join(" ", deleted.Errors.Select(e => e.Description)));
        Assert.Null(await service.GetUserByIdAsync(golfer.Id));
    }

    [Fact]
    public async Task Admin_sent_password_reset_link_redeems()
    {
        var admin = await Data.CreateAdminAsync();
        var golfer = await Data.CreateUserAsync();

        var sent = await Service<IPasswordResetService>().SendResetLinkAsAdminAsync(golfer.Id, admin.Id);
        Assert.True(sent.Success);

        var link = Email.LastTo(golfer.Email!, SentEmailKind.PasswordReset).Link;
        var query = QueryHelpers.ParseQuery(new Uri(link).Query);
        var reset = await Service<IPasswordResetService>().ResetPasswordAsync(query["email"]!, query["token"]!, "reset-by-admin");
        Assert.True(reset.Success);

        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await users.CheckPasswordAsync((await users.FindByIdAsync(golfer.Id))!, "reset-by-admin"));
    }

    [Fact]
    public async Task Resending_an_invite_emails_the_same_registration_link()
    {
        var admin = await Data.CreateAdminAsync();
        var invites = Service<IUserInvitationService>();

        Assert.True((await invites.CreateAndSendInviteAsync("resend@test.fairwayfinder.pro", admin.Id)).Success);
        var pending = Assert.Single(await invites.GetPendingInvitesAsync());
        var first = Email.LastTo("resend@test.fairwayfinder.pro", SentEmailKind.Invitation).Link;

        Assert.True((await invites.ResendInviteAsync(pending.Id, admin.Id)).Success);
        var second = Email.LastTo("resend@test.fairwayfinder.pro", SentEmailKind.Invitation).Link;

        Assert.Equal(first, second);
        Assert.Equal(2, Email.Sent.Count);
    }
}
