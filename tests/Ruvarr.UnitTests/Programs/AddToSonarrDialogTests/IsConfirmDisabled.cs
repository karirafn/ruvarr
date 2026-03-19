using Ruvarr.Programs;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.AddToSonarrDialogTests;

public sealed class IsConfirmDisabled
{
    [Fact]
    public void ReturnsTrueWhenIsSubmitting()
    {
        // Act
        bool result = AddToSonarrDialog.IsConfirmDisabled(
            isSubmitting: true, selectedRootFolderId: 1, selectedQualityProfileId: 1, loadErrorMessage: null);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenLoadErrorMessageIsNotNull()
    {
        // Act
        bool result = AddToSonarrDialog.IsConfirmDisabled(
            isSubmitting: false, selectedRootFolderId: 1, selectedQualityProfileId: 1, loadErrorMessage: "Error");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenSelectedRootFolderIdIsNull()
    {
        // Act
        bool result = AddToSonarrDialog.IsConfirmDisabled(
            isSubmitting: false, selectedRootFolderId: null, selectedQualityProfileId: 1, loadErrorMessage: null);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenSelectedQualityProfileIdIsNull()
    {
        // Act
        bool result = AddToSonarrDialog.IsConfirmDisabled(
            isSubmitting: false, selectedRootFolderId: 1, selectedQualityProfileId: null, loadErrorMessage: null);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsFalseWhenAllFieldsAreValid()
    {
        // Act
        bool result = AddToSonarrDialog.IsConfirmDisabled(
            isSubmitting: false, selectedRootFolderId: 1, selectedQualityProfileId: 1, loadErrorMessage: null);

        // Assert
        result.ShouldBeFalse();
    }
}
