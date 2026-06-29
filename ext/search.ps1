#Requires -Version 5.1
<#
.SYNOPSIS
  LRS 搜索扩展 - 自包含 PowerShell 搜索脚本

.DESCRIPTION
  被 LRS 扩展系统通过 Process.Start 调用。
  在指定目录递归枚举匹配的文件名，结果在脚本进程内用 WPF DataGrid 显示。
  完全自包含：搜索、展示、打开结果；不调起资源管理器（不使用 search-ms: / explorer.exe search）。

.PARAMETER SearchDir
  搜索根目录（必填）。

.PARAMETER PresetFilePath
  可选。提供时脚本会取其文件名（去扩展）作为预填搜索词。
  用于"右键文件 → 搜索同名文件"场景。
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SearchDir,

    [string]$PresetFilePath = ""
)

$ErrorActionPreference = 'Stop'

# ---- 1) 加载 WinForms（用于消息框与输入框） ----
try {
    Add-Type -AssemblyName System.Windows.Forms | Out-Null
} catch {
    Write-Host "LRS-Search: WinForms load failed: $($_.Exception.Message)"
    exit 1
}

# ---- 2) 校验搜索目录 ----
if (-not (Test-Path -LiteralPath $SearchDir -PathType Container)) {
    [System.Windows.Forms.MessageBox]::Show(
        "搜索目录不存在:`n$SearchDir",
        "LRS 搜索",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
    exit 1
}

# ---- 3) 计算搜索词 ----
$filter = ""
if (-not [string]::IsNullOrWhiteSpace($PresetFilePath)) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($PresetFilePath)
    if (-not [string]::IsNullOrWhiteSpace($name)) {
        $filter = "*$name*"
    }
}

if ([string]::IsNullOrWhiteSpace($filter)) {
    Add-Type -AssemblyName Microsoft.VisualBasic | Out-Null
    $inputBoxResult = [Microsoft.VisualBasic.Interaction]::InputBox(
        "在下方输入要在以下目录中搜索的文件名（支持通配符 * 与 ?）:`n`n$SearchDir",
        "LRS 搜索",
        "")
    if ([string]::IsNullOrWhiteSpace($inputBoxResult)) { exit 0 }
    $filter = $inputBoxResult
}

# ---- 4) 递归搜索（限前 1000 个） ----
$results = $null
try {
    $results = Get-ChildItem -LiteralPath $SearchDir -Filter $filter -Recurse -File -ErrorAction SilentlyContinue |
        Select-Object -First 1000 -Property Name, DirectoryName, FullName, Length, LastWriteTime
} catch {
    [System.Windows.Forms.MessageBox]::Show(
        "搜索出错:`n$($_.Exception.Message)",
        "LRS 搜索",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
    exit 1
}

if ($null -eq $results -or @($results).Count -eq 0) {
    [System.Windows.Forms.MessageBox]::Show(
        "未找到匹配项:`n$filter",
        "LRS 搜索",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
    exit 0
}

# ---- 5) 加载 WPF ----
try {
    Add-Type -AssemblyName PresentationFramework | Out-Null
    Add-Type -AssemblyName PresentationCore | Out-Null
    Add-Type -AssemblyName WindowsBase | Out-Null
} catch {
    [System.Windows.Forms.MessageBox]::Show(
        "加载 WPF 失败:`n$($_.Exception.Message)",
        "LRS 搜索",
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
    exit 1
}

# ---- 6) 把结果包装成可绑定对象（大小格式化为人类可读） ----
$obs = New-Object System.Collections.ObjectModel.ObservableCollection[Object]
foreach ($r in $results) {
    $len = [int64]$r.Length
    $lenText =
        if ($len -lt 1024)            { "$len B" }
        elseif ($len -lt 1048576)      { "{0:N1} KB" -f ($len / 1024.0) }
        elseif ($len -lt 1073741824)   { "{0:N2} MB" -f ($len / 1048576.0) }
        else                           { "{0:N2} GB" -f ($len / 1073741824.0) }

    $obs.Add([pscustomobject]@{
        Name             = $r.Name
        DirectoryName    = $r.DirectoryName
        FullName         = $r.FullName
        LengthText       = $lenText
        LastWriteTimeText = $r.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
    })
}

# ---- 7) 构造 WPF 窗口（XAML 内联） ----
# 注：$obs.Count / $filter / $SearchDir 都会被 PowerShell here-string 展开
#     XAML 内的 {Binding ...} 因为没有 $ 前缀，会被原样保留，XamlReader 能正确解析
$countText = "$($obs.Count) 个匹配（最多 1000）"
$xamlText = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="LRS 搜索结果 - $filter"
        Width="900" Height="500"
        WindowStartupLocation="CenterScreen"
        ShowInTaskbar="True">
    <DockPanel LastChildFill="True">
        <Border DockPanel.Dock="Top" Background="#F0F0F0" Padding="10,6">
            <StackPanel>
                <TextBlock FontSize="13" Foreground="#333"
                           Text="在 $SearchDir 中找到 $countText" />
                <TextBlock FontSize="11" Foreground="#888" Margin="0,3,0,0"
                           Text="双击以默认应用打开；关闭窗口返回 LRS" />
            </StackPanel>
        </Border>
        <DataGrid x:Name="ResultsGrid"
                  AutoGenerateColumns="False"
                  IsReadOnly="True"
                  AlternatingRowBackground="#F8F8F8"
                  GridLinesVisibility="Horizontal"
                  HeadersVisibility="Column"
                  Margin="6">
            <DataGrid.Columns>
                <DataGridTextColumn Header="文件名" Binding="{Binding Name}" Width="200" />
                <DataGridTextColumn Header="所在目录" Binding="{Binding DirectoryName}" Width="*" />
                <DataGridTextColumn Header="大小" Binding="{Binding LengthText}" Width="90" />
                <DataGridTextColumn Header="修改时间" Binding="{Binding LastWriteTimeText}" Width="140" />
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</Window>
"@

# 解析 XAML（这里需要 XmlNodeReader 走 XamlReader）
[xml]$xamlDoc = $xamlText
$reader = New-Object System.Xml.XmlNodeReader $xamlDoc
$window = [Windows.Markup.XamlReader]::Load($reader)
$grid = $window.FindName("ResultsGrid")
$grid.ItemsSource = $obs

# ---- 8) 双击行 → 用默认应用打开 ----
# 注：捕获 $obs / $window 等当前脚本变量；用 GetNewClosure() 显式保留闭包
$openHandler = {
    param($sender, $e)
    $item = $sender.SelectedItem
    if ($item -and $item.FullName) {
        try {
            Start-Process -FilePath $item.FullName -ErrorAction Stop
        } catch {
            [System.Windows.Forms.MessageBox]::Show(
                "打开失败:`n$($_.Exception.Message)`n`n$($item.FullName)",
                "LRS 搜索",
                [System.Windows.Forms.MessageBoxButtons]::OK,
                [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
        }
    }
}.GetNewClosure()

$grid.Add_MouseDoubleClick($openHandler)

# 模态显示；窗口关闭后进程退出 0
[void]$window.ShowDialog()
exit 0
