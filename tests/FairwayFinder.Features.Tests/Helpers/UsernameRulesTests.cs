using FairwayFinder.Features.Helpers;

namespace FairwayFinder.Features.Tests.Helpers;

public class UsernameRulesTests
{
    [Theory]
    [InlineData("  GeorgeB ", "georgeb")]
    [InlineData("@Tiger_Woods", "tiger_woods")]
    [InlineData(null, "")]
    public void Normalize_trims_drops_a_leading_at_and_lowercases(string? raw, string expected)
    {
        Assert.Equal(expected, UsernameRules.Normalize(raw));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("tiger.woods")]
    [InlineData("tiger_woods_")]
    [InlineData("1putt")]
    [InlineData("abcdefghijklmnopqrst")]
    public void Validate_accepts_well_formed_names(string name)
    {
        Assert.Null(UsernameRules.Validate(name));
    }

    [Theory]
    [InlineData("ab")]                    // too short
    [InlineData("abcdefghijklmnopqrstu")] // too long
    [InlineData(".tiger")]                // leading period
    [InlineData("_tiger")]                // leading underscore
    [InlineData("tiger.")]                // trailing period
    [InlineData("tiger..woods")]          // double period
    [InlineData("tiger woods")]           // space
    [InlineData("tiger-woods")]           // hyphen
    [InlineData("tigér")]                 // non-ASCII
    [InlineData("Tiger")]                 // not normalized
    public void Validate_rejects_malformed_names(string name)
    {
        Assert.NotNull(UsernameRules.Validate(name));
    }

    [Fact]
    public void Validate_rejects_reserved_names()
    {
        Assert.Equal("That username is reserved.", UsernameRules.Validate("admin"));
        Assert.Equal("That username is reserved.", UsernameRules.Validate("fairwayfinder"));
    }

    [Theory]
    [InlineData("George", "Bjork", "georgebjork")]
    [InlineData("José", "Núñez", "josenunez")]
    [InlineData("Mary-Kate", "O'Neil", "marykateoneil")]
    [InlineData("Maximiliano", "Fitzgerald", "maximilianofitz")] // cut to 15
    [InlineData("Al", null, "golfer")]                          // too short after cleaning
    [InlineData(null, null, "golfer")]
    [InlineData("李", "伟", "golfer")]                            // nothing Latin survives
    public void BuildBase_makes_a_clean_stem_from_the_name(string? first, string? last, string expected)
    {
        var stem = UsernameRules.BuildBase(first, last);

        Assert.Equal(expected, stem);
        Assert.Null(UsernameRules.Validate(stem));
    }
}
