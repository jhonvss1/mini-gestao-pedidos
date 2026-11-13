-- Query do enunciado (Parte 4), SEM correção, só traduzida para os nomes do projeto:
--   Clientes -> cliente, Pedidos -> pedido, ItensPedido -> item_pedido, DataPedido -> data_criacao (D-17).

SELECT c.nome, SUM(i.quantidade * i.preco_unitario) AS total
FROM cliente c
LEFT JOIN pedido p ON p.cliente_id = c.id
LEFT JOIN item_pedido i ON i.pedido_id = p.id
WHERE YEAR(p.data_criacao) = 2026
GROUP BY c.nome
ORDER BY total;
