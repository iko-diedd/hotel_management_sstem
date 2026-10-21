using HousekeepingApi.Services;
using Xunit;

namespace HousekeepingApi.Tests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Short1a")]          // too short
    [InlineData("alllowercase123")]  // no uppercase
    [InlineData("ALLUPPERCASE123")]  // no lowercase
    [InlineData("NoDigitsHereAtAll")] // no digit
    public void Weak_passwords_are_rejected(string? password) => Assert.NotNull(PasswordPolicy.Validate(password));

    [Fact]
    public void Overlong_passwords_are_rejected() => Assert.NotNull(PasswordPolicy.Validate("Aa1" + new string('x', 130)));

    [Fact]
    public void Strong_password_is_accepted() => Assert.Null(PasswordPolicy.Validate("Correct-Horse-9"));
}
