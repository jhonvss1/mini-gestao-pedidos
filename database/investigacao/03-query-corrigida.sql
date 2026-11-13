-- Versão corrigida: ranking dos clientes que mais compraram em 2026.
--  * faixa de datas em vez de YEAR(coluna): o predicado vira sargable e pode usar Seek no índice;
--  * agrega pedido por cliente_id ANTES de juntar com cliente (uma tabela só, sem multiplicar linhas por item);
--  * agrupa por id (homônimos continuam separados), nunca por nome;
--  * ORDER BY total DESC com desempate por id (resultado determinístico);
--  * TOP (5) como no endpoint de ranking. Para a lista completa, remova o TOP.
-- valor_total é a soma dos itens (RN-05); 04-comparar-resultados.sql prova a equivalência com SUM(quantidade * preco_unitario).

SELECT TOP (5) c.id, c.nome, t.total
FROM (
    SELECT p.cliente_id, SUM(p.valor_total) AS total
    FROM pedido p
    WHERE p.data_criacao >= '2026-01-01' AND p.data_criacao < '2027-01-01'
    GROUP BY p.cliente_id
) t
JOIN cliente c ON c.id = t.cliente_id
ORDER BY t.total DESC, c.id ASC;
