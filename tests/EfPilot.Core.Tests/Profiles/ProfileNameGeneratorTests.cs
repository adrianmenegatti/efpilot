using EfPilot.Core.Profiles;

namespace EfPilot.Core.Tests.Profiles;

public sealed class ProfileNameGeneratorTests
{
    [Theory]
    [InlineData("AppIdentityDbContext", "Identity")]
    [InlineData("RateRiskDbContext", "RateRisk")]
    [InlineData("TenantContext", "Tenant")]
    [InlineData("CustomName", "CustomName")]
    public void FromDbContext_ShouldGenerateExpectedProfileName(
        string dbContextName,
        string expected)
    {
        var result = ProfileNameGenerator.FromDbContext(dbContextName);

        Assert.Equal(expected, result);
    }
}