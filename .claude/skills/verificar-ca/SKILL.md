---
name: verificar-ca
description: Verifica critérios de aceite (CA-xx) do Mini Gestão de Pedidos ligando spec e testes. Use quando pedirem para verificar, conferir ou rastrear um ou mais CA, ou "todos os CA".
---

# verificar-ca

Liga `docs/spec.md` (seção 6) aos testes marcados com `[Trait("CA", "CA-xx")]` e só declara verificado o que foi executado nesta rodada.

## Entrada

Um ou mais CA (`CA-14`, `CA-05b`, `CA-18 a CA-25`) ou `todos`.

## Passos

1. **Ler os critérios.** Em `docs/spec.md`, seção 6, leia o texto de cada CA pedido. Para `todos`, liste todos os IDs da seção.
2. **Localizar os testes.** Procure `Trait("CA", "CA-xx")` em `tests/`:
   `grep -rn 'Trait("CA", "CA-xx")' tests`
   Registre o arquivo e o método de cada ocorrência.
3. **Executar.** Para cada CA com teste: `dotnet test Pedidos.slnx --filter "CA=CA-xx"`. Para vários, rode um filtro por CA (ou combine com `|`). Os testes de integração precisam do Docker em execução (Testcontainers). Se o Docker ou o SDK 10 não estiverem disponíveis, **não execute nada e diga isso**: os CA ficam como "não executado".
4. **Emitir a tabela** `CA | critério (resumo) | teste (arquivo > método) | resultado`, com um destes resultados:
   - `passou` / `falhou` (com a saída do teste);
   - `sem teste`: existe na spec e não há `Trait` correspondente;
   - `manual`: não é automatizado por desenho;
   - `não executado`: havia teste, mas a rodada não o executou.
5. **Nunca** marcar um CA como verificado sem ter rodado o teste nesta rodada. Teste existente não é teste executado.

## CA que não são automatizados

| CA | Como verificar |
|---|---|
| CA-33 | `powershell -File scripts/verificar-compose.ps1` (**apaga o volume do banco**, só em desenvolvimento) |
| CA-38 a CA-40, CA-43 a CA-45 | Roteiro manual do frontend no README |
| CA-41, CA-42 | Estrutura dos projetos de teste (unitários com NSubstitute; integração com SQL Server real); conferir por inspeção |

## Regras

- A spec manda: se o teste e o CA divergirem, aponte a divergência em vez de ajustar um deles em silêncio.
- Não rode `docker compose down -v` nem `verificar-compose.ps1` sem pedido explícito do usuário.
