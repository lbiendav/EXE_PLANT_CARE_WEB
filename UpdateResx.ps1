$ErrorActionPreference = "Stop"

function Add-ResxKeys {
    param (
        [string]$Path,
        [hashtable]$Entries
    )
    [xml]$xml = Get-Content $Path
    
    foreach ($key in $Entries.Keys) {
        # Check if key exists
        $node = $xml.root.SelectSingleNode("data[@name='$key']")
        if ($null -eq $node) {
            $data = $xml.CreateElement("data")
            $data.SetAttribute("name", $key)
            $data.SetAttribute("xml:space", "preserve")
            
            $value = $xml.CreateElement("value")
            $value.InnerText = $Entries[$key]
            
            $data.AppendChild($value) | Out-Null
            $xml.root.AppendChild($data) | Out-Null
        } else {
            $node.value = $Entries[$key]
        }
    }
    
    $xml.Save($Path)
    Write-Host "Updated $Path"
}

$en = @{
    "Home_Hero_Title" = "Bring Nature Into Your Home"
    "Home_Hero_Desc" = "Explore hundreds of plants, care guides, and manage your personal garden."
    "Home_Explore_Btn" = "Explore Plant Library"
    "Home_CareGuide_Title" = "Care Guides"
    "Home_ReadMore" = "Read more"
    "Home_Why_Title" = "Why choose HomePlant?"
    "Home_Why_Desc" = "HomePlant helps beginners easily care for plants through a rich library of knowledge, disease handling guides, and personal garden management."
}

$vi = @{
    "Home_Hero_Title" = "Mang Thiên Nhiên Vào Ngôi Nhà Của Bạn"
    "Home_Hero_Desc" = "Khám phá hàng trăm loại cây cảnh, cẩm nang chăm sóc và quản lý khu vườn của bạn."
    "Home_Explore_Btn" = "Khám phá thư viện cây"
    "Home_CareGuide_Title" = "Cẩm nang chăm sóc"
    "Home_ReadMore" = "Đọc thêm"
    "Home_Why_Title" = "Tại sao chọn HomePlant?"
    "Home_Why_Desc" = "HomePlant giúp người mới bắt đầu dễ dàng chăm sóc cây cảnh thông qua thư viện kiến thức phong phú, hướng dẫn xử lý bệnh và quản lý khu vườn cá nhân."
}

$fr = @{
    "Home_Hero_Title" = "Faites entrer la nature chez vous"
    "Home_Hero_Desc" = "Découvrez des centaines de plantes, des guides d'entretien et gérez votre jardin."
    "Home_Explore_Btn" = "Explorer la bibliothèque de plantes"
    "Home_CareGuide_Title" = "Guides d'entretien"
    "Home_ReadMore" = "Lire la suite"
    "Home_Why_Title" = "Pourquoi choisir HomePlant?"
    "Home_Why_Desc" = "HomePlant aide les débutants à entretenir facilement leurs plantes grâce à une riche bibliothèque de connaissances."
}

$ja = @{
    "Home_Hero_Title" = "自然を家に取り入れる"
    "Home_Hero_Desc" = "何百もの植物、お手入れガイドを探索し、あなたの庭を管理しましょう。"
    "Home_Explore_Btn" = "植物図鑑を見る"
    "Home_CareGuide_Title" = "お手入れガイド"
    "Home_ReadMore" = "続きを読む"
    "Home_Why_Title" = "HomePlantを選ぶ理由"
    "Home_Why_Desc" = "HomePlantは、豊富な知識ライブラリを通じて、初心者が簡単に植物の世話をするのを助けます。"
}

$base = (Get-Item ".\Resources").FullName
Add-ResxKeys "$base\SharedResource.en.resx" $en
Add-ResxKeys "$base\SharedResource.vi.resx" $vi
Add-ResxKeys "$base\SharedResource.fr.resx" $fr
Add-ResxKeys "$base\SharedResource.ja.resx" $ja
