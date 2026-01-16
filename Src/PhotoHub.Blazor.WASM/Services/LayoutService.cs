using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PhotoHub.Blazor.WASM.Services;

public class LayoutService
{
    private readonly IJSRuntime _jsRuntime;
    private const string DarkModeKey = "isDarkMode";

    public LayoutService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public event Action? OnMajorUpdate;

    private bool _isDarkMode = true;
    public bool IsDarkMode
    {
        get => _isDarkMode;
        private set
        {
            if (_isDarkMode != value)
            {
                _isDarkMode = value;
                NotifyUpdate();
            }
        }
    }

    public async Task InitializeAsync()
    {
        var storedValue = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", DarkModeKey);
        if (bool.TryParse(storedValue, out var isDark))
        {
            IsDarkMode = isDark;
        }
    }

    public async Task ToggleDarkModeAsync()
    {
        IsDarkMode = !IsDarkMode;
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", DarkModeKey, IsDarkMode.ToString().ToLower());
    }

    private bool _isNavbarCustom;
    public bool IsNavbarCustom
    {
        get => _isNavbarCustom;
        private set
        {
            if (_isNavbarCustom != value)
            {
                _isNavbarCustom = value;
                NotifyUpdate();
            }
        }
    }

    private RenderFragment? _navbarContent;
    public RenderFragment? NavbarContent
    {
        get => _navbarContent;
        private set
        {
            _navbarContent = value;
            NotifyUpdate();
        }
    }

    private bool _keepDrawerVisible;
    public bool KeepDrawerVisible
    {
        get => _keepDrawerVisible;
        private set
        {
            if (_keepDrawerVisible != value)
            {
                _keepDrawerVisible = value;
                NotifyUpdate();
            }
        }
    }

    public void SetCustomNavbar(RenderFragment? content, bool keepDrawerVisible = false)
    {
        IsNavbarCustom = true;
        NavbarContent = content;
        KeepDrawerVisible = keepDrawerVisible;
    }

    public void ResetNavbar()
    {
        IsNavbarCustom = false;
        NavbarContent = null;
        KeepDrawerVisible = false;
    }

    private void NotifyUpdate() => OnMajorUpdate?.Invoke();
}
