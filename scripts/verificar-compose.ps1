<#
.SYNOPSIS
    Verifica a execução completa pelo Docker Compose (CA-33) e a idempotência dos scripts de banco (CA-34).

.DESCRIPTION
    1. Derruba tudo e apaga o volume do banco (a menos que -ManterDados seja usado).
    2. Sobe db, db-init, api e frontend com build, esperando os healthchecks.
    3. Confere o estado dos serviços e a contagem da carga inicial no SQL Server.
    4. Chama a API pelo próprio frontend (proxy do nginx): GET, POST válido e POST com estoque insuficiente.
    5. Executa o db-init de novo e confere que nada foi duplicado.

    ATENÇÃO: sem -ManterDados o script roda "docker compose down -v", que APAGA os dados do banco de desenvolvimento.

.EXAMPLE
    powershell -File scripts/verificar-compose.ps1
#>
[CmdletBinding()]
param(
    [int]$PortaFrontend = 4200,
    [switch]$ManterDados
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)

$script:falhas = New-Object System.Collections.Generic.List[string]

function Passo([string]$Texto) { Write-Host "`n== $Texto" -ForegroundColor Cyan }
function Ok([string]$Texto) { Write-Host "  [OK]    $Texto" -ForegroundColor Green }
function Falha([string]$Texto) {
    Write-Host "  [FALHA] $Texto" -ForegroundColor Red
    $script:falhas.Add($Texto)
}
function Conferir([bool]$Condicao, [string]$Texto) {
    if ($Condicao) { Ok $Texto } else { Falha $Texto }
}

function ExecutarDocker {
    & docker @args
    if ($LASTEXITCODE -ne 0) { throw "docker $($args -join ' ') falhou (código $LASTEXITCODE)." }
}

# Consulta o SQL Server dentro do container. A consulta vai pelo stdin: evita o problema de aspas do PS 5.1.
function Contagens {
    $senha = (& docker compose exec -T db printenv MSSQL_SA_PASSWORD).Trim()
    $consulta = "SET NOCOUNT ON;`nSELECT (SELECT COUNT(*) FROM cliente), (SELECT COUNT(*) FROM produto), (SELECT COUNT(*) FROM pedido), (SELECT COUNT(*) FROM item_pedido);`nGO"
    $saida = $consulta | & docker compose exec -T db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P $senha -d gestao_pedidos -h -1 -W -s ','
    $linha = $saida | Where-Object { $_ -match '^\d+,\d+,\d+,\d+$' } | Select-Object -First 1
    if (-not $linha) { throw "Não foi possível ler as contagens do banco: $saida" }
    return $linha.Trim()
}

# O Windows PowerShell 5.1 entrega um array JSON como UM objeto: contar com @(...) daria 1. Percorre os itens de fato.
function ContarItens([string]$Json) {
    $lista = $Json | ConvertFrom-Json
    return @($lista | ForEach-Object { $_ }).Count
}

function Requisitar([string]$Metodo, [string]$Url, [string]$Corpo) {
    $parametros = @{ UseBasicParsing = $true; Method = $Metodo; Uri = $Url; TimeoutSec = 30 }
    if ($Corpo) {
        $parametros.Body = $Corpo
        $parametros.ContentType = 'application/json'
    }
    if ($PSVersionTable.PSVersion.Major -ge 7) { $parametros.SkipHttpErrorCheck = $true }

    try {
        $resposta = Invoke-WebRequest @parametros
        return @{ Status = [int]$resposta.StatusCode; Corpo = [string]$resposta.Content }
    }
    catch {
        # Windows PowerShell 5.1 lança exceção para 4xx/5xx: lê o status e o corpo da resposta.
        $resposta = $_.Exception.Response
        if ($null -eq $resposta) { throw }
        $leitor = New-Object System.IO.StreamReader($resposta.GetResponseStream())
        return @{ Status = [int]$resposta.StatusCode; Corpo = $leitor.ReadToEnd() }
    }
}

$base = "http://localhost:$PortaFrontend"

try {
    Passo 'Subindo o ambiente'
    if ($ManterDados) {
        Write-Host '  (mantendo o volume do banco)'
    }
    else {
        ExecutarDocker compose down -v --remove-orphans
    }
    ExecutarDocker compose up -d --build --wait

    Passo 'Estado dos serviços'
    $servicos = @(& docker compose ps -a --format json | ForEach-Object { $_ | ConvertFrom-Json })
    foreach ($nome in 'db', 'api', 'frontend') {
        $s = $servicos | Where-Object { $_.Service -eq $nome } | Select-Object -First 1
        Conferir ($null -ne $s -and $s.State -eq 'running' -and $s.Health -eq 'healthy') "$nome em execução e saudável"
    }
    $init = $servicos | Where-Object { $_.Service -eq 'db-init' } | Select-Object -First 1
    Conferir ($null -ne $init -and $init.State -eq 'exited' -and $init.ExitCode -eq 0) 'db-init concluído com sucesso (código 0)'

    Passo 'Carga inicial no SQL Server'
    $inicial = Contagens
    Conferir ($inicial -eq '8,7,16,22') "contagens (clientes, produtos, pedidos, itens) = $inicial, esperado 8,7,16,22"

    Passo "Frontend e API pelo proxy ($base)"
    $r = Requisitar 'GET' "$base/" $null
    Conferir ($r.Status -eq 200 -and $r.Corpo -match '<app-root') 'GET / serve o frontend'

    $r = Requisitar 'GET' "$base/pedidos/novo" $null
    Conferir ($r.Status -eq 200 -and $r.Corpo -match '<app-root') 'GET /pedidos/novo cai no index.html (rotas do Angular)'

    $r = Requisitar 'GET' "$base/api/pedidos" $null
    $total = if ($r.Status -eq 200) { ($r.Corpo | ConvertFrom-Json).totalItens } else { -1 }
    Conferir ($r.Status -eq 200 -and $total -eq 16) "GET /api/pedidos retorna 200 com totalItens = $total (esperado 16)"

    $r = Requisitar 'GET' "$base/api/relatorios/top-clientes?dataInicio=2026-10-01&dataFim=2026-10-31" $null
    $ids = if ($r.Status -eq 200) { (($r.Corpo | ConvertFrom-Json) | ForEach-Object { $_.clienteId }) -join ',' } else { '' }
    Conferir ($ids -eq '1,3,5,2,4') "GET /api/relatorios/top-clientes retorna os clientes $ids (esperado 1,3,5,2,4)"

    $r = Requisitar 'GET' "$base/api/clientes" $null
    Conferir ($r.Status -eq 200 -and (ContarItens $r.Corpo) -eq 8) 'GET /api/clientes retorna 8 clientes'

    $r = Requisitar 'GET' "$base/api/produtos" $null
    Conferir ($r.Status -eq 200 -and (ContarItens $r.Corpo) -eq 7) 'GET /api/produtos retorna 7 produtos'

    $r = Requisitar 'POST' "$base/api/pedidos" '{"clienteId":1,"itens":[{"produtoId":1,"quantidade":1}]}'
    $valor = if ($r.Status -eq 201) { ($r.Corpo | ConvertFrom-Json).valorTotal } else { $null }
    Conferir ($r.Status -eq 201 -and $valor -eq 89.9) "POST /api/pedidos válido retorna 201 com valorTotal = $valor (esperado 89,90)"

    $r = Requisitar 'POST' "$base/api/pedidos" '{"clienteId":1,"itens":[{"produtoId":5,"quantidade":1}]}'
    Conferir ($r.Status -eq 409 -and $r.Corpo -match 'ESTOQUE_INSUFICIENTE') 'POST /api/pedidos sem estoque retorna 409 ESTOQUE_INSUFICIENTE'

    $aposPost = Contagens
    Conferir ($aposPost -eq '8,7,17,23') "o POST valido gravou 1 pedido e 1 item (contagens $aposPost, esperado 8,7,17,23)"

    Passo 'Idempotência do db-init (CA-34)'
    ExecutarDocker compose run --rm db-init
    $aposInit = Contagens
    Conferir ($aposInit -eq $aposPost) "reexecutar o db-init não alterou os dados ($aposInit)"
}
catch {
    Falha "erro durante a verificação: $($_.Exception.Message)"
}

Write-Host ''
if ($script:falhas.Count -eq 0) {
    Write-Host 'Verificacao do Compose concluida: tudo OK.' -ForegroundColor Green
    Write-Host "O ambiente continua no ar em $base. Para parar: docker compose down  (use -v só se quiser apagar o banco)."
    exit 0
}

Write-Host "Verificação do Compose FALHOU ($($script:falhas.Count) problema(s)):" -ForegroundColor Red
$script:falhas | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
exit 1
