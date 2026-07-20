# publish.ps1 — 폐쇄망 반입용 self-contained 단일 exe 생성
# 결과물 publish\dpack.exe 파일 하나만 폐쇄망에 반입하면 됩니다(.NET 설치 여부 무관).
# 버전은 루트 VERSION 파일에서 자동으로 읽어 exe에 새겨집니다.
#   PowerShell 에서:   .\publish.ps1

$ver = (Get-Content -Raw VERSION).Trim()
Write-Host "dpack $ver — self-contained 단일 exe 빌드 중..."

dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o publish --nologo

if ($LASTEXITCODE -eq 0) {
    $sz = [math]::Round((Get-Item publish\dpack.exe).Length / 1MB, 1)
    Write-Host "OK -> publish\dpack.exe  (v$ver, $sz MB)"
} else {
    Write-Host "빌드 실패 (exit=$LASTEXITCODE)"
    exit $LASTEXITCODE
}
