# 模板說明
---
## 指令

`1. 安裝模板`
```
dotnet new install {FolderPath}
# 範例：
dotnet new install .\AetherCoreTemplates\FrameworkTemplate

```

`2. 查詢模板列表`
```
dotnet new -l
```

`3. 使用模板`
```
dotnet new {shortName} -n {專案名稱} --force
# 範例：
dotnet new aethercore-fw -n XYZ --force
```

`刪除模板`
```
dotnet new uninstall {FolderPath}
# 範例：
dotnet new uninstall .\AetherCoreTemplates\FrameworkTemplate
```