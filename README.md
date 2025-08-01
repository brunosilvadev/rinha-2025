# Rinha de Backend 2025 - Submissão em .NET 9

Este projeto é uma submissão para a Rinha de Backend 2025, implementada em .NET 9.

## TODO

- [x] Implementar a lógica para decidir qual payment processor utilizar (rate limiting, caching)
- [x] Passar testes de performance
- [x] Avaliar necessidade de usar fila DLQ
- [x] Calcular taxa cobrada total final
- [x] Limpar comentários
- [x] Revisar configurações de CPU e memória
- [x] Remover lógica desnecessária para minimizar o pacote
- [x] Remover arquivos desnecessários do repositório
- [ ] Melhorar o README do stress tester
- [ ] Fazer o PR para mandar


# Integração com Payment Processors - Rinha 2025

## Configuração Docker Compose

Este projeto foi configurado para integrar com os Payment Processors da Rinha de Backend 2025.

### Pré-requisitos

1. **Subir os Payment Processors primeiro:**
   ```bash
   # Clone o repositório da Rinha
   git clone https://github.com/zanfranceschi/rinha-de-backend-2025.git
   
   # Navegue até o diretório dos payment processors
   cd rinha-de-backend-2025/payment-processor
   
   # Suba os payment processors
   docker-compose up -d
   ```

2. **Subir sua aplicação:**
   ```bash
   # No diretório do seu projeto
   docker-compose up -d
   ```

### URLs dos Payment Processors

Após subir os containers, os Payment Processors estarão disponíveis em:

- **Default:** `http://payment-processor-default:8080` (interno) / `http://localhost:8001` (externo)
- **Fallback:** `http://payment-processor-fallback:8080` (interno) / `http://localhost:8002` (externo)

### Variáveis de Ambiente

O docker-compose.yml foi configurado com as seguintes variáveis de ambiente:

```yaml
environment:
  - PROCESSOR_DEFAULT_URL=http://payment-processor-default:8080
  - PROCESSOR_FALLBACK_URL=http://payment-processor-fallback:8080
```

### Configuração de Rede

- **Rede externa:** `payment-processor` (criada pelos containers dos payment processors)
- **Rede interna:** `backend` (para comunicação entre nginx e apis)

### Fluxo de Processamento

1. **Tentativa no Payment Processor Default** primeiro (menor taxa)
2. **Fallback automático** para o Payment Processor Fallback em caso de falha
3. **Logs detalhados** de cada tentativa e resultado

### Endpoints Disponíveis

- `POST /payments` - Processa pagamentos
- `GET /payments-summary` - Resumo dos pagamentos processados
- `GET /` - Health check básico

### Teste Local

Use o arquivo `rest.http` para testar os endpoints:

```http
@baseUrl = http://localhost:9999

# Health check
GET {{baseUrl}}/

# Processar pagamento
POST {{baseUrl}}/payments
Content-Type: application/json

{
    "correlationId": "4a7901b8-7d26-4d9d-aa19-4dc1c7cf60b3",
    "amount": 19.90
}

# Consultar resumo
GET {{baseUrl}}/payments-summary?from=2025-07-01T00:00:00Z&to=2025-07-21T23:59:59Z
```

### Recursos Configurados

- **CPU Total:** 1.5 unidades (0.65 + 0.65 + 0.10 + 0.10)
- **Memória Total:** 350MB (125MB + 125MB + 32MB + 68MB)
