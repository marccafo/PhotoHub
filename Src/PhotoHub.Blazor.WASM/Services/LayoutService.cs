using Microsoft.AspNetCore.Components;

namespace PhotoHub.Blazor.WASM.Services;

public class LayoutService
{
    public event Action? OnMajorUpdate;

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

    public void SetCustomNavbar(RenderFragment? content)
    {
        IsNavbarCustom = true;
        NavbarContent = content;
    }

    public void ResetNavbar()
    {
        IsNavbarCustom = false;
        NavbarContent = null;
    }

    private void NotifyUpdate() => OnMajorUpdate?.Invoke();
}
