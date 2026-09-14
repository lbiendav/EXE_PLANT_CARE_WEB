using HomePlant.Models;

namespace HomePlant.ViewModels;

public sealed class AdminUserGroupVM<T>
{
    public string UserId { get; set; } = "";
    public UserModel? User { get; set; }
    public List<T> Items { get; set; } = new();

    public string DisplayName => !string.IsNullOrWhiteSpace(User?.FullName)
        ? User.FullName
        : "Người dùng không còn tồn tại";

    public string Email => !string.IsNullOrWhiteSpace(User?.Email)
        ? User.Email
        : UserId;

    public string Initial => DisplayName[..1].ToUpperInvariant();
}

public sealed class AdminAiDiagnosesVM
{
    public int TotalCount { get; set; }
    public List<AdminUserGroupVM<AiDiagnosisModel>> UserGroups { get; set; } = new();
}

public sealed class AdminGardenPlantsVM
{
    public int TotalCount { get; set; }
    public List<AdminUserGroupVM<AdminGardenPlantItemVM>> UserGroups { get; set; } = new();
}

public sealed class AdminGardenPlantItemVM
{
    public UserPlantModel Plant { get; set; } = new();
    public string SpeciesName { get; set; } = "Loại cây chưa xác định";
}
