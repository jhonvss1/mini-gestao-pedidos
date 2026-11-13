# Hook PostToolUse (Edit|Write|MultiEdit): normaliza espaços em branco de arquivos .cs editados.
# Lê o JSON do hook na entrada padrão. Nunca bloqueia o fluxo: sempre sai com código 0.

try {
    $entrada = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $arquivo = $entrada.tool_input.file_path

    if ($arquivo -and $arquivo -like '*.cs' -and (Test-Path -LiteralPath $arquivo)) {
        $raiz = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
        # Windows PowerShell 5.1 não tem [IO.Path]::GetRelativePath.
        $completo = (Resolve-Path -LiteralPath $arquivo).Path
        if (-not $completo.StartsWith($raiz, [StringComparison]::OrdinalIgnoreCase)) { exit 0 }
        $relativo = $completo.Substring($raiz.Length).TrimStart('\', '/')
        Push-Location $raiz
        try {
            dotnet format whitespace . --folder --include $relativo --verbosity quiet | Out-Null
        }
        finally {
            Pop-Location
        }
    }
}
catch {
    # Falha de formatação não deve interromper a edição.
}

exit 0
