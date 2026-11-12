using FairwayFinder.Features.Helpers;

namespace FairwayFinder.Features.Tests.Helpers;

public class PublicSlugRulesTests
{
    [Theory]
    [InlineData("george")]
    [InlineData("george-bjork")]
    [InlineData("g8r")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123")]
    public void Accepts_lowercase_letters_digits_and_single_hyphens(string slug)
    {
        Assert.Null(PublicSlugRules.Validate(slug));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("abcdefghijklmnopqrstuvwxyz01234")]
    [InlineData("-george")]
    [InlineData("george-")]
    [InlineData("george--bjork")]
    [InlineData("george_bjork")]
    [InlineData("george.bjork")]
    [InlineData("George")]
    [InlineData("admin")]
    [InlineData("badge")]
    public void Rejects_everything_else(string slug)
    {
        Assert.NotNull(PublicSlugRules.Validate(slug));
    }

    [Fact]
    public void Normalize_trims_and_lowercases()
    {
        Assert.Equal("george", PublicSlugRules.Normalize("  George "));
        Assert.Equal("", PublicSlugRules.Normalize(null));
    }

    [Theory]
    [InlineData("georgeb", "georgeb")]
    [InlineData("tee.time_42", "tee-time-42")]
    [InlineData("a__b", "a-b")]
    public void FromHandle_turns_username_punctuation_into_hyphens(string handle, string expected)
    {
        Assert.Equal(expected, PublicSlugRules.FromHandle(handle));
    }
}
