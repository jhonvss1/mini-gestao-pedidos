---
name: verificar-ca
description: Verifica critérios de aceite (CA-xx) da spec contra os testes. Gera a matriz CA x teste x resultado, executando os testes de verdade. Use quando o usuário pedir para verificar, auditar ou conferir um ou mais critérios de aceite, ou "todos".
---

# Verificar critérios de aceite

Entrada: um ou mais critérios (`CA-14`, `CA-05 a CA-09`) ou "todos".

## Passos
1. **Ler os critérios** em `docs/spec.md`, seção 6 ("Critérios de aceite verificáveis"). Cite o texto de cada um.
2. **Localizar os testes**: todo teste de critério tem `[Trait("CA", "CA-xx")]`. Procure com Grep em `tests/` (um mesmo teste pode carregar mais de um trait).
3. **Executar de verdade**, critério a critério:

   ```bash
   dotnet test Pedidos.slnx --filter "CA=CA-14"
   ```

   Os testes de integração sobem um SQL Server pelo Testcontainers, então o Docker precisa estar ativo. Se a API local estiver rodando (ela trava as DLLs em `bin/`), acrescente `--artifacts-path .artifacts`.
4. **Montar a matriz** com uma linha por critério: critério, testes encontrados, resultado desta rodada (passou, falhou ou não executado).
5. **Classificar o que não é automatizado**:
   - Sem teste e deveria ter: marque **sem teste**.
   - Manuais por definição da spec: CA-33 (`scripts/verificar-compose.ps1`, apaga o volume do banco em desenvolvimento), CA-38 a CA-40 e CA-43 a CA-45 (roteiro manual do README). Aponte o procedimento; não declare verificados.

## Regras
- **Nunca declare um critério verificado sem ter executado o teste nesta rodada.** Resultado de rodada anterior não vale.
- Se um teste falhar, mostre a mensagem de erro e aponte o arquivo e a linha; não corrija sem o usuário pedir.
- Não rode `verificar-compose.ps1` sem avisar que ele apaga o volume do banco.
- Termine com um resumo: quantos passaram, quantos falharam, quantos sem teste e quantos manuais.
