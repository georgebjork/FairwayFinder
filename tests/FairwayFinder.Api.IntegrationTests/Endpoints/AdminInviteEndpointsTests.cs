using System.Net;
using System.Net.Http.Json;
using FairwayFinder.Api.IntegrationTests.Infrastructure;
using FairwayFinder.Features.Data;
using FairwayFinder.IntegrationTests.Common;
using FairwayFinder.IntegrationTests.Common.Fakes;

namespace FairwayFinder.Api.IntegrationTests.Endpoints;

public class AdminInviteEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Regular_golfers_cannot_manage_invites()
    {
        var golfer = await SignInNewUserAsync();

        await AssertStatusAsync(HttpStatusCode.Forbidden, await golfer.Client.GetAsync("/api/admin/invites"));
        await AssertStatusAsync(HttpStatusCode.Forbidden, await golfer.Client.PostAsJsonAsync("/api/admin/invites",
            new SendInviteRequest { Email = "friend@test.fairwayfinder.pro" }));
    }

    [Fact]
    public async Task Admin_invite_emails_a_registration_link_that_registers_the_invitee()
    {
        var admin = await SignInNewAdminAsync();
        const string email = "invited@test.fairwayfinder.pro";

        await AssertStatusAsync(HttpStatusCode.NoContent, await admin.Client.PostAsJsonAsync("/api/admin/invites",
            new SendInviteRequest { Email = email }));

        var pending = await GetAsync<List<PendingInviteDto>>(admin.Client, "/api/admin/invites");
        Assert.Equal(email, Assert.Single(pending).Email);

        var sent = Email.LastTo(email, SentEmailKind.Invitation);
        Assert.StartsWith(TestSettings.RegistrationUrlBase, sent.Link);
        Assert.Equal(TestSettings.AppInstallUrl, sent.AppInstallUrl);

        var code = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(sent.Link).Query)["code"].ToString();
        using var client = AnonymousClient();
        var registered = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Code = code,
            FirstName = "Invited",
            LastName = "Golfer",
            Password = "a-long-password",
        });
        await AssertStatusAsync(HttpStatusCode.OK, registered);
    }

    [Fact]
    public async Task Inviting_an_existing_account_is_rejected()
    {
        var admin = await SignInNewAdminAsync();
        var existing = await Data.CreateUserAsync();

        var response = await admin.Client.PostAsJsonAsync("/api/admin/invites", new SendInviteRequest { Email = existing.Email! });

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
    }

    [Fact]
    public async Task Revoked_invites_leave_the_pending_list_and_stop_working()
    {
        var admin = await SignInNewAdminAsync();
        var invite = await Data.CreateInvitationAsync("revoked@test.fairwayfinder.pro", admin.Id);

        await AssertStatusAsync(HttpStatusCode.NoContent, await admin.Client.DeleteAsync($"/api/admin/invites/{invite.Id}"));
        await AssertStatusAsync(HttpStatusCode.NotFound, await admin.Client.DeleteAsync("/api/admin/invites/987654"));

        Assert.Empty(await GetAsync<List<PendingInviteDto>>(admin.Client, "/api/admin/invites"));

        using var client = AnonymousClient();
        var lookup = await GetAsync<InviteValidationResult>(client, $"/api/auth/invites/{invite.InvitationIdentifier}");
        Assert.False(lookup.Valid);
    }
}
