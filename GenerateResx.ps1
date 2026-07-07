$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms

function Create-Resx {
    param (
        [string]$Path,
        [hashtable]$Entries
    )
    $writer = New-Object System.Resources.ResXResourceWriter($Path)
    foreach ($key in $Entries.Keys) {
        $writer.AddResource($key, $Entries[$key])
    }
    $writer.Generate()
    $writer.Close()
    Write-Host "Created $Path"
}

$en = @{
    "Language" = "Language"
    "Explore" = "Explore"
    "Plant_Library" = "Plant Library"
    "My_Garden" = "My Garden"
    "Profile" = "Profile"
    "Manage" = "Manage"
    "Login" = "Login"
    "Register" = "Register"
    "Logout" = "Logout"
}

$vi = @{
    "Language" = "Ngôn ngữ"
    "Explore" = "Khám phá"
    "Plant_Library" = "Thư viện cây"
    "My_Garden" = "Khu vườn của tôi"
    "Profile" = "Hồ sơ"
    "Manage" = "Quản lý"
    "Login" = "Đăng nhập"
    "Register" = "Đăng ký"
    "Logout" = "Đăng xuất"
}

$fr = @{
    "Language" = "Langue"
    "Explore" = "Explorer"
    "Plant_Library" = "Bibliothèque de plantes"
    "My_Garden" = "Mon Jardin"
    "Profile" = "Profil"
    "Manage" = "Gérer"
    "Login" = "Se connecter"
    "Register" = "S'inscrire"
    "Logout" = "Se déconnecter"
}

$ja = @{
    "Language" = "言語"
    "Explore" = "探検"
    "Plant_Library" = "植物図鑑"
    "My_Garden" = "マイガーデン"
    "Profile" = "プロフィール"
    "Manage" = "管理"
    "Login" = "ログイン"
    "Register" = "登録"
    "Logout" = "ログアウト"
}

Create-Resx ".\Resources\SharedResource.en.resx" $en
Create-Resx ".\Resources\SharedResource.vi.resx" $vi
Create-Resx ".\Resources\SharedResource.fr.resx" $fr
Create-Resx ".\Resources\SharedResource.ja.resx" $ja
