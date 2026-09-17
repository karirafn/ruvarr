using Ruvarr.Abstractions;

using Shouldly;

namespace Ruvarr.UnitTests.Abstractions;

public sealed class ApiClientErrorsTests
{
    [Fact]
    public void NotFoundCode_IsStableString()
    {
        // Arrange
        // Act
        string code = ApiClientErrors.NotFoundCode;

        // Assert
        code.ShouldBe("ApiClient.NotFound");
    }

    [Fact]
    public void RequestFailedCode_IsStableString()
    {
        // Arrange
        // Act
        string code = ApiClientErrors.RequestFailedCode;

        // Assert
        code.ShouldBe("ApiClient.RequestFailed");
    }

    [Fact]
    public void NotFound_HasExpectedCodeAndDescription()
    {
        // Arrange
        // Act
        RuvarrError error = ApiClientErrors.NotFound;

        // Assert
        error.ShouldSatisfyAllConditions(
            () => error.Code.ShouldBe(ApiClientErrors.NotFoundCode),
            () => error.Description.ShouldBe("The requested resource was not found.")
        );
    }

    [Fact]
    public void RequestFailed_HasExpectedCodeAndDescription()
    {
        // Arrange
        // Act
        RuvarrError error = ApiClientErrors.RequestFailed;

        // Assert
        error.ShouldSatisfyAllConditions(
            () => error.Code.ShouldBe(ApiClientErrors.RequestFailedCode),
            () => error.Description.ShouldBe("The API request failed.")
        );
    }
}
