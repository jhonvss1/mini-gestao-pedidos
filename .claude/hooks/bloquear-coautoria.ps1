# Hook PreToolUse (Bash): recusa "git commit" cuja mensagem tenha um trailer Co-Authored-By.
# Impõe por mecanismo a regra do CLAUDE.md (seção Git): commits sem coautoria. Sai com 2 para bloquear.
#
# Só bloqueia o trailer de verdade ("Co-Authored-By: Nome <email@dominio>"). Mencionar o termo em texto, como numa
# documentação ou numa mensagem que explica a regra, não é coautoria e passa.

try {
    $entrada = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $comando = [string]$entrada.tool_input.command

    $ehCommit = $comando -match 'git\s+(\S+\s+)*commit'
    $temTrailer = $comando -match '(?i)co-authored-by:\s*[^<\r\n"]+<[^>\r\n]+@[^>\r\n]+>'

    if ($ehCommit -and $temTrailer) {
        [Console]::Error.WriteLine('Commit bloqueado: este projeto proibe Co-Authored-By nas mensagens (CLAUDE.md, secao Git). Refaca o commit sem a linha de coautoria.')
        exit 2
    }
}
catch {
    # Falha do próprio hook não deve interromper o fluxo.
}

exit 0
