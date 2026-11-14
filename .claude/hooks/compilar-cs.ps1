# Hook PostToolUse (Edit|Write|MultiEdit): compila o projeto dono do arquivo .cs editado e devolve os erros ao Claude.
# Lê o JSON do hook na entrada padrão. Sai com 0 se compilou (ou se não se aplica) e com 2 se o build falhou: o stderr
# volta para o Claude, que corrige na hora. Usa --artifacts-path .artifacts para não colidir com a API local rodando
# (ela trava as DLLs de bin/) nem com o build normal.

try {
    $entrada = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $arquivo = $entrada.tool_input.file_path

    if (-not $arquivo -or $arquivo -notlike '*.cs' -or -not (Test-Path -LiteralPath $arquivo)) { exit 0 }

    $raiz = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $completo = (Resolve-Path -LiteralPath $arquivo).Path
    if (-not $completo.StartsWith($raiz, [StringComparison]::OrdinalIgnoreCase)) { exit 0 }

    # Projeto dono do arquivo: o primeiro .csproj subindo as pastas.
    $pasta = Split-Path -Parent $completo
    $projeto = $null
    while ($pasta -and $pasta.Length -ge $raiz.Length) {
        $achado = Get-ChildItem -LiteralPath $pasta -Filter '*.csproj' -File -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($achado) { $projeto = $achado.FullName; break }
        $pasta = Split-Path -Parent $pasta
    }
    if (-not $projeto) { exit 0 }

    Push-Location $raiz
    try {
        $saida = & dotnet build $projeto --artifacts-path (Join-Path $raiz '.artifacts') --nologo -v q -clp:ErrorsOnly 2>&1 | Out-String
        $codigo = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($codigo -ne 0) {
        [Console]::Error.WriteLine("O build de $(Split-Path -Leaf $projeto) falhou depois da edicao:")
        [Console]::Error.WriteLine($saida.Trim())
        exit 2
    }
}
catch {
    # Falha do próprio hook não deve interromper a edição.
}

exit 0
