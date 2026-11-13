# Produção: AWS (EC2) + Cloudflare Tunnel

Guia para publicar a aplicação em **uma instância EC2** (região `us-east-1`) com o banco em container, usando o **Cloudflare Tunnel** para expor o site **sem abrir nenhuma porta de entrada** no servidor. O que é código (Compose de produção, scripts, health check) está no repositório; o que é criação de recursos na AWS e na Cloudflare está descrito aqui para você executar.

> **Regra de ouro:** nunca rode `docker compose down -v` nem `scripts/verificar-compose.ps1` em produção. Os dois **apagam o volume do banco**. Eles existem só para desenvolvimento.

## Visão geral

```
Internet ──► Cloudflare (HTTPS, Access) ──► túnel ──► [ EC2 ]
                                                       cloudflared ──► frontend (nginx) ──┬─► arquivos do Angular
                                                                                         └─► /api ──► API ──► SQL Server (Express)
```

- Nenhuma porta é publicada no host. O `cloudflared` sai da instância para a Cloudflare (conexão de saída), então o **grupo de segurança não precisa de nenhuma regra de entrada**.
- O HTTPS termina na Cloudflare. Dentro da instância o tráfego é HTTP pela rede privada do Compose.
- A API conecta ao banco com o login **restrito** `app_pedidos` (só o que ela usa), não com o `sa`.
- Os dados ficam no volume Docker `mssql-data`, no disco (EBS) da instância.

## 1. Antes de criar qualquer coisa na AWS

1. Ative **MFA** na conta e use um usuário IAM no dia a dia, não a conta raiz.
2. Crie um **alerta de orçamento** (AWS Budgets) com aviso em 50% e 80% dos créditos, e anote a data de expiração deles.
3. Tenha um domínio gerenciado na Cloudflare (por exemplo `pedidos.seudominio.com`).

## 2. Criar a instância (EC2, `us-east-1`)

| Item | Escolha | Motivo |
|---|---|---|
| AMI | Ubuntu Server 24.04 LTS | Suporte longo; o agente do SSM já vem instalado |
| Tipo | `t3.medium` (2 vCPU, 4 GB) | O SQL Server em container pede pelo menos 2 GB livres; `t3.small` fica no limite |
| Disco | EBS `gp3`, 30 GB, **criptografado** | Sistema, imagens Docker e volume do banco |
| Rede | IP público automático | Só para a **saída** (baixar pacotes e imagens, falar com a Cloudflare). Sem NAT, que custa mais |
| Grupo de segurança | **Nenhuma regra de entrada** | O acesso é pelo túnel e pelo SSM, não por porta aberta |
| Acesso administrativo | **SSM Session Manager** (perfil IAM `AmazonSSMManagedInstanceCore`) | Sem ssh aberto e sem chave para guardar |
| Metadados | IMDSv2 obrigatório | Boa prática de segurança |

Custo aproximado (estimativa, confira na calculadora da AWS): instância `t3.medium` sob demanda cerca de US$ 30 por mês, disco de 30 GB cerca de US$ 2,40 e IPv4 público cerca de US$ 3,60. Total na casa de US$ 36 por mês, coberto pelos créditos.

## 3. Preparar o servidor

Conecte pelo **Session Manager** (console da AWS, botão *Connect*). No servidor:

```bash
curl -fsSL https://get.docker.com | sudo sh
```

```bash
sudo usermod -aG docker "$USER" && newgrp docker
```

```bash
git clone <URL-do-seu-repositório> gestao-pedidos && cd gestao-pedidos
```

## 4. Cloudflare Tunnel

No painel **Zero Trust**: *Networks → Tunnels → Create a tunnel → Cloudflared*.

1. Dê um nome ao túnel e, na tela de instalação, escolha **Docker** e **copie apenas o token** (a parte depois de `--token`). O túnel roda como serviço do Compose, então não instale nada além disso.
2. Em **Public hostname**, adicione `pedidos.seudominio.com` apontando para o serviço **HTTP** `frontend:80` (é o nome do serviço dentro da rede do Compose).

## 5. Segredos

```bash
cp .env.producao.example .env.producao && chmod 600 .env.producao
```

Preencha `.env.producao`:

- `MSSQL_SA_PASSWORD`: senha do administrador do banco (usada só na criação do banco).
- `SENHA_APP`: senha do login `app_pedidos`, usado pela API. **Não pode conter `app_pedidos`.**
- `TUNNEL_TOKEN`: o token copiado na etapa anterior.
- `CARGA_INICIAL=true` **só se** for uma demonstração pública com dados de exemplo. Em produção real deixe `false`; o banco nasce vazio.

As duas senhas precisam ter 8 ou mais caracteres e três entre maiúscula, minúscula, dígito e símbolo. Evite aspas, `$` e `\`. Uma forma de gerar:

```bash
openssl rand -base64 24
```

O arquivo `.env.producao` está no `.gitignore`: **nunca o versione**.

## 6. Subir

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env.producao up -d --build
```

O Compose recusa subir se faltar alguma senha. Na primeira vez o build leva alguns minutos. Confira:

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env.producao ps
```

Esperado: `db`, `api`, `frontend` e `cloudflared` em execução (os três primeiros `healthy`) e `db-init` encerrado com código 0. Depois, no navegador:

- `https://pedidos.seudominio.com/api/saude` deve responder `Healthy` (a API consultou o banco).
- `https://pedidos.seudominio.com/` abre o frontend.

## 7. Proteger o acesso (recomendado)

A API **não tem autenticação**: qualquer pessoa com a URL consegue criar pedidos e zerar o estoque. Para uma demonstração pública, coloque o **Cloudflare Access** na frente, sem alterar o código:

*Zero Trust → Access → Applications → Add an application → Self-hosted*, com o hostname `pedidos.seudominio.com` e uma política *Allow* para os e-mails que podem acessar. O front e a API estão no mesmo hostname, então ambos ficam protegidos.

## 8. Atualizar a aplicação

```bash
git pull
```

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env.producao up -d --build
```

O `db-init` roda de novo e é idempotente (não duplica dados). Para voltar a uma versão anterior: `git checkout <tag ou commit>` e o mesmo comando de subida. Para trocar a senha do `app_pedidos`, altere `SENHA_APP` e rode o comando de subida: o `db-init` atualiza o login e a API reinicia com a nova senha.

## 9. Backup e restauração

O banco está num volume do Docker no disco da instância; **sem backup, perder o disco é perder os dados.** Há duas camadas:

1. **Backup lógico do SQL Server** (`scripts/backup-banco.sh`): gera o `.bak` com `CHECKSUM`, confere com `RESTORE VERIFYONLY`, comprime e aplica retenção (14 dias por padrão). Rode uma vez à mão e depois agende:

   ```bash
   scripts/backup-banco.sh
   ```

   ```bash
   (crontab -l 2>/dev/null; echo "0 3 * * * $PWD/scripts/backup-banco.sh >> /var/log/backup-gestao-pedidos.log 2>&1") | crontab -
   ```

   Para guardar uma cópia **fora do servidor**, crie um bucket S3, dê à instância um perfil IAM com `s3:PutObject` só nesse bucket e defina `S3_DESTINO=s3://seu-bucket/gestao-pedidos` (requer o AWS CLI instalado).

2. **Snapshots do disco (EBS)**: configure uma política no *Data Lifecycle Manager* para um snapshot diário, como segunda proteção.

**Teste a restauração antes de precisar dela.** Por padrão o script restaura num banco separado, sem tocar nos dados reais:

```bash
scripts/restaurar-banco.sh /var/backups/gestao-pedidos/<arquivo>.bak.gz
```

Ele cria `gestao_pedidos_verificacao` e mostra a contagem de pedidos. Para restaurar de verdade sobre a produção, informe `gestao_pedidos` como segundo argumento (o script pede que você digite `RESTAURAR`, para a API e o frontend e os religa no fim).

## 10. Operação do dia a dia

| Tarefa | Comando |
|---|---|
| Ver os logs da API | `docker compose -f docker-compose.yml -f docker-compose.prod.yml --env-file .env.producao logs -f api` |
| Reiniciar um serviço | `... restart api` |
| Estado e saúde | `... ps` e `https://pedidos.seudominio.com/api/saude` |
| Espaço em disco | `df -h` e `docker system df` |

Os serviços têm `restart: unless-stopped`, então voltam sozinhos após reinício da instância. Os logs têm rotação (3 arquivos de 10 MB por serviço). Mantenha o sistema operacional atualizado (`unattended-upgrades`) e refaça o build de tempos em tempos para receber correções das imagens-base.

## Limites conhecidos e próximos passos

- **Licenciamento:** a imagem do SQL Server usa por padrão a edição *Developer*, que **não é licenciada para produção**. O Compose de produção fixa a edição **Express** (gratuita, permitida para produção), com limite de **10 GB por banco** e uso de memória reduzido, suficiente para este projeto. Um banco criado como *Developer* não deve ser migrado de edição; produção começa do zero em Express.
- **Evolução do schema:** os scripts `01` só criam o que não existe. Para mudar o schema depois (nova coluna, por exemplo) falta um mecanismo de versionamento: scripts numerados aplicados uma vez com uma tabela de versão, ou uma ferramenta como DbUp. É uma decisão em aberto.
- **Sem pipeline automático:** o deploy é manual (`git pull` e `up --build`). Evolução natural: GitHub Actions rodando `dotnet test` e publicando as imagens.
- **Banco gerenciado:** se a aplicação crescer, o caminho é o Amazon RDS for SQL Server: o `db-init` e o login `app_pedidos` já funcionam contra qualquer SQL Server, bastando mudar o servidor na conexão.
- **Frontend na CDN:** servir o Angular pelo Cloudflare Pages exigiria configuração da URL da API por ambiente e CORS na API (hoje tudo é mesma origem, pelo nginx).
- **Observabilidade:** hoje só logs do Docker e o endpoint de saúde. Evolução: CloudWatch Logs e alarmes.
- **Proteção contra abuso:** sem autenticação nem limite de requisições na própria API; use Cloudflare Access e regras de rate limit.
