using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TravelMatch.API.DTOs.GuideVerification;
using TravelMatch.API.Models;
using TravelMatch.Tests.TestSupport;

namespace TravelMatch.Tests;

public class GuideVerificationApiTests
{
    private static readonly SubmitGuideVerificationRequestDto ValidRequest = new()
    {
        Bio = "Licensed local guide with cultural tour experience.",
        ServiceArea = "Dhaka",
        ExperienceYears = 8
    };

    [Fact]
    public async Task Submit_GuidePersistsInformationAsPending()
    {
        using var factory = new ApiWebApplicationFactory();
        var guide = AddGuide(factory);
        using var client = AuthenticatedClient(factory, guide.User);

        var response = await client.PostAsJsonAsync(
            "/api/guide-verifications",
            ValidRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<GuideVerificationResultDto>();
        Assert.True(result!.Success);
        Assert.Equal("Pending", result.Data!.VerificationStatus);

        using var verificationContext = factory.CreateContext();
        var profile = verificationContext.GuideProfiles
            .Single(candidate => candidate.UserId == guide.User.Id);
        Assert.Equal(ValidRequest.Bio, profile.Bio);
        Assert.Equal(ValidRequest.ServiceArea, profile.ServiceArea);
        Assert.Equal(ValidRequest.ExperienceYears, profile.ExperienceYears);
        Assert.Equal(VerificationStatus.Pending, profile.VerificationStatus);
        Assert.NotNull(profile.VerificationSubmittedAt);
    }

    [Fact]
    public async Task Submit_UnauthenticatedReturnsUnauthorized()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/guide-verifications",
            ValidRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Tourist)]
    [InlineData(UserRole.Organizer)]
    [InlineData(UserRole.Admin)]
    public async Task Submit_NonGuideRoleIsForbidden(UserRole role)
    {
        using var factory = new ApiWebApplicationFactory();
        var user = AddUser(factory, role);
        using var client = AuthenticatedClient(factory, user);

        var response = await client.PostAsJsonAsync(
            "/api/guide-verifications",
            ValidRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Submit_InvalidInformationReturnsBadRequest()
    {
        using var factory = new ApiWebApplicationFactory();
        var guide = AddGuide(factory);
        using var client = AuthenticatedClient(factory, guide.User);
        var invalidRequest = new
        {
            Bio = " ",
            ServiceArea = "Dhaka",
            ExperienceYears = -1
        };

        var response = await client.PostAsJsonAsync(
            "/api/guide-verifications",
            invalidRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_DuplicatePendingRequestReturnsConflict()
    {
        using var factory = new ApiWebApplicationFactory();
        var submittedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var guide = AddGuide(
            factory,
            VerificationStatus.Pending,
            submittedAt);
        using var client = AuthenticatedClient(factory, guide.User);

        var response = await client.PostAsJsonAsync(
            "/api/guide-verifications",
            ValidRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var verificationContext = factory.CreateContext();
        var profile = verificationContext.GuideProfiles
            .Single(candidate => candidate.UserId == guide.User.Id);
        Assert.Equal(submittedAt, profile.VerificationSubmittedAt);
        Assert.Equal(string.Empty, profile.Bio);
    }

    [Fact]
    public async Task Submit_CannotResetVerifiedStatus()
    {
        using var factory = new ApiWebApplicationFactory();
        var guide = AddGuide(
            factory,
            VerificationStatus.Verified,
            DateTimeOffset.UtcNow.AddDays(-1));
        using var client = AuthenticatedClient(factory, guide.User);

        var response = await client.PostAsJsonAsync(
            "/api/guide-verifications",
            ValidRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var verificationContext = factory.CreateContext();
        Assert.Equal(
            VerificationStatus.Verified,
            verificationContext.GuideProfiles.Single().VerificationStatus);
    }

    [Fact]
    public async Task Submit_CannotChangeAnotherGuideOrSetVerifiedStatus()
    {
        using var factory = new ApiWebApplicationFactory();
        var submittingGuide = AddGuide(factory);
        var otherGuide = AddGuide(factory);
        using var client = AuthenticatedClient(factory, submittingGuide.User);

        var response = await client.PostAsJsonAsync(
            "/api/guide-verifications",
            new
            {
                ValidRequest.Bio,
                ValidRequest.ServiceArea,
                ValidRequest.ExperienceYears,
                guideUserId = otherGuide.User.Id,
                verificationStatus = "Verified"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var verificationContext = factory.CreateContext();
        var submittedProfile = verificationContext.GuideProfiles
            .Single(profile => profile.UserId == submittingGuide.User.Id);
        var otherProfile = verificationContext.GuideProfiles
            .Single(profile => profile.UserId == otherGuide.User.Id);
        Assert.Equal(
            VerificationStatus.Pending,
            submittedProfile.VerificationStatus);
        Assert.Equal(ValidRequest.Bio, submittedProfile.Bio);
        Assert.Equal(VerificationStatus.Pending, otherProfile.VerificationStatus);
        Assert.Null(otherProfile.VerificationSubmittedAt);
    }

    [Fact]
    public async Task Submit_RejectedGuideCannotResubmitWithoutAnAuthorizedReopeningRule()
    {
        using var factory = new ApiWebApplicationFactory();
        var guide = AddGuide(
            factory,
            VerificationStatus.Rejected,
            DateTimeOffset.UtcNow.AddDays(-1));
        using var client = AuthenticatedClient(factory, guide.User);

        var response = await client.PostAsJsonAsync(
            "/api/guide-verifications",
            ValidRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var verificationContext = factory.CreateContext();
        Assert.Equal(
            VerificationStatus.Rejected,
            verificationContext.GuideProfiles.Single().VerificationStatus);
        Assert.Equal(
            guide.Profile.VerificationSubmittedAt,
            verificationContext.GuideProfiles.Single().VerificationSubmittedAt);
    }

    [Fact]
    public async Task AdminCanListAndRetrieveOnlySubmittedGuides()
    {
        using var factory = new ApiWebApplicationFactory();
        var submittedGuide = AddGuide(
            factory,
            VerificationStatus.Pending,
            DateTimeOffset.UtcNow);
        AddGuide(factory);
        var admin = AddUser(factory, UserRole.Admin);
        using var client = AuthenticatedClient(factory, admin);

        var listResponse = await client.GetAsync("/api/guide-verifications");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var submissions = await listResponse.Content
            .ReadFromJsonAsync<List<GuideVerificationSubmissionResponseDto>>();
        var submission = Assert.Single(submissions!);
        Assert.Equal(submittedGuide.Profile.Id, submission.GuideProfileId);
        Assert.DoesNotContain(
            "PasswordHash",
            await listResponse.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);

        var detailResponse = await client.GetAsync(
            $"/api/guide-verifications/{submittedGuide.Profile.Id}");

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
    }

    [Fact]
    public async Task GuideCannotAccessReviewEndpoints()
    {
        using var factory = new ApiWebApplicationFactory();
        var guide = AddGuide(
            factory,
            VerificationStatus.Pending,
            DateTimeOffset.UtcNow);
        using var client = AuthenticatedClient(factory, guide.User);

        var listResponse = await client.GetAsync("/api/guide-verifications");
        var approveResponse = await client.PostAsync(
            $"/api/guide-verifications/{guide.Profile.Id}/approve",
            content: null);

        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, approveResponse.StatusCode);
        using var verificationContext = factory.CreateContext();
        Assert.Equal(
            VerificationStatus.Pending,
            verificationContext.GuideProfiles.Single().VerificationStatus);
    }

    [Theory]
    [InlineData(UserRole.Tourist)]
    [InlineData(UserRole.Guide)]
    [InlineData(UserRole.Organizer)]
    public async Task NonAdminCannotAccessReviewEndpoints(UserRole role)
    {
        using var factory = new ApiWebApplicationFactory();
        var guide = AddGuide(
            factory,
            VerificationStatus.Pending,
            DateTimeOffset.UtcNow);
        var user = role == UserRole.Guide
            ? guide.User
            : AddUser(factory, role);
        using var client = AuthenticatedClient(factory, user);

        var response = await client.GetAsync("/api/guide-verifications");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminApprovalPersistsAndCannotBeRepeated()
    {
        using var factory = new ApiWebApplicationFactory();
        var guide = AddGuide(
            factory,
            VerificationStatus.Pending,
            DateTimeOffset.UtcNow);
        var admin = AddUser(factory, UserRole.Admin);
        using var client = AuthenticatedClient(factory, admin);

        var response = await client.PostAsync(
            $"/api/guide-verifications/{guide.Profile.Id}/approve",
            content: null);
        var repeatedResponse = await client.PostAsync(
            $"/api/guide-verifications/{guide.Profile.Id}/approve",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, repeatedResponse.StatusCode);
        using var verificationContext = factory.CreateContext();
        Assert.Equal(
            VerificationStatus.Verified,
            verificationContext.GuideProfiles.Single().VerificationStatus);
    }

    [Fact]
    public async Task AdminCanRejectPendingSubmission()
    {
        using var factory = new ApiWebApplicationFactory();
        var guide = AddGuide(
            factory,
            VerificationStatus.Pending,
            DateTimeOffset.UtcNow);
        var admin = AddUser(factory, UserRole.Admin);
        using var client = AuthenticatedClient(factory, admin);

        var response = await client.PostAsync(
            $"/api/guide-verifications/{guide.Profile.Id}/reject",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var verificationContext = factory.CreateContext();
        Assert.Equal(
            VerificationStatus.Rejected,
            verificationContext.GuideProfiles.Single().VerificationStatus);
    }

    [Fact]
    public async Task AdminReview_UnknownSubmissionReturnsNotFound()
    {
        using var factory = new ApiWebApplicationFactory();
        var admin = AddUser(factory, UserRole.Admin);
        using var client = AuthenticatedClient(factory, admin);

        var response = await client.PostAsync(
            "/api/guide-verifications/9999/approve",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static (User User, GuideProfile Profile) AddGuide(
        ApiWebApplicationFactory factory,
        VerificationStatus status = VerificationStatus.Pending,
        DateTimeOffset? submittedAt = null)
    {
        using var context = factory.CreateContext();
        var user = AddUser(context, UserRole.Guide);
        var profile = new GuideProfile
        {
            UserId = user.Id,
            VerificationStatus = status,
            VerificationSubmittedAt = submittedAt
        };
        context.GuideProfiles.Add(profile);
        context.SaveChanges();
        return (user, profile);
    }

    private static User AddUser(
        ApiWebApplicationFactory factory,
        UserRole role)
    {
        using var context = factory.CreateContext();
        return AddUser(context, role);
    }

    private static User AddUser(
        TravelMatch.API.Data.ApplicationDbContext context,
        UserRole role)
    {
        var user = new User
        {
            FullName = $"{role} Account",
            Email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@example.com",
            PhoneNumber = "+8801000000000",
            PasswordHash = "password-hash",
            Role = role
        };
        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    private static HttpClient AuthenticatedClient(
        ApiWebApplicationFactory factory,
        User user)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.GenerateToken(user));
        return client;
    }
}
