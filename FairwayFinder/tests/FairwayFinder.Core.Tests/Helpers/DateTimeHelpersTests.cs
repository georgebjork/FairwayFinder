using Bogus;
using FairwayFinder.Core.Helpers;

namespace FairwayFinder.Core.Tests.Helpers;

public class DateTimeHelpersTests
{
    private readonly Faker _faker = new();

    [Fact]
    public void FormatDate_ShouldReturnEmptyStringForYear1970OrEarlier()
    {
        // Using Bogus to generate a date before or on 1970
        var dateBefore1970 = _faker.Date.Between(new DateTime(1, 1, 1), new DateTime(1970, 12, 31));

        var result = dateBefore1970.FormatDate();

        Assert.Equal("", result);
    }

    [Fact]
    public void FormatDate_ShouldReturnFormattedStringForYearAfter1970()
    {
        // Using Bogus to generate a date after 1970
        var dateAfter1970 = _faker.Date.Between(new DateTime(1971, 1, 1), DateTime.Today);

        var result = dateAfter1970.FormatDate();

        // Since the format is "M/d/yy", we use the same format here for comparison
        var expected = dateAfter1970.ToString("M/d/yy");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatDate_RandomDatesBeforeAndAfter1970_Validation()
    {
        // Generating a random date before or on 1970
        var dateBefore1970 = _faker.Date.Between(new DateTime(1, 1, 1), new DateTime(1970, 12, 31));
        var resultBefore1970 = dateBefore1970.FormatDate();
        Assert.Empty(resultBefore1970);

        // Generating a random date after 1970
        var dateAfter1970 = _faker.Date.Between(new DateTime(1971, 1, 1), DateTime.Today);
        var resultAfter1970 = dateAfter1970.FormatDate();
        var expectedAfter1970 = dateAfter1970.ToString("M/d/yy");
        Assert.Equal(expectedAfter1970, resultAfter1970);
    }
}
