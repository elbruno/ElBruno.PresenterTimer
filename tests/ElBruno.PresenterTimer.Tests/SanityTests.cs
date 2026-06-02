using ElBruno.PresenterTimer.ViewModels;

namespace ElBruno.PresenterTimer.Tests;

/// <summary>
/// Phase 0 sanity tests — verify the skeleton compiles, MVVM base types are usable,
/// and the test runner itself is healthy.
/// </summary>
public class SanityTests
{
    [Fact]
    public void ViewModelBase_SetProperty_RaisesPropertyChanged()
    {
        var vm = new TestableViewModel();
        string? changedProperty = null;
        vm.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

        vm.Name = "hello";

        Assert.Equal("hello", vm.Name);
        Assert.Equal(nameof(vm.Name), changedProperty);
    }

    [Fact]
    public void ViewModelBase_SetProperty_ReturnsFalse_WhenValueUnchanged()
    {
        var vm = new TestableViewModel();
        vm.Name = "same";

        // Reset tracking after first set
        var raised = false;
        vm.PropertyChanged += (_, _) => raised = true;

        var result = vm.SetPropertyPublic(ref vm._nameField, "same", nameof(vm.Name));

        Assert.False(result);
        Assert.False(raised);
    }

    [Fact]
    public void RelayCommand_Execute_InvokesDelegate()
    {
        var executed = false;
        var cmd = new RelayCommand(() => executed = true);

        cmd.Execute(null);

        Assert.True(executed);
    }

    [Fact]
    public void RelayCommand_CanExecute_ReturnsTrueByDefault()
    {
        var cmd = new RelayCommand(() => { });

        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public void RelayCommand_CanExecute_RespectsDelegate()
    {
        var allow = false;
        var cmd = new RelayCommand(() => { }, () => allow);

        Assert.False(cmd.CanExecute(null));
        allow = true;
        Assert.True(cmd.CanExecute(null));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private sealed class TestableViewModel : ViewModelBase
    {
        public string? _nameField;

        public string? Name
        {
            get => _nameField;
            set => SetProperty(ref _nameField, value);
        }

        /// <summary>Exposes <see cref="ViewModelBase.SetProperty{T}"/> for white-box testing.</summary>
        public bool SetPropertyPublic<T>(ref T field, T value, string propertyName)
            => SetProperty(ref field, value, propertyName);
    }
}
