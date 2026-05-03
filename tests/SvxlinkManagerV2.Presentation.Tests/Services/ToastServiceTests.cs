using FluentAssertions;
using SvxlinkManagerV2.Presentation.Services;

namespace SvxlinkManagerV2.Presentation.Tests.Services;

/// <summary>
/// Tests unitaires pour ToastService
/// </summary>
public class ToastServiceTests
{
    [Fact]
    public void ShowSuccess_ShouldAddSuccessToast()
    {
        // Arrange
        var service = new ToastService();

        // Act
        service.ShowSuccess("Success message", "Success Title");

        // Assert
        service.Toasts.Should().ContainSingle();
        var toast = service.Toasts.First();
        toast.Type.Should().Be(ToastType.Success);
        toast.Message.Should().Be("Success message");
        toast.Title.Should().Be("Success Title");
        toast.DurationMs.Should().Be(3000);
    }

    [Fact]
    public void ShowSuccess_WithCustomDuration_ShouldUseCustomDuration()
    {
        // Arrange
        var service = new ToastService();

        // Act
        service.ShowSuccess("Success", durationMs: 5000);

        // Assert
        service.Toasts.First().DurationMs.Should().Be(5000);
    }

    [Fact]
    public void ShowError_ShouldAddErrorToast()
    {
        // Arrange
        var service = new ToastService();

        // Act
        service.ShowError("Error message", "Error Title");

        // Assert
        service.Toasts.Should().ContainSingle();
        var toast = service.Toasts.First();
        toast.Type.Should().Be(ToastType.Error);
        toast.Message.Should().Be("Error message");
        toast.Title.Should().Be("Error Title");
    }

    [Fact]
    public void ShowInfo_ShouldAddInfoToast()
    {
        // Arrange
        var service = new ToastService();

        // Act
        service.ShowInfo("Info message");

        // Assert
        service.Toasts.Should().ContainSingle();
        var toast = service.Toasts.First();
        toast.Type.Should().Be(ToastType.Info);
        toast.Message.Should().Be("Info message");
        toast.Title.Should().Be("Information");
    }

    [Fact]
    public void ShowWarning_ShouldAddWarningToast()
    {
        // Arrange
        var service = new ToastService();

        // Act
        service.ShowWarning("Warning message");

        // Assert
        service.Toasts.Should().ContainSingle();
        var toast = service.Toasts.First();
        toast.Type.Should().Be(ToastType.Warning);
        toast.Message.Should().Be("Warning message");
        toast.Title.Should().Be("Attention");
    }

    [Fact]
    public void AddToast_WhenExceedingMaxToasts_ShouldRemoveOldest()
    {
        // Arrange
        var service = new ToastService();

        // Act - Ajouter 6 toasts (max = 5)
        for (int i = 1; i <= 6; i++)
        {
            service.ShowInfo($"Message {i}");
        }

        // Assert
        service.Toasts.Should().HaveCount(5);
        service.Toasts.Should().NotContain(t => t.Message == "Message 1"); // Premier supprimé
        service.Toasts.Should().Contain(t => t.Message == "Message 6"); // Dernier présent
    }

    [Fact]
    public void Remove_ShouldRemoveSpecificToast()
    {
        // Arrange
        var service = new ToastService();
        service.ShowInfo("Toast 1");
        service.ShowInfo("Toast 2");
        var toastToRemove = service.Toasts.First();

        // Act
        service.Remove(toastToRemove.Id);

        // Assert
        service.Toasts.Should().ContainSingle();
        service.Toasts.Should().NotContain(t => t.Id == toastToRemove.Id);
    }

    [Fact]
    public void Clear_ShouldRemoveAllToasts()
    {
        // Arrange
        var service = new ToastService();
        service.ShowInfo("Toast 1");
        service.ShowInfo("Toast 2");
        service.ShowInfo("Toast 3");

        // Act
        service.Clear();

        // Assert
        service.Toasts.Should().BeEmpty();
    }

    [Fact]
    public void OnToastAdded_ShouldBeInvokedWhenToastIsAdded()
    {
        // Arrange
        var service = new ToastService();
        var eventInvoked = false;
        service.OnToastAdded += () => eventInvoked = true;

        // Act
        service.ShowInfo("Test");

        // Assert
        eventInvoked.Should().BeTrue();
    }

    [Fact]
    public void OnToastRemoved_ShouldBeInvokedWhenToastIsRemoved()
    {
        // Arrange
        var service = new ToastService();
        service.ShowInfo("Test");
        var toastId = service.Toasts.First().Id;
        var eventInvoked = false;
        service.OnToastRemoved += () => eventInvoked = true;

        // Act
        service.Remove(toastId);

        // Assert
        eventInvoked.Should().BeTrue();
    }

    [Fact]
    public void OnToastRemoved_ShouldBeInvokedWhenClearIsCalled()
    {
        // Arrange
        var service = new ToastService();
        service.ShowInfo("Test");
        var eventInvoked = false;
        service.OnToastRemoved += () => eventInvoked = true;

        // Act
        service.Clear();

        // Assert
        eventInvoked.Should().BeTrue();
    }

    [Fact]
    public void ToastModel_ShouldHaveUniqueIds()
    {
        // Arrange
        var service = new ToastService();

        // Act
        service.ShowInfo("Toast 1");
        service.ShowInfo("Toast 2");
        service.ShowInfo("Toast 3");

        // Assert
        var ids = service.Toasts.Select(t => t.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ToastModel_ShouldHaveCreatedAtTimestamp()
    {
        // Arrange
        var service = new ToastService();
        var beforeCreation = DateTime.Now;

        // Act
        service.ShowInfo("Test");

        // Assert
        var toast = service.Toasts.First();
        toast.CreatedAt.Should().BeOnOrAfter(beforeCreation);
        toast.CreatedAt.Should().BeOnOrBefore(DateTime.Now.AddSeconds(1));
    }
}
