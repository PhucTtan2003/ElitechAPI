// Models/ViewModels/UserDashboardVm.cs
namespace Elitech.Models.ViewModels;

public class UserDashboardVm
{
    public List<UserDeviceVm> Devices { get; set; } = new();
    public string? SelectedDeviceGuid { get; set; }
}

public class UserDeviceVm
{
    public string DeviceGuid { get; set; } = "";
    public string? DeviceName { get; set; }
}