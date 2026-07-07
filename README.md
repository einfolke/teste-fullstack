<div align="center">

# Parking — Solução do Teste Fullstack

Sistema de gestão de estacionamento com clientes, veículos, importação em massa e faturamento proporcional.

![Exemplo GIF](./exemplogif.gif)

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![React](https://img.shields.io/badge/React-Vite-61DAFB?logo=react&logoColor=black)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-4169E1?logo=postgresql&logoColor=white)
![EF Core](https://img.shields.io/badge/EF%20Core-8.0-512BD4)

</div>

---

## Índice

- [Como executar](#como-executar)
- [Visão geral das entregas](#visão-geral-das-entregas)
- [Tarefa 1 — Edição de clientes com unicidade](#tarefa-1--edição-de-clientes-com-unicidade)
- [Tarefa 2 — Edição completa de veículos](#tarefa-2--edição-completa-de-veículos)
- [Tarefa 3 — Importação de CSV robusta](#tarefa-3--importação-de-csv-robusta)
- [Tarefa 4 — Faturamento proporcional](#tarefa-4--faturamento-proporcional)
- [Status Ativo / Inativo](#status-ativo--inativo)
- [Refinamentos de UI](#refinamentos-de-ui)
- [Testes automatizados](#testes-automatizados)
- [Decisões técnicas](#decisões-técnicas)

---

## Como executar

> **Pré-requisitos:** .NET 8 SDK, Node.js e PostgreSQL 17.

<table>
<tr><th>Passo</th><th>Comando</th></tr>
<tr>
<td><b>1. Banco</b></td>
<td>

```powershell
$env:PGPASSWORD='postgres'
& "C:\Program Files\PostgreSQL\17\bin\psql.exe" `
  -h localhost -U postgres -d parking_test -f scripts/seed.sql
```

</td>
</tr>
<tr>
<td><b>2. Backend</b><br><sub>http://localhost:5000</sub></td>
<td>

```powershell
cd src/backend
dotnet run
```

</td>
</tr>
<tr>
<td><b>3. Frontend</b><br><sub>http://localhost:5173</sub></td>
<td>

```powershell
$env:Path += ";C:\Program Files\nodejs"   # se o npm não estiver no PATH
cd src/frontend
npm install
npm run dev
```

</td>
</tr>
<tr>
<td><b>4. Testes</b></td>
<td>

```powershell
# pare a API antes — ela bloqueia o .exe durante o build
Get-Process -Name Parking.Api -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet test tests/Parking.Tests/Parking.Tests.csproj
```

</td>
</tr>
</table>

---

## Visão geral das entregas

| # | Entrega | Status |
|:--:|---------|:------:|
| 1 | Edição de clientes com unicidade Nome + Telefone | Concluído |
| 2 | Edição completa de veículos (modelo, ano, troca de cliente) | Concluído |
| 3 | Importação de CSV com relatório de erros linha a linha | Concluído |
| 4 | Faturamento proporcional aos dias de associação | Concluído |
| ★ | Status Ativo/Inativo (não fatura inativos) | Concluído |
| ★ | Refinamentos de UI (modal, combobox, badges) | Concluído |
| ★ | Suíte de testes automatizados (28 testes) | Concluído |

---

## Tarefa 1 — Edição de clientes com unicidade

Edição inline de clientes (nome, telefone, endereço, mensalista e mensalidade), com a regra de unicidade pela combinação **Nome + Telefone** — aplicada tanto na criação quanto na edição (excluindo o próprio registro). Ao deixar de ser mensalista, a mensalidade é zerada automaticamente.

**Validações no backend**

| Situação | Resposta |
|----------|----------|
| Nome vazio | `400 BadRequest` |
| Mensalista sem `ValorMensalidade > 0` | `400 BadRequest` |
| Nome + Telefone já existente | `409 Conflict` (mensagem específica) |

---

## Tarefa 2 — Edição completa de veículos

Permite editar **Modelo**, **Ano** e **trocar o cliente** do veículo. A troca preserva um **histórico de associação** (base da Tarefa 4): a vigência atual é encerrada e uma nova é aberta. Também foi corrigido o bug em que a atualização não refletia na tela (invalidação do cache do React Query).

---

## Tarefa 3 — Importação de CSV robusta

Parser de CSV próprio que respeita campos entre aspas com vírgulas e aspas escapadas, com numeração real de linha (cabeçalho = linha 1) e validação campo a campo.

**Retorno estruturado**

```json
{
  "processados": 10,
  "inseridos": 8,
  "falhas": 2,
  "erros": [
    { "linha": 4, "coluna": "placa", "motivo": "Placa inválida", "conteudo": "12X" }
  ]
}
```

A tela apresenta um resumo (processados / inseridos / falhas) e uma tabela de erros com **linha, coluna, motivo e conteúdo**, facilitando a correção do arquivo.

---

## Tarefa 4 — Faturamento proporcional

> **Regra:** a fatura considera apenas os dias em que cada veículo esteve associado ao cliente no mês. Se um veículo troca de dono no meio do mês, cada cliente paga proporcionalmente aos seus dias.

**Modelagem** — tabela de histórico `associacao_veiculo_cliente`:

| coluna | descrição |
|--------|-----------|
| `veiculo_id` / `cliente_id` | par da associação |
| `data_inicio` | início da vigência |
| `data_fim` | fim da vigência (`null` = vigente) |

**Cálculo**

```
valorPorDia = ValorMensalidade / diasNoMes
dias        = (min(fim, últimoDia) − max(início, primeiroDia)) + 1
total       = Σ (valorPorDia × dias)        // arredondado a 2 casas, AwayFromZero
```

A observação da fatura detalha placa e dias (ex.: `ABC1D23: 17/31 dias`). A geração é **idempotente**: rodar a mesma competência novamente não duplica faturas.

**Exemplo validado — competência 2025-08 (31 dias)**

| Cliente | Mensalidade | Valor | Detalhe |
|---------|:-----------:|:-----:|---------|
| João    | 189,90 | **294,04** | BRA1A23 31/31 · ABC1D23 17/31 |
| Carlos  | 159,90 | **299,17** | QWE1Z89 31/31 · JKL2M34 27/31 |
| Beatriz | 209,90 | **291,15** | ZTB3N56 31/31 · HGF4P77 12/31 |

O relatório exibe o **nome do cliente** (não o ID), via join com `Cliente.Nome`.

---

## Status Ativo / Inativo

Campo `Ativo` (padrão `true`) em **Cliente** e **Veículo**, com etiquetas visuais nas listas e botão de alternância.

<table>
<tr><th>Estado</th><th>Aparência</th><th>Efeito no faturamento</th></tr>
<tr><td>Ativo</td><td><code>badge verde</code></td><td>Gera cobrança normalmente</td></tr>
<tr><td>Inativo</td><td><code>badge vermelho</code></td><td><b>Não</b> gera cobrança</td></tr>
</table>

O faturamento filtra `Mensalista && Ativo` e ignora veículos inativos no rateio.

---

## Refinamentos de UI

- **Modal** (popup) para cadastro de novo cliente.
- **Combobox** de cliente com busca digitável e **insensível a acentos** (normalização NFD).
- Correção do recorte do dropdown dentro de tabelas com `overflow: hidden`, usando `position: fixed` calculado por `getBoundingClientRect`.

---

## Testes automatizados

Projeto **xUnit** com **EF Core InMemory** — executa sem depender do PostgreSQL. **28 testes** cobrindo:

| Suíte | Cobertura |
|-------|-----------|
| `FaturamentoServiceTests` | mês completo, período parcial, troca no meio do mês, cliente inativo, veículo inativo, não mensalista, idempotência |
| `ClientesControllerTests` | criação válida, mensalista sem valor, duplicidade Nome+Telefone, alteração de status, update inexistente |
| `VeiculosControllerTests` | criação + associação vigente, placa inválida/duplicada, troca de cliente, alteração de status |
| `PlacaServiceTests` | sanitização e validação de placas (Mercosul e padrão antigo) |

---

## Decisões técnicas

**1. Histórico de associação em tabela dedicada**
Em vez de guardar apenas o `cliente_id` no veículo, o vínculo é registrado com período de vigência. Só assim é possível faturar proporcionalmente respeitando trocas de dono no meio do mês, mantendo o passado auditável.

**2. `DateTime` com `Kind=Utc` nas consultas**
Evita o erro do Npgsql *"Cannot write DateTime with Kind=Unspecified"* em colunas `timestamptz`. Para a contagem de dias em memória, uso datas "puras" (`.Date`) separadas, evitando erro de ±1 dia por causa do horário `23:59:59` do limite do mês.

**3. Unicidade em dois níveis**
Validação no controller (mensagem amigável) **e** índice único no banco, como defesa contra condição de corrida.

**4. Soft-status (`Ativo`) em vez de exclusão física**
Inativar preserva o histórico (faturas, associações) e permite reativar. A exclusão física continua disponível, mas o fluxo recomendado é inativar.

**5. Arredondamento `MidpointRounding.AwayFromZero`**
Comportamento esperado para valores monetários.

**6. Idempotência do faturamento**
Gerar a mesma competência novamente não duplica faturas — o endpoint pode ser chamado mais de uma vez.

**7. Parser de CSV próprio**
`Split(',')` quebrava campos com vírgula entre aspas (ex.: endereço). O parser dedicado trata aspas e escapes corretamente.

**8. Testes com EF Core InMemory**
Rápidos e sem dependência externa. As regras testadas são aplicadas em código; a unicidade de índice (específica do Postgres) é validada em integração/manual.

**9. DTOs como `record` posicionais com `Ativo = true` por padrão**
Mantém compatibilidade com chamadas que não enviam o campo, sem quebrar contratos existentes.

---

<div align="center">
<sub>Documento de solução — Parking · .NET 8 + React + PostgreSQL</sub>
</div>
