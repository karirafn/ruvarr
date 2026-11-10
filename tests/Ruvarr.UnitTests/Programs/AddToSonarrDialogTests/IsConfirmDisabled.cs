using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Programs.Components;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.AddToSonarrDialogTests;

public sealed class IsConfirmDisabled
{
    private static readonly SonarrConfigLoadState Loaded = new SonarrConfigLoadState.Loaded(
        [new RootFolder(1, "/tv")],
        [new QualityProfile(1, "HD-1080p")]);

    [Fact]
    public void ReturnsTrueWhenLoading()
    {
        // Act
        bool result = AddToSonarrDialog.IsConfirmDisabled(
            new SonarrConfigLoadState.Loading(), isSubmitting: false, selectedRootFolderId: 1, selectedQualityProfileId: 1);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenIsSubmitting()
    {
        // Act
        bool result = AddToSonarrDialog.IsConfirmDisabled(
            Loaded, isSubmitting: true, selectedRootFolderId: 1, selectedQualityProfileId: 1);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenUnreachable()
    {
        // Act
        bool result = AddToSonarrDialog.IsConfirmDisabled(
            new SonarrConfigLoadState.Unreachable(), isSubmitting: false, selectedRootFolderId: 1, selectedQualityProfileId: 1);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenLoadedWithNoRootFolders()
    {
        // Arrange
        SonarrConfigLoadState state = new SonarrConfigLoadState.Loaded([], [new QualityProfile(1, "HD-1080p")]);

        // Act
        bool result = AddToSonarrDialog.IsConfirmDisabled(
            state, isSubmitting: false, selectedRootFolderId: 1, selectedQualityProfileId: 1);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenLoadedWithNoQualityProfiles()
    {
        // Arrange
        SonarrConfigLoadState state = new SonarrConfigLoadState.Loaded([new RootFolder(1, "/tv")], []);

        // Act
        bool result = AddToSonarrDialog.IsConfirmDisabled(
            state, isSubmitting: false, selectedRootFolderId: 1, selectedQualityProfileId: 1);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenSelectedRootFolderIdIsNull()
    {
        // Act
        bool result = AddToSonarrDialog.IsConfirmDisabled(
            Loaded, isSubmitting: false, selectedRootFolderId: null, selectedQualityProfileId: 1);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenSelectedQualityProfileIdIsNull()
    {
        // Act
        bool result = AddToSonarrDialog.IsConfirmDisabled(
            Loaded, isSubmitting: false, selectedRootFolderId: 1, selectedQualityProfileId: null);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsFalseWhenLoadedAndBothSelected()
    {
        // Act
        bool result = AddToSonarrDialog.IsConfirmDisabled(
            Loaded, isSubmitting: false, selectedRootFolderId: 1, selectedQualityProfileId: 1);

        // Assert
        result.ShouldBeFalse();
    }
}
