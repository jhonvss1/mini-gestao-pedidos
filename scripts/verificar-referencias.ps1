# Confere que as referências entre projetos respeitam a direção de dependências do plano (seção 1.2).
# Uso: powershell -File scripts/verificar-referencias.ps1

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot

# Referências permitidas (ProjectReference) por projeto de src/.
$permitidas = @{
    'Pedidos.Domain'         = @()
    'Pedidos.Application'    = @('Pedidos.Domain')
    'Pedidos.Infrastructure' = @('Pedidos.Application')
    'Pedidos.Api'            = @('Pedidos.Application', 'Pedidos.Infrastructure')
}

$falhas = @()

foreach ($projeto in $permitidas.Keys) {
    $arquivo = Join-Path $raiz "src/$projeto/$projeto.csproj"
    if (-not (Test-Path $arquivo)) {
        $falhas += "Projeto ausente: $arquivo"
        continue
    }

    [xml]$xml = Get-Content $arquivo
    $referencias = @($xml.Project.ItemGroup.ProjectReference |
        Where-Object { $_ } |
        ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_.Include) })

    $esperadas = $permitidas[$projeto]

    foreach ($ref in $referencias) {
        if ($ref -notin $esperadas) { $falhas += "$projeto referencia $ref, o que não é permitido." }
    }
    foreach ($ref in $esperadas) {
        if ($ref -notin $referencias) { $falhas += "$projeto deveria referenciar $ref." }
    }
}

if ($falhas.Count -gt 0) {
    $falhas | ForEach-Object { Write-Host "FALHA: $_" -ForegroundColor Red }
    exit 1
}

Write-Host 'Referências entre projetos conformes ao plano.' -ForegroundColor Green
